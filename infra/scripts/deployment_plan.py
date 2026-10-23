"""Select dev deployment work from the last successful deployment's Git diff."""

import argparse
import json
import re
import subprocess
from pathlib import Path, PurePosixPath

ALL_CHECKS = {"ai", "backend", "infra", "unity"}
ALL_IMAGES = {"ai", "backend", "web"}


def select_deployment_work(changed_paths):
    checks = set()
    builds = set()
    runtime_configuration_changed = False
    image_configurations = {
        "infra/docker/backend.Dockerfile": ("backend", "backend"),
        "infra/docker/ai-worker.Dockerfile": ("ai", "ai"),
        "infra/docker/web.Dockerfile": ("unity", "web"),
        "infra/nginx/web.conf": ("unity", "web"),
    }

    for path in changed_paths:
        if path.startswith(("docs/api/", ".gitlab/ci/")) or path in {
            "docs/erd.dbml",
            ".gitlab-ci.yml",
            "AGENTS.md",
            "docs/code-conventions.md",
        }:
            checks.update(ALL_CHECKS)
        elif path == ".dockerignore":
            checks.update(ALL_CHECKS)
            builds.update(ALL_IMAGES)
        elif path in image_configurations:
            check, image = image_configurations[path]
            checks.update({check, "infra"})
            builds.add(image)
        elif path.startswith("infra/compose/") and not path.endswith(".md"):
            checks.update(ALL_CHECKS)
            runtime_configuration_changed = True
        elif path.startswith("infra/"):
            checks.add("infra")
        elif path.endswith(".md"):
            continue
        elif path.startswith("backend/"):
            checks.add("backend")
            if not path.startswith("backend/src/test/"):
                builds.add("backend")
        elif path.startswith("ai/"):
            checks.add("ai")
            if "tests" not in PurePosixPath(path).parts:
                builds.add("ai")
        elif path.startswith("frontend/"):
            checks.add("unity")
            if not path.startswith("frontend/UnityClient/Assets/Tests/"):
                builds.add("web")

    return {
        "checks": sorted(checks),
        "builds": sorted(builds),
        "deploy": bool(builds) or runtime_configuration_changed,
    }


def run_git(repository, *arguments):
    return subprocess.run(
        ["git", "-C", str(repository), *arguments],
        capture_output=True,
        check=False,
    )


def commit_exists(repository, commit):
    if not commit or not re.fullmatch(r"(?:[0-9a-f]{40}|[0-9a-f]{64})", commit):
        return False
    return run_git(repository, "cat-file", "-e", f"{commit}^{{commit}}").returncode == 0


def create_plan(repository, base_commit, candidate_commit):
    if not commit_exists(repository, candidate_commit):
        raise ValueError("Candidate must be the full SHA of an available Git commit.")

    reason = "cumulative-diff"
    if base_commit is None:
        reason = "first-deployment"
    elif not commit_exists(repository, base_commit):
        reason = "missing-base-history"
    elif (
        run_git(
            repository, "merge-base", "--is-ancestor", base_commit, candidate_commit
        ).returncode
        != 0
    ):
        reason = "base-not-ancestor"

    changed_paths = []
    if reason == "cumulative-diff":
        difference = run_git(
            repository,
            "diff",
            "--name-only",
            "--no-renames",
            "-z",
            base_commit,
            candidate_commit,
            "--",
        )
        if difference.returncode != 0:
            raise RuntimeError(
                "Could not compare the successful deployment with the candidate."
            )
        changed_paths = difference.stdout.decode("utf-8").split("\0")
        changed_paths = [path for path in changed_paths if path]
        work = select_deployment_work(changed_paths)
    else:
        # An unknown baseline must never silently skip an undeployed application change.
        work = {
            "checks": sorted(ALL_CHECKS),
            "builds": sorted(ALL_IMAGES),
            "deploy": True,
        }

    return {
        "schema_version": 1,
        "base_commit": base_commit,
        "candidate_commit": candidate_commit,
        "reason": reason,
        "changed_paths": changed_paths,
        **work,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository", type=Path, default=Path.cwd())
    parser.add_argument(
        "--base", help="Full SHA from the last successful deployment record."
    )
    parser.add_argument(
        "--candidate", required=True, help="Full SHA of the deployment candidate."
    )
    parser.add_argument("--output", type=Path, required=True)
    arguments = parser.parse_args()

    plan = create_plan(arguments.repository, arguments.base, arguments.candidate)
    arguments.output.parent.mkdir(parents=True, exist_ok=True)
    arguments.output.write_text(json.dumps(plan, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
