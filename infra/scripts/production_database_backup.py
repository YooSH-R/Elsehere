"""Verify a fresh encrypted production backup before starting a migration."""

import os
import stat
import subprocess
import tempfile
import time
import uuid
from pathlib import Path

TRANSFER_TIMEOUT = 180
RESTORE_DATABASE = "a305_restore"
READINESS_SQL = """
SELECT count(*) > 0 AND bool_and(success) FROM public.flyway_schema_history;
SELECT count(*) FROM public.users;
SELECT count(*) FROM public.conversations;
"""


def transfer(source, destination, failure, *, output=subprocess.DEVNULL):
    # Check both ends. A consumer can exit successfully on partial output.
    # Neither command's diagnostics belong in the deployment log.
    with subprocess.Popen(
        source,
        stdin=subprocess.DEVNULL,
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,
    ) as producer:
        try:
            result = subprocess.run(
                destination,
                stdin=producer.stdout,
                stdout=output,
                stderr=subprocess.DEVNULL,
                timeout=TRANSFER_TIMEOUT,
                check=False,
            )
            producer.stdout.close()
            if producer.wait(timeout=10) or result.returncode:
                raise RuntimeError(failure)
        except subprocess.TimeoutExpired:
            raise RuntimeError(f"{failure} Timed out.") from None
        finally:
            producer.stdout.close()
            if producer.poll() is None:
                producer.kill()


def restore_backup(
    docker, docker_command, image_id, container, archive, certificate, key
):
    # Use the exact PostgreSQL image that produced the dump. This container has
    # no production mounts, published ports, network access or retained logs.
    container_id = docker(
        "create",
        "--name",
        container,
        "--pull",
        "never",
        "--label",
        "org.elsehere.purpose=production-backup-restore",
        "--network",
        "none",
        "--log-driver",
        "none",
        "--cpus",
        "0.5",
        "--memory",
        "512m",
        "--env",
        "POSTGRES_HOST_AUTH_METHOD=trust",
        "--env",
        f"POSTGRES_DB={RESTORE_DATABASE}",
        image_id,
    ).strip()
    try:
        docker("start", container_id, timeout=30)
        deadline = time.monotonic() + 45
        while True:
            try:
                # The image's initialization server only listens on a socket.
                docker(
                    "exec",
                    container_id,
                    "pg_isready",
                    "-h",
                    "127.0.0.1",
                    "-U",
                    "postgres",
                    "-d",
                    RESTORE_DATABASE,
                    "-q",
                    timeout=5,
                )
                break
            except RuntimeError:
                if time.monotonic() >= deadline:
                    raise RuntimeError(
                        "Isolated backup database did not start."
                    ) from None
                time.sleep(1)
        transfer(
            [
                "openssl",
                "cms",
                "-decrypt",
                "-binary",
                "-inform",
                "DER",
                "-recip",
                str(certificate),
                "-inkey",
                str(key),
                "-passin",
                "pass:",
                "-in",
                str(archive),
            ],
            [
                *docker_command,
                "exec",
                "-i",
                container_id,
                "pg_restore",
                "--single-transaction",
                "--exit-on-error",
                "--no-owner",
                "--no-privileges",
                "-U",
                "postgres",
                "-d",
                RESTORE_DATABASE,
            ],
            "Could not decrypt and restore the production backup.",
        )
        checks = docker(
            "exec",
            container_id,
            "psql",
            "-XAt",
            "-U",
            "postgres",
            "-d",
            RESTORE_DATABASE,
            "-v",
            "ON_ERROR_STOP=1",
            "-c",
            READINESS_SQL,
            timeout=30,
        ).splitlines()
        if len(checks) != 3 or checks[0] != "t":
            raise RuntimeError(
                "Restored production database did not pass sanity checks."
            )
    finally:
        docker("rm", "--force", "--volumes", container_id, timeout=30)


def verify_backup(config, docker, database, pipeline_id):
    settings = config.get("database_backup") or {}
    if not all(settings.get(name) for name in ("directory", "certificate", "key")):
        raise ValueError("Production database backup verification is not configured.")
    directory = Path(settings["directory"])
    certificate = Path(settings["certificate"])
    key = Path(settings["key"])
    if not directory.is_dir() or not certificate.is_file() or not key.is_file():
        raise ValueError("Production backup directory, certificate or key is missing.")
    if os.name != "nt":
        for restricted_path in (directory, key):
            permissions = restricted_path.stat()
            if (
                permissions.st_uid != os.geteuid()
                or stat.S_IMODE(permissions.st_mode) & 0o077
            ):
                raise ValueError("Production backup directory and key must be private.")

    docker_command = config.get("docker_command", ["sudo", "-n", "docker"])
    nonce = uuid.uuid4().hex
    timestamp = time.strftime("%Y%m%dT%H%M%SZ", time.gmtime())
    filename = f"before-main-{pipeline_id}-{timestamp}-{nonce[:8]}.dump.cms"
    with tempfile.TemporaryDirectory(prefix=".pending-", dir=directory) as pending:
        archive = Path(pending) / filename
        with archive.open("xb") as encrypted:
            archive.chmod(0o600)
            transfer(
                [
                    *docker_command,
                    "exec",
                    database["container_id"],
                    "sh",
                    "-ec",
                    (
                        "exec pg_dump --format=custom --no-password --lock-wait-timeout=10s "
                        '-U "$POSTGRES_USER" -d "$POSTGRES_DB"'
                    ),
                ],
                [
                    "openssl",
                    "cms",
                    "-encrypt",
                    "-binary",
                    "-stream",
                    "-aes-256-gcm",
                    "-outform",
                    "DER",
                    str(certificate),
                ],
                "Could not create the encrypted production database backup.",
                output=encrypted,
            )
        restore_backup(
            docker,
            docker_command,
            database["id"],
            f"a305-prod-restore-{nonce}",
            archive,
            certificate,
            key,
        )
        archive.replace(directory / filename)
    return filename
