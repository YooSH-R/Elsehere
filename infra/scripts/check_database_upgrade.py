"""Exercise a backend upgrade and rollback against a disposable PostgreSQL database."""

import argparse
import json
import os
import secrets
import subprocess
import tarfile
import tempfile
import time
import uuid
from pathlib import Path

from deployment_plan import commit_exists
from mr_results import verify_build_artifact

REPOSITORY = Path(__file__).resolve().parents[2]
POSTGRES_IMAGE = "postgres:16-alpine"
BACKEND_RUNTIME = "eclipse-temurin:21-jre-alpine"
APP_PROBE = """
import http.cookiejar
import json
import time
import urllib.error
import urllib.request

client = urllib.request.build_opener(
    urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar())
)

def request(path, payload=None):
    data = json.dumps(payload).encode() if payload is not None else None
    request = urllib.request.Request(
        "http://backend:8080" + path, data,
        {"Content-Type": "application/json", "X-Requested-With": "XMLHttpRequest"},
    )
    with client.open(request, timeout=3) as response:
        return json.load(response)

deadline = time.monotonic() + 120
while time.monotonic() < deadline:
    try:
        if request("/api/health")["status"] == "UP":
            break
    except (urllib.error.URLError, TimeoutError):
        time.sleep(1)
else:
    raise RuntimeError("Backend did not become ready")

guest = request("/api/v1/auth/guest", {"avatarType": "M", "voiceConsent": False})
profile = request("/api/v1/bootstrap")["user"]
assert profile["guest"] and profile["userId"] == guest["user"]["userId"]
assert request("/api/v1/scenes")
print(profile["userId"])
"""


def previous_commit(environment):
    state = json.loads(environment["A305_DEPLOY_STATE"])
    return state["images"]["backend"]["source_commit"]


def build_previous_jar(commit, directory):
    if not commit_exists(REPOSITORY, commit):
        raise ValueError("Previous backend source must be an available full Git SHA.")
    archive = directory / "backend.tar"
    with archive.open("wb") as output:
        subprocess.run(
            ["git", "archive", commit, "backend"],
            cwd=REPOSITORY,
            stdout=output,
            check=True,
        )
    with tarfile.open(archive) as source:
        source.extractall(directory, filter="data")
    backend = directory / "backend"
    wrapper = ["cmd.exe", "/c", "gradlew.bat"] if os.name == "nt" else ["sh", "gradlew"]
    subprocess.run(
        [*wrapper, "--no-daemon", "--max-workers=2", "--build-cache", "bootJar"],
        cwd=backend,
        check=True,
        timeout=420,
    )
    return backend / "build/libs/app.jar"


def docker(*args, **options):
    return subprocess.run(
        ["docker", *map(str, args)], check=True, timeout=200, **options
    )


