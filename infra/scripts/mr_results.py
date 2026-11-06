"""Reuse successful merge request checks only when their inputs still match."""

import argparse
import hashlib
import json
import os
import re
import shutil
import stat
import zipfile
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.parse import quote, urlencode
from urllib.request import Request, urlopen

from deployment_plan import commit_exists, run_git

COMMON_INPUTS = (
    ".editorconfig",
    ".gitattributes",
    ".gitlab-ci.yml",
    ".gitlab/ci/",
    "AGENTS.md",
    "docs/code-conventions.md",
    "infra/scripts/mr_results.py",
    "infra/scripts/record-ci-success.sh",
)
JOB_INPUTS = {
    "backend:check-and-build": (
        "backend/",
        "docs/api/",
        "docs/erd.dbml",
        ".dockerignore",
        "infra/",
    ),
    "ai:check-and-test": ("ai/", "docs/api/"),
    "ai:panel-tests": ("ai/feedback/", "docs/api/"),
    "unity:format": (
        "frontend/",
        "docs/api/",
        "docs/unity/",
        "infra/scripts/",
        "infra/tests/tripo/",
    ),
    "unity:browser-tests": (
        "frontend/UnityClient/Assets/Plugins/WebGL/",
        "frontend/UnityClient/Assets/WebGLTemplates/",
        "infra/tests/",
    ),
    "infra:deployment-plan-tests": ("infra/", ".yamllint.yml"),
    "infra:compose-and-nginx": ("infra/",),
    "infra:observability": ("infra/",),
    "infra:app-runtime": ("backend/", "ai/", "infra/", "docs/api/", ".dockerignore"),
}
BUILD_ARTIFACTS = {
    "backend:check-and-build": (
        "backend/build/commit.txt",
        "backend/build/libs/app.jar",
    ),
    "ai:check-and-test": ("dist/ai-commit.txt", "dist/ai-source.tar.gz"),
}
RECEIPT_PATH = "dist/ci/success.json"
SHA_PATTERN = r"[0-9a-f]{40}(?:[0-9a-f]{24})?"


def api_request(endpoint):
    url = f"{os.environ['CI_API_V4_URL']}/projects/{os.environ['CI_PROJECT_ID']}/{endpoint}"
    request = Request(url)
    # A storage redirect must not receive the GitLab credential.
    request.add_unredirected_header("JOB-TOKEN", os.environ["CI_JOB_TOKEN"])
    return request


def fetch_json(endpoint):
    with urlopen(api_request(endpoint), timeout=10) as response:
        data = response.read(4 * 1024 * 1024 + 1)
    if len(data) > 4 * 1024 * 1024:
        raise ValueError("MR result metadata is too large.")
    return json.loads(data)


def download_archive(endpoint, destination):
    with (
        urlopen(api_request(endpoint), timeout=120) as response,
        destination.open("wb") as output,
    ):
        total = 0
        while chunk := response.read(1024 * 1024):
            total += len(chunk)
            if total > 256 * 1024 * 1024:
                raise ValueError("MR artifacts exceed the reuse size limit.")
            output.write(chunk)


def same_job_inputs(repository, job, source, candidate):
    if not commit_exists(repository, source) or not commit_exists(
        repository, candidate
    ):
        return False
    return (
        run_git(
            repository,
            "diff",
            "--quiet",
            source,
            candidate,
            "--",
            *COMMON_INPUTS,
            *JOB_INPUTS[job],
        ).returncode
        == 0
    )


def valid_receipt(receipt, job, source):
    return (
        isinstance(receipt, dict)
        and set(receipt)
        == {"schema_version", "commit", "project_id", "pipeline_id", "job_id", "job"}
        and receipt.get("schema_version") == 1
        and receipt.get("commit") == source
        and receipt.get("job") == job
        and str(receipt.get("project_id")) == os.environ["CI_PROJECT_ID"]
        and all(
            re.fullmatch(r"[1-9][0-9]*", str(receipt.get(key, "")))
            for key in ("pipeline_id", "job_id")
        )
    )


def artifact_endpoint(mr_iid, job, filename=None):
    ref = quote(f"refs/merge-requests/{mr_iid}/head", safe="")
    action = f"raw/{filename}" if filename else "download"
    return f"jobs/artifacts/{ref}/{action}?{urlencode({'job': job})}"


def read_artifact_member(archive, name):
    member = archive.getinfo(name)
    if (
        stat.S_ISLNK(member.external_attr >> 16)
        or member.is_dir()
        or member.file_size > 256 * 1024 * 1024
    ):
        raise ValueError("MR build artifact must be a bounded regular file.")
    return member


def verify_archive(path, job, receipt):
    with zipfile.ZipFile(path) as archive:
        if read_artifact_member(archive, RECEIPT_PATH).file_size > 65536:
            raise ValueError("MR success receipt is too large.")
        if json.loads(archive.read(RECEIPT_PATH)) != receipt:
            raise ValueError("MR artifact and success receipt do not match.")
        for name in BUILD_ARTIFACTS[job]:
            read_artifact_member(archive, name)
        if archive.getinfo(BUILD_ARTIFACTS[job][0]).file_size > 128:
            raise ValueError("MR source commit marker is too large.")
        marker = archive.read(BUILD_ARTIFACTS[job][0]).decode("utf-8").strip()
        if marker != receipt["commit"]:
            raise ValueError("MR build belongs to another source commit.")


