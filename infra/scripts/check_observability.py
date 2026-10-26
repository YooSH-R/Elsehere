"""Validate observability configuration in disposable, private Docker containers."""

import base64
import json
import re
import subprocess
import tempfile
import time
import uuid
from pathlib import Path

REPOSITORY = Path(__file__).resolve().parents[2]
MONITORING = REPOSITORY / "infra" / "monitoring"


def docker(*arguments, check=True):
    result = subprocess.run(
        ["docker", *map(str, arguments)],
        capture_output=True,
        text=True,
        encoding="utf-8",
        check=False,
    )
    if check and result.returncode:
        raise RuntimeError(f"Docker command failed: {arguments[0]}\n{result.stderr}")
    return result


def copy_to(container, source, destination):
    docker("cp", source, f"{container}:{destination}")


def http(client, url, headers=()):
    arguments = ["exec", client, "wget", "-S", "-O", "-", "-T", "3"]
    for header in headers:
        arguments.extend(["--header", header])
    return docker(*arguments, url, check=False)


def wait_for_http(client, url):
    for _ in range(60):
        result = http(client, url)
        if result.returncode == 0:
            return
        time.sleep(1)
    raise RuntimeError(f"Service did not become ready: {url}")


def check_nginx(container):
    docker("exec", container, "nginx", "-t")
    supplied_id = "1234567890abcdef1234567890abcdef"
    result = http(
        container,
        "http://127.0.0.1/dev/api/private-path?token=private-query",
        [f"X-Request-ID: {supplied_id}", "Authorization: Bearer private-header"],
    )
    assert result.returncode == 0, result.stderr
    request_id = json.loads(result.stdout)["request_id"]
    assert re.fullmatch("[a-f0-9]{32}", request_id)
    assert request_id != supplied_id
    assert f"X-Request-ID: {request_id}" in result.stderr
    assert "500" in http(container, "http://127.0.0.1/api/failure").stderr
    assert (
        "502"
        in http(
            container, "http://127.0.0.1/unavailable/private-path?token=private-query"
        ).stderr
    )
    container_logs = docker("logs", container)
    logs = container_logs.stdout + container_logs.stderr
    assert (
        "private-path" not in logs
        and "private-query" not in logs
        and "private-header" not in logs
    )
    events = [json.loads(line) for line in logs.splitlines() if line.startswith("{")]
    assert any(
        event["request_id"] == request_id and event["environment"] == "dev"
        for event in events
    )
    assert {200, 500, 502}.issubset({event["status"] for event in events})
    print(
        "Nginx: generated/forwarded IDs, 200/500/502 summaries and sensitive-input exclusion passed.",
        flush=True,
    )


def check_prometheus(container):
    docker(
        "exec",
        container,
        "/bin/promtool",
        "check",
        "config",
        "/etc/prometheus/prometheus.yml",
    )
    for dashboard_file in (MONITORING / "grafana/dashboards").glob("*.json"):
        dashboard = json.loads(dashboard_file.read_text(encoding="utf-8"))
        for panel in dashboard["panels"]:
            for target in panel["targets"]:
                expression = (
                    target["expr"]
                    .replace("$__rate_interval", "1m")
                    .replace("$__range", "1h")
                    .replace("$environment", ".*")
                )
                docker(
                    "exec",
                    container,
                    "/bin/promtool",
                    "--experimental",
                    "promql",
                    "format",
                    expression,
                )
    print(
        "Prometheus: configuration and all dashboard queries passed official validation.",
        flush=True,
    )


def check_grafana(client, base_url="http://grafana:3000"):
    url = base_url + "/api/dashboards/uid/a305-api"
    assert "401" in http(client, url).stderr
    credentials = base64.b64encode(b"admin:observability-fixture-password").decode()
    result = http(client, url, [f"Authorization: Basic {credentials}"])
    assert result.returncode == 0, result.stderr
    dashboard = json.loads(result.stdout)["dashboard"]
    assert dashboard["uid"] == "a305-api" and len(dashboard["panels"]) == 6
    datasource = http(
        client,
        base_url + "/api/datasources/uid/a305-prometheus",
        [f"Authorization: Basic {credentials}"],
    )
    assert (
        datasource.returncode == 0
        and json.loads(datasource.stdout)["url"] == "http://prometheus:9090"
    )
    availability = http(
        client,
        base_url + "/api/dashboards/uid/a305-availability",
        [f"Authorization: Basic {credentials}"],
    )
    assert availability.returncode == 0
    assert len(json.loads(availability.stdout)["dashboard"]["panels"]) == 3
    print(
        "Grafana: provisioned datasource/dashboard and authenticated access passed.",
        flush=True,
    )


