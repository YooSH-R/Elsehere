"""Restore WebGL builds from this project's temporary generic packages."""

import argparse
import hashlib
import json
import os
import re
import shutil
import sys
import tarfile
import tempfile
from pathlib import Path
from urllib.error import HTTPError
from urllib.parse import urlencode
from urllib.request import Request, urlopen


def package_metadata(repository, candidate):
    metadata = json.loads(
        (repository / "dist/webgl-package.json").read_text(encoding="utf-8-sig")
    )
    if (
        metadata.get("schema_version") != 1
        or metadata.get("commit") != candidate
        or metadata.get("pipeline_id") != os.environ["CI_PIPELINE_ID"]
        or not all(
            re.fullmatch(r"[1-9][0-9]*", str(metadata.get(field, "")))
            for field in ("pipeline_id", "job_id")
        )
    ):
        raise ValueError("WebGL package must belong to this candidate and pipeline.")
    return metadata


def package_api():
    return (
        f"{os.environ['CI_API_V4_URL']}/projects/{os.environ['CI_PROJECT_ID']}/packages"
    )


def package_request(url, method="GET"):
    request = Request(url, method=method)
    # Object storage redirects must not receive the CI credential.
    request.add_unredirected_header("JOB-TOKEN", os.environ["CI_JOB_TOKEN"])
    return request


def restore_webgl(repository, candidate):
    metadata = package_metadata(repository, candidate)
    version = f"{metadata['pipeline_id']}-{metadata['job_id']}"
    request = package_request(f"{package_api()}/generic/a305-webgl/{version}/webgl.tar")
    with tempfile.TemporaryDirectory() as directory:
        archive = Path(directory) / "webgl.tar"
        with urlopen(request, timeout=300) as response, archive.open("wb") as output:
            shutil.copyfileobj(response, output)
        with archive.open("rb") as source:
            checksum = hashlib.file_digest(source, "sha256").hexdigest()
        if archive.stat().st_size != metadata["size"] or checksum != metadata["sha256"]:
            raise ValueError("WebGL package size or checksum does not match.")
        with tarfile.open(archive) as bundle:
            bundle.extractall(repository / "dist/webgl", filter="data")


def cleanup_webgl(repository, candidate):
    if not (repository / "dist/webgl-package.json").exists():
        return
    metadata = package_metadata(repository, candidate)
    version = f"{metadata['pipeline_id']}-{metadata['job_id']}"
    query = urlencode(
        {
            "package_type": "generic",
            "package_name": "a305-webgl",
            "package_version": version,
            "per_page": 100,
        }
    )
    with urlopen(package_request(f"{package_api()}?{query}"), timeout=60) as response:
        packages = json.load(response)
    for package in packages:
        if (package["name"], package["version"], package["package_type"]) != (
            "a305-webgl",
            version,
            "generic",
        ):
            continue
        request = package_request(f"{package_api()}/{int(package['id'])}", "DELETE")
        try:
            with urlopen(request, timeout=60):
                pass
        except HTTPError as error:
            if error.code != 404:
                raise
    print(f"Cleaned temporary WebGL package {version}.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=["cleanup"])
    parser.parse_args()
    try:
        cleanup_webgl(
            Path(os.environ.get("CI_PROJECT_DIR", ".")), os.environ["CI_COMMIT_SHA"]
        )
    except HTTPError as error:
        if error.code != 403:
            raise
        print(
            "Package cleanup requires Maintainer access; retry this cleanup job as a Maintainer."
        )
        return 77
    return 0


if __name__ == "__main__":
    sys.exit(main())