def eligible_merge_request(repository, candidate, merge_request):
    if not isinstance(merge_request, dict):
        return False
    project = int(os.environ["CI_PROJECT_ID"])
    source = merge_request.get("sha", "")
    merged = merge_request.get("merge_commit_sha", "")
    return (
        merge_request.get("state") == "merged"
        and merge_request.get("target_branch") == "dev"
        and merge_request.get("source_project_id") == project
        and merge_request.get("target_project_id") == project
        and isinstance(source, str)
        and re.fullmatch(SHA_PATTERN, source)
        and isinstance(merged, str)
        and commit_exists(repository, merged)
        and run_git(
            repository, "merge-base", "--is-ancestor", merged, candidate
        ).returncode
        == 0
        and isinstance(merge_request.get("iid"), int)
        and merge_request["iid"] > 0
    )


def find_reusable_jobs(repository, candidate, jobs, output):
    if not all(
        os.environ.get(key)
        for key in ("CI_PROJECT_ID", "CI_API_V4_URL", "CI_JOB_TOKEN")
    ):
        return {}
    failures = (
        HTTPError,
        URLError,
        TimeoutError,
        OSError,
        ValueError,
        TypeError,
        KeyError,
        zipfile.BadZipFile,
    )
    try:
        merge_requests = fetch_json(
            "merge_requests?state=merged&target_branch=dev&order_by=updated_at&sort=desc&per_page=20"
        )
        if not isinstance(merge_requests, list):
            raise TypeError("Expected merge request results.")
    except failures:
        print("MR results unavailable; run the selected checks in this pipeline.")
        return {}
    reused = {}
    for job in sorted(set(jobs) & JOB_INPUTS.keys()):
        attempts = 0
        for merge_request in merge_requests:
            if not eligible_merge_request(repository, candidate, merge_request):
                continue
            source = merge_request["sha"]
            if not same_job_inputs(repository, job, source, candidate):
                continue
            # Bound lookups when older MR pipelines do not yet publish receipts.
            attempts += 1
            if attempts > 2:
                break
            iid = merge_request["iid"]
            try:
                receipt = fetch_json(artifact_endpoint(iid, job, RECEIPT_PATH))
                if not valid_receipt(receipt, job, source):
                    continue
                result = {"mr_iid": iid, "source_commit": source, "receipt": receipt}
                if job in BUILD_ARTIFACTS:
                    output.mkdir(parents=True, exist_ok=True)
                    path = output / (job.split(":")[0] + "-mr-artifacts.zip")
                    download_archive(artifact_endpoint(iid, job), path)
                    verify_archive(path, job, receipt)
                    with path.open("rb") as archive:
                        result["sha256"] = hashlib.file_digest(
                            archive, "sha256"
                        ).hexdigest()
                    result["archive"] = path.relative_to(repository).as_posix()
                reused[job] = result
                print(
                    f"Reuse {job} from MR !{iid}, pipeline {receipt['pipeline_id']}, job {receipt['job_id']}."
                )
                break
            except failures as error:
                # Missing, expired or inaccessible artifacts never count as a passed check.
                if isinstance(error, (URLError, TimeoutError)) and not (
                    isinstance(error, HTTPError) and error.code in {404, 410}
                ):
                    print("MR artifact lookup unavailable; run the remaining checks.")
                    return reused
                continue
    return reused


def restore_results(repository, candidate):
    directory = repository / "dist/deployment"
    plan = json.loads((directory / "plan.json").read_text(encoding="utf-8"))
    if plan["candidate_commit"] != candidate:
        raise ValueError("MR results belong to another deployment candidate.")
    reused = plan["reused_jobs"]
    for job, result in reused.items():
        source = result["source_commit"]
        if not valid_receipt(result["receipt"], job, source) or not same_job_inputs(
            repository, job, source, candidate
        ):
            raise ValueError("MR check inputs do not match this deployment candidate.")
        if job not in BUILD_ARTIFACTS:
            continue
        path = (repository / result["archive"]).resolve()
        if not path.is_relative_to(directory.resolve()):
            raise ValueError("MR artifacts must come from the deployment plan.")
        with path.open("rb") as archive:
            checksum = hashlib.file_digest(archive, "sha256").hexdigest()
        if checksum != result["sha256"]:
            raise ValueError("MR artifact checksum does not match the plan.")
        verify_archive(path, job, result["receipt"])
        with zipfile.ZipFile(path) as archive:
            for name in BUILD_ARTIFACTS[job]:
                destination = repository / name
                if not destination.resolve().is_relative_to(repository.resolve()):
                    raise ValueError("MR build destination is outside the checkout.")
                destination.parent.mkdir(parents=True, exist_ok=True)
                with (
                    archive.open(name) as source_file,
                    destination.open("wb") as output,
                ):
                    shutil.copyfileobj(source_file, output)
    (directory / "reused-results.json").write_text(
        json.dumps(reused, indent=2) + "\n", encoding="utf-8"
    )
    print(f"Restored {len(reused)} successful MR checks for {candidate}.")


def verify_build_artifact(repository, image, candidate, reused):
    job = "backend:check-and-build" if image == "backend" else "ai:check-and-test"
    expected = candidate
    if job in reused:
        expected = reused[job]["source_commit"]
        if not same_job_inputs(repository, job, expected, candidate):
            raise ValueError(
                "Reused build inputs differ from the deployment candidate."
            )
        record = repository / "dist/deployment/reused-results.json"
        if (
            not record.is_file()
            or json.loads(record.read_text()).get(job) != reused[job]
        ):
            raise ValueError("Reused build has no matching restoration record.")
    marker = repository / BUILD_ARTIFACTS[job][0]
    if marker.read_text(encoding="utf-8").strip() != expected:
        raise ValueError("Verified build artifact belongs to another commit.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=["restore"])
    parser.parse_args()
    restore_results(Path(os.environ["CI_PROJECT_DIR"]), os.environ["CI_COMMIT_SHA"])


if __name__ == "__main__":
    main()
