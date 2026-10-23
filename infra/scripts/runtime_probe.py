"""Probe application containers from inside the isolated AI worker container."""

import gzip
import json
import os
from urllib.error import HTTPError
from urllib.request import Request, urlopen


def fetch(url, expected_status=200, headers=None):
    request = Request(url, headers=headers or {})
    try:
        response = urlopen(request, timeout=10)
    except HTTPError as error:
        response = error
    with response:
        if response.status != expected_status:
            raise AssertionError(
                f"{url}: expected {expected_status}, got {response.status}"
            )
        return response.read(), response.headers


def main():
    body, _ = fetch("http://web/api/health")
    if json.loads(body) != {"status": "UP"}:
        raise AssertionError(
            f"Unexpected application/database health response: {body.decode('utf-8')}"
        )
    fetch("http://web/api/private-resource", expected_status=401)

    body, _ = fetch("http://127.0.0.1:8020/health")
    if json.loads(body).get("ok") is not True:
        raise AssertionError("AI worker is not ready.")
    fetch("http://127.0.0.1:8020/v1/judge", expected_status=401)
    fetch(
        "http://127.0.0.1:8020/v1/judge",
        expected_status=405,
        headers={"X-Internal-Token": os.environ["AI_WORKER_TOKEN"]},
    )

    body, headers = fetch("http://web/")
    if b"<html" not in body.lower() or "text/html" not in headers["Content-Type"]:
        raise AssertionError("Web index is not an HTML document.")
    body, _ = fetch("http://web/version.json")
    version = json.loads(body)
    if version.get("service") != "web":
        raise AssertionError("Web version metadata is missing.")

    # Small fixtures verify HTTP compression/MIME handling, not Unity player behavior.
    if version.get("build_type") == "http-fixture":
        for suffix, content_type in [
            ("wasm", "application/wasm"),
            ("js", "application/javascript"),
            ("data", "application/octet-stream"),
        ]:
            body, headers = fetch(f"http://web/Build/check.{suffix}.gz")
            if headers["Content-Encoding"] != "gzip":
                raise AssertionError(f"Missing gzip header for {suffix}.")
            if headers["Content-Type"] != content_type:
                raise AssertionError(f"Incorrect MIME type for {suffix}.")
            if not gzip.decompress(body):
                raise AssertionError(f"Empty decompressed {suffix} response.")

    print(
        "Runtime probes passed: backend/database, AI authentication, web and response headers."
    )


if __name__ == "__main__":
    main()
