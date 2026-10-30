"""Plan, verify and deliver protected releases without a container registry."""

import argparse
import gzip
import json
import os
import re
import shutil
import subprocess
import tarfile
from pathlib import Path

from deployment_plan import ALL_CHECKS, commit_exists, create_plan, run_git
from docker_build import build_image


class SupersededCandidateError(ValueError):
    """The candidate is already deployed, older than a release, or not the prod head."""


def create_release_plan(repository, state, candidate):
    plan = create_plan(repository, state["candidate_commit"], candidate)
    required_monitoring = state["minimum_backend_commit"]
    if "backend" in plan["builds"] and (
        not commit_exists(repository, required_monitoring)
        or run_git(
            repository, "merge-base", "--is-ancestor", required_monitoring, candidate
        ).returncode
        != 0
    ):
        raise ValueError(
            "Candidate is missing the deployed monitoring code; merge MR !56 first."
        )
    plan["configuration_changed"] = plan["reason"] != "cumulative-diff" or (
        run_git(
            repository,
            "diff",
            "--quiet",
            state["candidate_commit"],
            candidate,
            "--",
            "infra/compose/compose.app.yaml",
        ).returncode
        != 0
    )
    return plan


def create_child_pipeline(
    plan, parent_pipeline_id, state=None, environment="dev", *, automatic=False
):
    checks = ALL_CHECKS if environment == "prod" else set(plan["checks"])
    jobs = ["ci:yaml"]
    selections = {
        "backend": ["backend:check-and-build", "infra:app-runtime"],
        "ai": ["ai:check-and-test", "ai:panel-tests"],
        "unity": ["unity:format", "unity:editor-and-webgl"],
        "infra": ["infra:deployment-plan-tests", "infra:compose-and-nginx"],
    }
    for area, selected_jobs in selections.items():
        if area in checks:
            jobs.extend(selected_jobs)
    if checks & {"backend", "infra"}:
        jobs.append("infra:observability")
    variables = {
        "A305_DEPLOY_CHILD": "true",
        "A305_DEPLOY_ENVIRONMENT": environment,
        "A305_PARENT_PIPELINE_ID": str(parent_pipeline_id),
        "A305_DEPLOY_PLAN": json.dumps(
            {key: value for key, value in plan.items() if key != "changed_paths"}
        ),
        "A305_DEPLOY_STATE": json.dumps(state),
        **{
            f"A305_CHECK_{area.upper()}": str(area in checks).lower()
            for area in selections
        },
        "A305_BUILD_WEB": str("web" in plan["builds"]).lower(),
    }
    if environment == "prod":
        variables["A305_PROD_AUTOMATIC"] = str(automatic).lower()
    return {
        "include": [{"local": ".gitlab-ci.yml"}],
        "variables": variables,
        f"{environment}:deliver": {
            "needs": [{"job": job, "artifacts": True} for job in jobs]
        },
    }


def ssh_command(environment="dev"):
    prefix = environment.upper()
    key = Path(os.environ[f"{prefix}_DEPLOY_SSH_KEY"])
    known_hosts = Path(os.environ[f"{prefix}_DEPLOY_KNOWN_HOSTS"])
    if not key.is_file() or not known_hosts.is_file():
        raise ValueError(
            "Deploy SSH credentials must be protected GitLab File variables."
        )
    key.chmod(0o600)
    return [
        "ssh",
        "-T",
        "-i",
        str(key),
        "-o",
        "BatchMode=yes",
        "-o",
        "IdentitiesOnly=yes",
        "-o",
        "StrictHostKeyChecking=yes",
        "-o",
        f"UserKnownHostsFile={known_hosts}",
        "-o",
        "ConnectTimeout=15",
        "ubuntu@k15a305.p.ssafy.io",
    ]


