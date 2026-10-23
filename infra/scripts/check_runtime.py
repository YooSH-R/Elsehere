"""Build and probe the app Compose in an isolated, disposable Docker project."""

import argparse
import gzip
import json
import os
import re
import subprocess
import uuid
from pathlib import Path


def create_http_fixture(directory):
    (directory / "Build").mkdir(parents=True, exist_ok=True)
    (directory / "index.html").write_text(
        "<html><body>HTTP configuration test</body></html>\n", encoding="utf-8"
    )
    (directory / "version.json").write_text(
        json.dumps(
            {"service": "web", "commit": "runtime-check", "build_type": "http-fixture"}
        ),
        encoding="utf-8",
    )
    for suffix in ["wasm", "js", "data"]:
        (directory / "Build" / f"check.{suffix}.gz").write_bytes(
            gzip.compress(b"HTTP fixture")
        )


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", default=f"a305-check-{uuid.uuid4().hex[:8]}")
    parser.add_argument("--tag", default="runtime-check")
    parser.add_argument("--port", default="18081")
    arguments = parser.parse_args()
    if not re.fullmatch(r"a305-check-[a-z0-9-]+", arguments.project):
        parser.error("Only disposable a305-check-* projects are accepted.")
    if not re.fullmatch(r"[a-z0-9][a-z0-9.-]*", arguments.tag):
        parser.error("Invalid runtime check image tag.")

    repository = Path(__file__).resolve().parents[2]
    create_http_fixture(repository / "dist/runtime-webgl")
    images = {
        service: f"a305/{service}:{arguments.tag}"
        for service in ["backend", "ai", "web"]
    }

    def docker(*command, **options):
        return subprocess.run(
            ["docker", *command], cwd=repository, check=True, **options
        )

    for service, dockerfile in [
        ("backend", "backend.Dockerfile"),
        ("ai", "ai-worker.Dockerfile"),
        ("web", "web.Dockerfile"),
    ]:
        command = ["build", "-t", images[service], "-f", f"infra/docker/{dockerfile}"]
        if service == "web":
            command.extend(["--build-arg", "WEBGL_DIRECTORY=dist/runtime-webgl"])
        docker(*command, ".")

    environment = {
        **os.environ,
        "BACKEND_HOST_PORT": arguments.port,
        "BACKEND_IMAGE": images["backend"],
        "AI_IMAGE": images["ai"],
        "WEB_IMAGE": images["web"],
        "DB_NAME": "a305_check",
        "DB_USERNAME": "a305_check",
        "DB_PASSWORD": "ci-only-database-password",
        "JPA_DDL_AUTO": "update",
        "AI_WORKER_TOKEN": "ci-only-internal-token",
        "LLM_PROFILE": "gms-luna",
        "GMS_KEY": "ci-only-provider-key",
        "GROQ_API_KEY": "ci-only-provider-key",
    }
    compose = [
        "compose",
        "--project-name",
        arguments.project,
        "--file",
        "infra/compose/compose.app.yaml",
        "--file",
        "infra/compose/compose.runtime-test.yaml",
    ]
    try:
        for example, port in [("dev", "8081"), ("prod", "8080")]:
            docker(
                "compose",
                "--env-file",
                f"infra/compose/deploy.{example}.env.example",
                "--file",
                "infra/compose/compose.app.yaml",
                "config",
                "--quiet",
                env={
                    **environment,
                    "BACKEND_HOST_PORT": port,
                    "JPA_DDL_AUTO": "update" if example == "dev" else "validate",
                },
            )
        docker(*compose, "config", "--quiet", env=environment)
        docker(
            *compose,
            "up",
            "--detach",
            "--wait",
            "--wait-timeout",
            "180",
            env=environment,
        )
        docker(
            *compose,
            "exec",
            "--no-TTY",
            "ai-worker",
            "python",
            "-",
            env=environment,
            input=(repository / "infra/scripts/runtime_probe.py").read_text(
                encoding="utf-8"
            ),
            text=True,
        )
    finally:
        # This name is restricted above; production and developer data are never targeted.
        docker(*compose, "down", "--volumes", "--remove-orphans", env=environment)


if __name__ == "__main__":
    main()
