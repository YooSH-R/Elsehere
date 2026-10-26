"""Plan, verify and deliver protected dev releases without a container registry."""

import argparse
import gzip
import json
import os
import shutil
import subprocess
import tarfile
from pathlib import Path

from deployment_plan import commit_exists, create_plan, run_git


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


def create_child_pipeline(plan, parent_pipeline_id, state=None):
    checks = set(plan["checks"])
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
    variables = {
        "A305_DEPLOY_CHILD": "true",
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
    return {
        "include": [{"local": ".gitlab-ci.yml"}],
        "variables": variables,
        "dev:deliver": {"needs": [{"job": job, "artifacts": True} for job in jobs]},
    }


def ssh_command():
    key = Path(os.environ["DEV_DEPLOY_SSH_KEY"])
    known_hosts = Path(os.environ["DEV_DEPLOY_KNOWN_HOSTS"])
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


def server_request(operation, archive=None):
    result = subprocess.run(
        [*ssh_command(), operation],
        check=False,
        stdin=archive,
        capture_output=True,
        timeout=900 if operation == "apply" else 60,
    )
    try:
        response = json.loads(result.stdout)
    except (ValueError, UnicodeDecodeError) as error:
        raise RuntimeError(
            "Deploy SSH connection failed; check the restricted key and host key."
        ) from error
    if result.returncode or "error" in response:
        raise RuntimeError(
            response.get("error", "Deployment server rejected the request.")
        )
    return response


def verify_current_candidate(repository, branch, candidate):
    result = run_git(repository, "ls-remote", "origin", f"refs/heads/{branch}")
    head = result.stdout.decode().split()
    if result.returncode or not head or head[0] != candidate:
        raise ValueError(
            "Candidate is no longer the branch head; run its newest pipeline."
        )


def plan_pipeline(repository, candidate, branch, pipeline_id):
    verify_current_candidate(repository, branch, candidate)
    state = server_request("state")
    plan = create_release_plan(repository, state, candidate)
    directory = repository / "dist/deployment"
    directory.mkdir(parents=True, exist_ok=True)
    for name, document in [
        ("state.json", state),
        ("plan.json", plan),
        ("dev-child.yml", create_child_pipeline(plan, pipeline_id, state)),
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


def deliver_pipeline(repository, candidate, branch, pipeline_id):
    directory = repository / "dist/deployment"
    directory.mkdir(parents=True, exist_ok=True)
    plan = json.loads(os.environ["A305_DEPLOY_PLAN"])
    state = json.loads(os.environ["A305_DEPLOY_STATE"])
    if plan["candidate_commit"] != candidate:
        raise ValueError("Deployment plan belongs to another candidate.")
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
        docker(
            "build",
            "--label",
            f"org.opencontainers.image.revision={candidate}",
            "--tag",
            reference,
            "--file",
            f"infra/docker/{dockerfiles[name]}",
            repository,
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
        "expected_state_token": state["state_token"],
        "minimum_backend_commit": state["minimum_backend_commit"],
        "builds": plan["builds"],
        "configuration_changed": plan["configuration_changed"],
        "images": images,
    }
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
    # A newer push during a long Unity/image build must not be replaced by this job.
    verify_current_candidate(repository, branch, candidate)
    with bundle.open("rb") as source:
        result = server_request("apply", source)
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
    arguments = parser.parse_args()
    branch = os.environ["CI_COMMIT_BRANCH"]
    if os.environ.get("CI_COMMIT_REF_PROTECTED") != "true" or branch not in {
        "dev",
        "chore/S15P31A305-37-server-environments",
    }:
        raise ValueError(
            "Only protected dev and the explicit story 37 pilot may deploy."
        )
    repository = Path(os.environ["CI_PROJECT_DIR"])
    candidate = os.environ["CI_COMMIT_SHA"]
    pipeline_id = os.environ["CI_PIPELINE_ID"]
    if arguments.action == "plan":
        plan_pipeline(repository, candidate, branch, pipeline_id)
    else:
        deliver_pipeline(repository, candidate, branch, pipeline_id)


if __name__ == "__main__":
    main()