def main():
    prefix = f"a305-observability-check-{uuid.uuid4().hex[:8]}"
    containers = [
        f"{prefix}-{service}"
        for service in ("nginx", "prometheus", "grafana", "inner", "admin")
    ]
    nginx, prometheus, grafana, inner, admin = containers
    docker("network", "create", "--internal", prefix)
    try:
        docker(
            "create",
            "--name",
            nginx,
            "--network",
            prefix,
            "--memory",
            "128m",
            "--cpus",
            "0.25",
            "nginx:1.30.6-alpine",
        )
        copy_to(
            nginx,
            REPOSITORY / "infra/nginx/observability-http.conf",
            "/etc/nginx/conf.d/00-observability.conf",
        )
        copy_to(
            nginx, MONITORING / "tests/nginx.conf", "/etc/nginx/conf.d/default.conf"
        )
        copy_to(
            nginx,
            REPOSITORY / "infra/nginx/observability-server.conf",
            "/etc/nginx/a305-observability-server.conf",
        )
        docker("start", nginx)
        wait_for_http(nginx, "http://127.0.0.1/")
        check_nginx(nginx)

        docker(
            "create",
            "--name",
            inner,
            "--network",
            prefix,
            "--memory",
            "64m",
            "--cpus",
            "0.25",
            "nginx:1.30.6-alpine",
        )
        copy_to(
            inner,
            MONITORING / "tests/nginx-inner.conf",
            "/etc/nginx/conf.d/default.conf",
        )
        copy_to(
            inner,
            REPOSITORY / "infra/nginx/observability-web.conf",
            "/etc/nginx/conf.d/00-observability.conf",
        )
        docker("start", inner)
        wait_for_http(inner, "http://127.0.0.1/")
        result = http(
            inner,
            "http://127.0.0.1/private-inner-path?token=private-inner-query",
            ["X-Request-ID: inner-fixture-id"],
        )
        assert json.loads(result.stdout)["request_id"] == "inner-fixture-id"
        inner_logs = docker("logs", inner)
        assert "private-inner" not in inner_logs.stdout + inner_logs.stderr
        print(
            "Inner Nginx: request header preserved and raw URL logging disabled.",
            flush=True,
        )

        docker(
            "create",
            "--name",
            prometheus,
            "--network",
            prefix,
            "--network-alias",
            "prometheus",
            "--memory",
            "512m",
            "--cpus",
            "0.50",
            "prom/prometheus:v3.13.5",
            "--config.file=/etc/prometheus/prometheus.yml",
        )
        copy_to(
            prometheus, MONITORING / "prometheus.yml", "/etc/prometheus/prometheus.yml"
        )
        copy_to(prometheus, MONITORING / "alerts.yml", "/etc/prometheus/alerts.yml")
        copy_to(prometheus, MONITORING / "targets", "/etc/prometheus/targets")
        with tempfile.TemporaryDirectory() as temporary_directory:
            runtime = Path(temporary_directory) / "run"
            secrets = runtime / "secrets"
            secrets.mkdir(parents=True)
            for environment in ("dev", "prod"):
                (secrets / f"{environment}-monitoring-password").write_text(
                    "fixture-only-password", encoding="utf-8"
                )
            copy_to(prometheus, runtime, "/")
        docker("start", prometheus)
        wait_for_http(nginx, "http://prometheus:9090/-/ready")
        check_prometheus(prometheus)

        docker(
            "create",
            "--name",
            grafana,
            "--network",
            prefix,
            "--network-alias",
            "grafana",
            "--memory",
            "512m",
            "--cpus",
            "0.50",
            "--env",
            "GF_SECURITY_ADMIN_PASSWORD=observability-fixture-password",
            "--env",
            "GF_AUTH_ANONYMOUS_ENABLED=false",
            "--env",
            "GF_ANALYTICS_REPORTING_ENABLED=false",
            "grafana/grafana:13.2.4",
        )
        copy_to(
            grafana,
            str(MONITORING / "grafana/provisioning") + "/.",
            "/etc/grafana/provisioning/",
        )
        copy_to(
            grafana, MONITORING / "grafana/dashboards", "/var/lib/grafana/dashboards"
        )
        docker("start", grafana)
        wait_for_http(nginx, "http://grafana:3000/api/health")
        check_grafana(nginx)

        docker(
            "create",
            "--name",
            admin,
            "--network",
            prefix,
            "--network-alias",
            "admin",
            "--memory",
            "64m",
            "--cpus",
            "0.25",
            "nginx:1.30.6-alpine",
        )
        copy_to(admin, MONITORING / "admin-ui.conf", "/etc/nginx/conf.d/default.conf")
        docker("start", admin)
        wait_for_http(nginx, "http://admin:3000/api/health")
        check_grafana(nginx, "http://admin:3000")
        print(
            "Admin proxy: Grafana authentication and provisioned dashboards passed through the UI route.",
            flush=True,
        )
    finally:
        for container in containers:
            docker("rm", "-f", "-v", container, check=False)
        docker("network", "rm", prefix, check=False)


if __name__ == "__main__":
    main()
