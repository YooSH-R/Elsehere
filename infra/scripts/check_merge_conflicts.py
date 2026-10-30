"""Reject MR pipelines that cannot merge into the latest target branch."""

import os
import subprocess
import sys
from urllib.parse import quote, urlsplit, urlunsplit


def target_repository(environment):
    if environment["CI_PROJECT_ID"] == environment["CI_MERGE_REQUEST_PROJECT_ID"]:
        return "origin"

    # Fork pipelines have the source project as origin; fetch the MR's target project.
    url = environment["CI_MERGE_REQUEST_PROJECT_URL"].rstrip("/") + ".git"
    parts = urlsplit(url)
    token = environment.get("CI_JOB_TOKEN")
    if token and parts.scheme in ("http", "https"):
        authority = f"gitlab-ci-token:{quote(token, safe='')}@{parts.netloc}"
        url = urlunsplit(parts._replace(netloc=authority))
    return url


def main():
    if os.environ.get("CI_PIPELINE_SOURCE") != "merge_request_event":
        print("Skipping merge conflict check outside an MR pipeline.")
        return 0

    try:
        branch = os.environ["CI_MERGE_REQUEST_TARGET_BRANCH_NAME"]
        source = (
            os.environ.get("CI_MERGE_REQUEST_SOURCE_BRANCH_SHA")
            or os.environ["CI_COMMIT_SHA"]
        )
        remote = target_repository(os.environ)
    except KeyError as error:
        print(
            f"Missing MR variable: {error.args[0]}; checks remain blocked.",
            file=sys.stderr,
        )
        return 2

    target = "refs/ci/merge-target"
    fetched = subprocess.run(
        [
            "git",
            "fetch",
            "--no-tags",
            "--refmap=",
            remote,
            f"+refs/heads/{branch}:{target}",
        ],
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    if fetched.returncode != 0:
        # Fetch errors can include authenticated URLs. Do not print credentials to CI logs.
        print(
            "Could not fetch the MR target branch; checks remain blocked.",
            file=sys.stderr,
        )
        return 2

    print(f"Checking {source} against latest target branch {branch}.")
    source_result = subprocess.run(
        ["git", "rev-parse", "--verify", "--end-of-options", f"{source}^{{commit}}"],
        check=False,
        capture_output=True,
        text=True,
    )
    if source_result.returncode != 0:
        print(
            "Could not check the source commit; checks remain blocked.", file=sys.stderr
        )
        return 2

    result = subprocess.run(
        ["git", "merge-tree", "--write-tree", "--name-only", target, source],
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    if result.returncode == 0:
        print("No merge conflicts. Remaining checks may continue.")
        return 0
    if result.returncode == 1:
        print(result.stdout)
        print(
            "Merge conflicts found. Resolve them before running the remaining checks."
        )
        return 1

    print("Could not check merge conflicts; checks remain blocked.", file=sys.stderr)
    print(result.stderr, file=sys.stderr)
    return 2


if __name__ == "__main__":
    sys.exit(main())