def check_upgrade(previous_jar, candidate_jar):
    for jar in (previous_jar, candidate_jar):
        if not jar.is_file():
            raise ValueError(f"Backend JAR does not exist: {jar}")
    project = f"a305-db-upgrade-{uuid.uuid4().hex}"
    database = f"{project}-database"
    password = secrets.token_urlsafe(24)
    docker("network", "create", "--internal", project, capture_output=True)
    database_created = False
    try:
        docker(
            "create",
            "--name",
            database,
            "--network",
            project,
            "--network-alias",
            "database",
            "-e",
            "POSTGRES_DB=upgrade_test",
            "-e",
            "POSTGRES_USER=upgrade_test",
            "-e",
            f"POSTGRES_PASSWORD={password}",
            POSTGRES_IMAGE,
            capture_output=True,
        )
        database_created = True
        docker("start", database, capture_output=True)
        for _ in range(40):
            try:
                if query(database, "select 1") == ["1"]:
                    break
            except subprocess.CalledProcessError:
                time.sleep(1)
        else:
            raise RuntimeError("Isolated PostgreSQL did not become ready.")

        old_guest = probe_backend(project, password, previous_jar, "previous")
        history_sql = (
            "select version, script, checksum, success from flyway_schema_history "
            "order by installed_rank"
        )
        previous_history = query(database, history_sql)
        candidate_guest = probe_backend(project, password, candidate_jar, "candidate")
        upgraded_history = query(database, history_sql)
        if (
            not previous_history
            or upgraded_history[: len(previous_history)] != previous_history
        ):
            raise AssertionError(
                "Candidate did not preserve the applied migration history."
            )
        if query(
            database, "select count(*) from flyway_schema_history where not success"
        ) != ["0"]:
            raise AssertionError("The upgraded database contains failed migrations.")
        rollback_guest = probe_backend(project, password, previous_jar, "rollback")
        guest_ids = query(database, "select id from users order by id")
        if guest_ids != list(
            map(str, sorted((old_guest, candidate_guest, rollback_guest)))
        ):
            raise AssertionError(
                "Synthetic accounts did not survive the upgrade and rollback."
            )
        if query(database, history_sql) != upgraded_history:
            raise AssertionError(
                "The previous backend changed the upgraded migration history."
            )
        print("Database upgrade and previous backend read/write checks passed.")
    finally:
        try:
            if database_created:
                docker("rm", "-f", "-v", database, capture_output=True)
        finally:
            docker("network", "rm", project, capture_output=True)


def query(database, sql):
    return docker(
        "exec",
        database,
        "psql",
        "-XqAt",
        "-v",
        "ON_ERROR_STOP=1",
        "-U",
        "upgrade_test",
        "-d",
        "upgrade_test",
        "-c",
        sql,
        capture_output=True,
        text=True,
    ).stdout.splitlines()


def probe_backend(project, password, jar, phase):
    print(f"Starting {phase} backend.", flush=True)
    container = f"{project}-{phase}"
    docker(
        "create",
        "--name",
        container,
        "--network",
        project,
        "--network-alias",
        "backend",
        "-e",
        "DB_URL=jdbc:postgresql://database:5432/upgrade_test",
        "-e",
        "DB_USERNAME=upgrade_test",
        "-e",
        f"DB_PASSWORD={password}",
        "-e",
        "JPA_DDL_AUTO=validate",
        "-e",
        "AI_WORKER_TOKEN=ci-only",
        BACKEND_RUNTIME,
        "java",
        "-Xmx512m",
        "-jar",
        "/app.jar",
        capture_output=True,
    )
    try:
        docker("cp", jar.resolve(), f"{container}:/app.jar")
        docker("start", container, capture_output=True)
        probe = docker(
            "run",
            "--rm",
            "-i",
            "--network",
            project,
            "python:3.12-alpine",
            "python",
            "-",
            input=APP_PROBE,
            capture_output=True,
            text=True,
        )
        return int(probe.stdout.strip())
    except subprocess.CalledProcessError as error:
        if error.stderr:
            print(error.stderr, flush=True)
        docker("logs", "--tail", "80", container)
        raise
    finally:
        docker("rm", "-f", "-v", container, capture_output=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    previous = parser.add_mutually_exclusive_group()
    previous.add_argument("--previous-commit")
    previous.add_argument("--previous-jar", type=Path)
    parser.add_argument(
        "--candidate-jar", type=Path, default=REPOSITORY / "backend/build/libs/app.jar"
    )
    args = parser.parse_args()
    if os.environ.get("A305_DEPLOY_ENVIRONMENT") == "prod":
        verify_build_artifact(REPOSITORY, "backend", os.environ["CI_COMMIT_SHA"], {})
    if args.previous_jar:
        check_upgrade(args.previous_jar, args.candidate_jar)
    else:
        if not args.previous_commit and "A305_DEPLOY_STATE" not in os.environ:
            parser.error("Provide --previous-commit or --previous-jar for a local run.")
        commit = args.previous_commit or previous_commit(os.environ)
        with tempfile.TemporaryDirectory(prefix="a305-backend-") as work:
            check_upgrade(build_previous_jar(commit, Path(work)), args.candidate_jar)


if __name__ == "__main__":
    main()