def ssh_failure_message(stderr, environment="dev"):
    # Report only known categories: stderr can contain file-variable values.
    message = stderr.decode("utf-8", errors="replace").lower()
    if "error in libcrypto" in message or "invalid format" in message:
        return (
            "Deploy SSH private key format is invalid; check the complete "
            f"{environment.upper()}_DEPLOY_SSH_KEY content and its final LF newline."
        )
    if (
        "host key verification failed" in message
        or "remote host identification has changed" in message
    ):
        return (
            "Deploy SSH host key verification failed; check the pinned "
            f"{environment.upper()}_DEPLOY_KNOWN_HOSTS file."
        )
    if "permission denied (publickey" in message:
        return (
            f"Deploy SSH authentication failed; check {environment.upper()}_DEPLOY_SSH_KEY "
            "and its restricted authorized_keys entry."
        )
    if "could not resolve hostname" in message:
        return "Deploy SSH hostname resolution failed."
    if "connection refused" in message:
        return "Deploy SSH connection refused; check the SSH listener and route."
    if "connection timed out" in message or "operation timed out" in message:
        return "Deploy SSH connection timed out; check the network route and port 22."
    return "Deploy SSH connection failed; check the restricted key and host key."


def server_request(operation, archive=None, environment="dev"):
    result = subprocess.run(
        [*ssh_command(environment), operation],
        check=False,
        stdin=archive,
        capture_output=True,
        timeout=900 if operation == "apply" else 60,
    )
    try:
        response = json.loads(result.stdout)
    except (ValueError, UnicodeDecodeError) as error:
        raise RuntimeError(ssh_failure_message(result.stderr, environment)) from error
    if result.returncode or "error" in response:
        raise RuntimeError(
            response.get("error", "Deployment server rejected the request.")
        )
    return response


def verify_current_candidate(repository, branch, candidate):
    result = run_git(repository, "ls-remote", "origin", f"refs/heads/{branch}")
    head = result.stdout.decode().split()
    if result.returncode or not head:
        raise RuntimeError("Could not determine the deployment branch head.")
    if head[0] != candidate:
        raise SupersededCandidateError(
            "Candidate is no longer the branch head; run its newest pipeline."
        )


def verify_development_candidate(repository, branch, candidate, state=None):
    fetched = run_git(repository, "fetch", "--no-tags", "origin", branch)
    if fetched.returncode:
        raise RuntimeError("Could not determine the deployment branch history.")
    ancestry = run_git(
        repository, "merge-base", "--is-ancestor", candidate, "FETCH_HEAD"
    )
    if ancestry.returncode:
        raise ValueError("Candidate does not belong to the deployment branch history.")
    deployed = state.get("candidate_commit") if state else None
    if deployed is None:
        return
    if (
        run_git(
            repository, "merge-base", "--is-ancestor", candidate, deployed
        ).returncode
        == 0
    ):
        raise SupersededCandidateError("Candidate is already deployed or superseded.")
    if (
        run_git(
            repository, "merge-base", "--is-ancestor", deployed, candidate
        ).returncode
        != 0
    ):
        raise ValueError("Candidate does not extend the running deployment.")


def plan_pipeline(
    repository, candidate, branch, pipeline_id, environment="dev", *, automatic=False
):
    # Dev checks survive later merges; delivery rechecks the running release instead
    # of requiring the branch head. Production still requires its exact current head.
    if environment == "prod":
        verify_current_candidate(repository, branch, candidate)
    state = server_request("state", environment=environment)
    if state.get("environment", "dev") != environment:
        raise ValueError("The deployment key is connected to another environment.")
    plan = create_release_plan(repository, state, candidate)
    if environment == "prod":
        plan["checks"] = sorted(ALL_CHECKS)
    directory = repository / "dist/deployment"
    directory.mkdir(parents=True, exist_ok=True)
    for name, document in [
        ("state.json", state),
        ("plan.json", plan),
        (
            f"{environment}-child.yml",
            create_child_pipeline(
                plan, pipeline_id, state, environment, automatic=automatic
            ),
        ),
    ]:
        (directory / name).write_text(
            json.dumps(document, indent=2) + "\n", encoding="utf-8"
        )
    print(
        json.dumps(
            {
                "checks": plan["checks"],
                "builds": plan["builds"],
                "reason": plan["reason"],
            }
        )
    )


