"""Check alert firing/recovery with a local receiver and no real chat destination."""

import json
import tempfile
import time
import uuid
from datetime import UTC, datetime, timedelta
from pathlib import Path

from check_observability import MONITORING, copy_to, docker, http, wait_for_http


def create(container, network, alias, image, memory, *arguments):
    docker(
        "create",
        "--name",
        container,
        "--network",
        network,
        "--network-alias",
        alias,
        "--memory",
        memory,
        "--cpus",
        "0.25",
        image,
        *arguments,
    )


def post_alert(client, ends_at):
    payload = json.dumps(
        [
            {
                "labels": {
                    "alertname": "FixtureOnly",
                    "environment": "test",
                    "severity": "warning",
                },
                "annotations": {
                    "summary": "Isolated notification fixture",
                    "description": "No real Mattermost channel is contacted.",
                },
                "startsAt": (datetime.now(UTC) - timedelta(minutes=1)).isoformat(),
                "endsAt": ends_at.isoformat(),
            }
        ]
    )
    result = docker(
        "exec",
        client,
        "wget",
        "-q",
        "-O",
        "-",
        "-T",
        "5",
        "--header",
        "Content-Type: application/json",
        "--post-data",
        payload,
        "http://alertmanager:9093/api/v2/alerts",
        check=False,
    )
    assert result.returncode == 0, result.stderr


def wait_for_notification(client, status):
    for _ in range(30):
        messages = json.loads(http(client, "http://receiver:8085/").stdout)
        for message in messages:
            assert message["channel"] == "fixture-only"
            if status in json.dumps(message):
                return
        time.sleep(1)
    raise AssertionError(f"Missing fixture notification: {status}")


def main():
    prefix = f"a305-alert-check-{uuid.uuid4().hex[:8]}"
    containers = [
        f"{prefix}-{name}"
        for name in ("receiver", "client", "blackbox", "alertmanager", "promtool")
    ]
    receiver, client, blackbox, alertmanager, promtool = containers
    docker("network", "create", "--internal", prefix)
    try:
        create(
            receiver,
            prefix,
            "receiver",
            "python:3.13-alpine",
            "64m",
            "python",
            "/receiver.py",
        )
        copy_to(receiver, MONITORING / "tests/notification_receiver.py", "/receiver.py")
        docker("start", receiver)
        create(client, prefix, "client", "nginx:1.30.6-alpine", "64m")
        docker("start", client)
        wait_for_http(client, "http://receiver:8085/")

        create(promtool, prefix, "promtool", "prom/prometheus:v3.13.5", "256m")
        copy_to(promtool, MONITORING / "alerts.yml", "/etc/prometheus/alerts.yml")
        copy_to(
            promtool,
            MONITORING / "tests/alerts.test.yml",
            "/etc/prometheus/alerts.test.yml",
        )
        docker("start", promtool)
        docker(
            "exec",
            promtool,
            "/bin/promtool",
            "test",
            "rules",
            "/etc/prometheus/alerts.test.yml",
        )
        print(
            "Alert rules: pending, firing, recovery, sparse traffic and health exclusion passed.",
            flush=True,
        )

        with tempfile.TemporaryDirectory() as temporary_directory:
            runtime = Path(temporary_directory)
            blackbox_config = runtime / "blackbox.yml"
            blackbox_config.write_text(
                (MONITORING / "blackbox.yml").read_text(encoding="utf-8")
                + "\n  http_fixture:\n    prober: http\n    timeout: 3s\n    http:\n      preferred_ip_protocol: ip4\n",
                encoding="utf-8",
            )
            create(
                blackbox,
                prefix,
                "blackbox",
                "prom/blackbox-exporter:v0.29.1",
                "64m",
                "--config.file=/config.yml",
            )
            copy_to(blackbox, blackbox_config, "/config.yml")
            docker("start", blackbox)
            wait_for_http(client, "http://blackbox:9115/-/healthy")
            result = http(
                client,
                "http://blackbox:9115/probe?module=http_fixture&target=http%3A%2F%2Fclient%2F",
            )
            assert "probe_success 1" in result.stdout
            result = http(
                client,
                "http://blackbox:9115/probe?module=https_2xx&target=http%3A%2F%2Fclient%2F",
            )
            assert "probe_success 0" in result.stdout
            result = http(
                client,
                "http://blackbox:9115/probe?module=http_fixture&target=http%3A%2F%2Fclient%2Fmissing",
            )
            assert "probe_success 0" in result.stdout
            print(
                "Blackbox: healthy HTTP fixture, failure and strict HTTPS enforcement passed.",
                flush=True,
            )

            create(
                alertmanager,
                prefix,
                "alertmanager",
                "prom/alertmanager:v0.34.1",
                "128m",
                "--config.file=/config.yml",
                "--cluster.listen-address=",
            )
            pending_config = (MONITORING / "alertmanager.yml").read_text(
                encoding="utf-8"
            )
            assert (
                "mattermost_configs" not in pending_config
                and "webhook" not in pending_config
            )
            copy_to(alertmanager, MONITORING / "alertmanager.yml", "/pending.yml")
            notification_config = (
                MONITORING / "alertmanager.mattermost.yml.example"
            ).read_text(encoding="utf-8")
            notification_config = (
                notification_config.replace("REQUIRES_USER_APPROVAL", "fixture-only")
                .replace("group_wait: 30s", "group_wait: 1s")
                .replace("group_interval: 5m", "group_interval: 1s")
            )
            (runtime / "config.yml").write_text(notification_config, encoding="utf-8")
            secrets = runtime / "run/secrets"
            secrets.mkdir(parents=True)
            (secrets / "mattermost-webhook-url").write_text(
                "http://receiver:8085/hook", encoding="utf-8"
            )
            copy_to(alertmanager, runtime / "config.yml", "/config.yml")
            copy_to(alertmanager, runtime / "run", "/")
            docker("start", alertmanager)
            wait_for_http(client, "http://alertmanager:9093/-/ready")
            docker("exec", alertmanager, "/bin/amtool", "check-config", "/pending.yml")
            docker("exec", alertmanager, "/bin/amtool", "check-config", "/config.yml")
            post_alert(client, datetime.now(UTC) + timedelta(minutes=5))
            wait_for_notification(client, "FIRING")
            post_alert(client, datetime.now(UTC) - timedelta(seconds=1))
            wait_for_notification(client, "RESOLVED")
            print(
                "Alertmanager: native Mattermost payload firing/recovery reached only the isolated fixture receiver.",
                flush=True,
            )
    finally:
        for container in containers:
            docker("rm", "-f", "-v", container, check=False)
        docker("network", "rm", prefix, check=False)


if __name__ == "__main__":
    main()
