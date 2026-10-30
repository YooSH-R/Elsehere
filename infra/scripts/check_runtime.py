"""Build and probe the app Compose in an isolated, disposable Docker project."""

import argparse
import gzip
import json
import os
import re
import subprocess
import uuid
from pathlib import Path

from docker_build import build_image


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

    # Reproduce restrictive archive permissions; Nginx workers must still read the image.
    directory.chmod(0o700)
    for entry in directory.rglob("*"):
        entry.chmod(0o700 if entry.is_dir() else 0o600)


def check_backend_runtime(docker, compose, environment):
    docker(
        *compose,
        "exec",
        "--no-TTY",
        "backend",
        "sh",
        "-ec",
        'curl --fail --silent --show-error "$AI_WORKER_BASE_URL/health" >/dev/null; '
        'probe=$(mktemp "$NPC_AUDIO_DIR/.ci-write-check.XXXXXX"); '
        'printf audio-storage-check > "$probe"; rm "$probe"',
        env=environment,
    )
    container_id = docker(
        *compose,
        "ps",
        "--quiet",
        "backend",
        env=environment,
        capture_output=True,
        text=True,
    ).stdout.strip()
    mounts = json.loads(
        docker(
            "inspect",
            "--format",
            "{{json .Mounts}}",
            container_id,
            capture_output=True,
            text=True,
        ).stdout
    )
    if not any(
        mount["Type"] == "volume" and mount["Destination"] == "/app/data/npc-audio"
        for mount in mounts
    ):
        raise AssertionError("NPC audio must use a writable persistent volume.")
    print("Backend: internal AI worker health and writable NPC audio volume passed.")


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
        build_arguments = (
            {"WEBGL_DIRECTORY": "dist/runtime-webgl"} if service == "web" else None
        )
        build_image(
            docker,
            repository,
            service,
            images[service],
            f"infra/docker/{dockerfile}",
            build_arguments=build_arguments,
            cache_directory=os.environ.get("A305_DOCKER_CACHE_DIR"),
        )

    environment = {
        **os.environ,
        "BACKEND_HOST_PORT": arguments.port,
        "BACKEND_IMAGE": images["backend"],
        "AI_IMAGE": images["ai"],
        "WEB_IMAGE": images["web"],
        "DB_NAME": "a305_check",
        "DB_USERNAME": "a305_check",
        "DB_PASSWORD": "ci-only-database-password",
        "JPA_DDL_AUTO": "validate",
        "GOOGLE_CLIENT_ID": "ci-only-client.apps.googleusercontent.com",
        "GOOGLE_CLIENT_SECRET": "ci-only-google-secret",
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
                env={**environment, "BACKEND_HOST_PORT": port},
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
        check_backend_runtime(docker, compose, environment)
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