def docker(*arguments, **options):
    return subprocess.run(["docker", *map(str, arguments)], check=True, **options)


def deliver_pipeline(repository, candidate, branch, pipeline_id, environment="dev"):
    directory = repository / "dist/deployment"
    directory.mkdir(parents=True, exist_ok=True)
    plan = json.loads(os.environ["A305_DEPLOY_PLAN"])
    state = json.loads(os.environ["A305_DEPLOY_STATE"])
    if state.get("environment", "dev") != environment:
        raise ValueError("The deployment state belongs to another environment.")
    if plan["candidate_commit"] != candidate:
        raise ValueError("Deployment plan belongs to another candidate.")
    if environment == "dev":
        state = server_request("state", environment=environment)
        if state.get("environment", "dev") != environment:
            raise ValueError("The deployment state belongs to another environment.")
        verify_development_candidate(repository, branch, candidate, state)
        current_plan = create_release_plan(repository, state, candidate)
        for selection in ("checks", "builds"):
            if not set(current_plan[selection]).issubset(plan[selection]):
                raise ValueError(
                    f"Deployment baseline requires {selection} not verified by this "
                    "pipeline; run a new pipeline for this candidate."
                )
        plan = current_plan
    else:
        verify_current_candidate(repository, branch, candidate)
    images = state["images"].copy()
    dockerfiles = {
        "backend": "backend.Dockerfile",
        "ai": "ai-worker.Dockerfile",
        "web": "web.Dockerfile",
    }
    for name in plan["builds"]:
        if name in {"backend", "ai"}:
            marker = (
                "backend/build/commit.txt"
                if name == "backend"
                else "dist/ai-commit.txt"
            )
            if (repository / marker).read_text(encoding="utf-8").strip() != candidate:
                raise ValueError("Verified build artifact belongs to another commit.")
        else:
            version = json.loads(
                (repository / "dist/webgl/version.json").read_text(encoding="utf-8")
            )
            if (
                version.get("commit") != candidate
                or version.get("build_type") != "unity-webgl"
            ):
                raise ValueError(
                    "A real WebGL artifact for this candidate is required."
                )
        reference = f"a305/{name}:{candidate}"
        build_image(
            docker,
            repository,
            name,
            reference,
            f"infra/docker/{dockerfiles[name]}",
            labels={"org.opencontainers.image.revision": candidate},
            cache_directory=os.environ.get("A305_DOCKER_CACHE_DIR"),
        )
        image_id = (
            docker(
                "image",
                "inspect",
                "--format",
                "{{.Id}}",
                reference,
                capture_output=True,
            )
            .stdout.decode()
            .strip()
        )
        images[name] = {
            "reference": reference,
            "id": image_id,
            "source_commit": candidate,
        }

    image_archive = directory / "images.tar.gz"
    if plan["builds"]:
        with image_archive.open("wb") as target:
            process = subprocess.Popen(
                [
                    "docker",
                    "save",
                    *[images[name]["reference"] for name in plan["builds"]],
                ],
                stdout=subprocess.PIPE,
            )
            with gzip.GzipFile(fileobj=target, mode="wb") as compressed:
                shutil.copyfileobj(process.stdout, compressed)
            process.stdout.close()
            if process.wait() != 0:
                raise RuntimeError("Could not export verified images.")
    manifest = {
        "schema_version": 1,
        "candidate_commit": candidate,
        "pipeline_id": int(pipeline_id),
        "branch": branch,
        "environment": environment,
        "expected_state_token": state["state_token"],
        "minimum_backend_commit": state["minimum_backend_commit"],
        "builds": plan["builds"],
        "configuration_changed": plan["configuration_changed"],
        "images": images,
    }
    if environment == "dev":
        manifest["verified_base_commit"] = state["candidate_commit"]
    manifest_path = directory / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    bundle = directory / "release.tar"
    with tarfile.open(bundle, "w") as archive:
        archive.add(manifest_path, arcname="manifest.json")
        archive.add(
            repository / "infra/compose/compose.app.yaml", arcname="compose.app.yaml"
        )
        if plan["builds"]:
            archive.add(image_archive, arcname="images.tar.gz")
    if environment == "dev":
        verify_development_candidate(repository, branch, candidate)
    else:
        verify_current_candidate(repository, branch, candidate)
    with bundle.open("rb") as source:
        result = server_request("apply", source, environment)
    (directory / "result.json").write_text(
        json.dumps(result, indent=2) + "\n", encoding="utf-8"
    )
    print(
        json.dumps(
            {"result": result["result"], "candidate_commit": result["candidate_commit"]}
        )
    )


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=["plan", "deliver"])
    parser.add_argument("--environment", choices=["dev", "prod"], default="dev")
    arguments = parser.parse_args()
    branch = os.environ["CI_COMMIT_BRANCH"]
    environment = arguments.environment
    deployment_branch = "main" if environment == "prod" else "dev"
    if (
        os.environ.get("CI_COMMIT_REF_PROTECTED") != "true"
        or branch != deployment_branch
    ):
        raise ValueError("Only the protected deployment branch may deploy.")
    repository = Path(os.environ["CI_PROJECT_DIR"])
    candidate = os.environ["CI_COMMIT_SHA"]
    automatic = False
    if environment == "prod":
        source = os.environ.get("CI_PIPELINE_SOURCE")
        if arguments.action == "plan":
            if source not in {"push", "web"}:
                raise ValueError(
                    "Production plans require a main push or manual pipeline."
                )
            automatic = source == "push"
        else:
            if (
                source != "parent_pipeline"
                or os.environ.get("A305_DEPLOY_CHILD") != "true"
                or os.environ.get("A305_DEPLOY_ENVIRONMENT") != "prod"
            ):
                raise ValueError(
                    "Production delivery requires its production child pipeline."
                )
            automatic = os.environ.get("A305_PROD_AUTOMATIC") == "true"
    if environment == "prod" and not automatic:
        if os.environ.get("PROD_VERIFIED_COMMIT") != candidate:
            raise ValueError(
                "PROD_VERIFIED_COMMIT must match this candidate's integration-tested SHA."
            )
        if not re.fullmatch(
            r"https://lab\.ssafy\.com/s15-final/S15P31A305/-/merge_requests/[1-9][0-9]*",
            os.environ.get("PROD_RELEASE_MR", ""),
        ):
            raise ValueError(
                "PROD_RELEASE_MR must identify the reviewed release and integration evidence."
            )
        if os.environ.get("PROD_DB_COMPATIBLE") != "true":
            raise ValueError(
                "Confirm database backup and application rollback compatibility with PROD_DB_COMPATIBLE=true."
            )
    pipeline_id = os.environ["CI_PIPELINE_ID"]
    if arguments.action == "plan":
        if environment == "prod":
            plan_pipeline(
                repository,
                candidate,
                branch,
                pipeline_id,
                environment,
                automatic=automatic,
            )
        else:
            plan_pipeline(repository, candidate, branch, pipeline_id, environment)
    else:
        try:
            deliver_pipeline(repository, candidate, branch, pipeline_id, environment)
        except SupersededCandidateError:
            if environment != "dev":
                raise
            result = {"result": "superseded", "candidate_commit": candidate}
            directory = repository / "dist/deployment"
            directory.mkdir(parents=True, exist_ok=True)
            (directory / "result.json").write_text(
                json.dumps(result, indent=2) + "\n", encoding="utf-8"
            )
            print(json.dumps(result))
            # Keep the completed checks successful without recording a deployment.
            return 78


if __name__ == "__main__":
    raise SystemExit(main())
