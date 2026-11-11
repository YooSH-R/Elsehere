"""Expose this project's runner inventory and active jobs as Prometheus metrics."""

import json
import os
import sys
import time
from datetime import datetime
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path
from urllib.error import URLError
from urllib.parse import quote, urlencode, urlsplit
from urllib.request import HTTPRedirectHandler, Request, build_opener

ACTIVE_STATUSES = {"running", "preparing", "canceling"}
WAITING_STATUSES = {"pending", "waiting_for_resource"}
UNASSIGNED_RUNNER = {"id": 0, "description": "미배정", "status": "unknown"}


class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, request, response, code, message, headers, target):
        # A login redirect must not receive the API credential.
        return None


class GitLabClient:
    def __init__(self, base_url, project, token_file):
        address = urlsplit(base_url)
        if (
            address.scheme != "https"
            or not address.hostname
            or address.username
            or address.password
        ):
            raise ValueError("GitLab requires an HTTPS address without credentials")
        self.project_url = (
            f"{base_url.rstrip('/')}/api/v4/projects/{quote(str(project), safe='')}"
        )
        self.token_file = Path(token_file)
        self.opener = build_opener(NoRedirect())

    def get_list(self, resource, parameters=(), *, deadline=None):
        deadline = deadline if deadline is not None else time.monotonic() + 10
        token = self.token_file.read_text(encoding="utf-8-sig").strip()
        if not token:
            raise ValueError("GitLab token file is empty")
        records = []
        page = 1
        while True:
            remaining = deadline - time.monotonic()
            if remaining <= 0 or page > 100:
                raise TimeoutError("GitLab collection exceeded its request budget")
            query = urlencode([*parameters, ("per_page", 100), ("page", page)])
            request = Request(
                f"{self.project_url}/{resource}?{query}",
                headers={"PRIVATE-TOKEN": token, "Accept": "application/json"},
            )
            with self.opener.open(request, timeout=min(4, remaining)) as response:
                rows = json.load(response)
                if not isinstance(rows, list):
                    raise TypeError("GitLab returned an invalid collection")
                records.extend(rows)
                next_page = response.headers.get("X-Next-Page", "")
            if not next_page:
                return records
            if not next_page.isdecimal() or int(next_page) <= page:
                raise ValueError("GitLab returned invalid pagination")
            page = int(next_page)


def metric(name, labels, value):
    escaped_labels = []
    for key, value_text in labels.items():
        escaped = str(value_text).replace("\\", "\\\\")
        escaped = escaped.replace("\n", "\\n").replace('"', '\\"')
        escaped_labels.append(f'{key}="{escaped}"')
    suffix = "{" + ",".join(escaped_labels) + "}" if labels else ""
    number = format(value, ".17g") if isinstance(value, float) else value
    return f"{name}{suffix} {number}\n"


def render_metrics(runners, jobs, runner_hosts, now):
    runner_inventory = {runner["id"]: runner for runner in runners}
    active_jobs = [job for job in jobs if job["status"] in ACTIVE_STATUSES]
    # Keep jobs visible even if a runner was removed during the two API reads.
    for job in active_jobs:
        runner = job.get("runner") or UNASSIGNED_RUNNER
        runner_inventory.setdefault(runner["id"], runner)

    lines = []
    for runner_id, runner in sorted(runner_inventory.items()):
        description = runner.get("description") or str(runner_id)
        labels = {
            "runner_id": runner_id,
            "runner": description,
            "host": runner_hosts.get(description, "미분류"),
        }
        lines.append(
            metric(
                "a305_gitlab_runner_info",
                {
                    **labels,
                    "status": runner.get("status", "unknown"),
                    "paused": str(runner.get("paused", False)).lower(),
                },
                1,
            )
        )
        assigned_jobs = [
            job
            for job in active_jobs
            if (job.get("runner") or UNASSIGNED_RUNNER)["id"] == runner_id
        ]
        lines.append(
            metric("a305_gitlab_runner_running_jobs", labels, len(assigned_jobs))
        )
        for job in assigned_jobs:
            started_at = job.get("started_at")
            elapsed = "NaN"
            if started_at:
                started_timestamp = datetime.fromisoformat(started_at).timestamp()
                elapsed = max(0, now - started_timestamp)
            lines.append(
                metric(
                    "a305_gitlab_job_elapsed_seconds",
                    {
                        **labels,
                        "job_id": job["id"],
                        "job_name": job["name"],
                        "status": job["status"],
                        "ref": job.get("ref", ""),
                        "stage": job.get("stage", ""),
                        "pipeline_id": (job.get("pipeline") or {}).get("id", ""),
                        "job_url": job.get("web_url", ""),
                    },
                    elapsed,
                )
            )
    waiting_jobs = sum(job["status"] in WAITING_STATUSES for job in jobs)
    lines.append(metric("a305_gitlab_pending_jobs", {}, waiting_jobs))
    return "".join(lines)


def collect_metrics(client, runner_hosts):
    deadline = time.monotonic() + 10
    try:
        runners = client.get_list("runners", deadline=deadline)
        statuses = sorted(ACTIVE_STATUSES | WAITING_STATUSES)
        jobs = client.get_list(
            "jobs", [("scope[]", status) for status in statuses], deadline=deadline
        )
        now = time.time()
        metrics = render_metrics(runners, jobs, runner_hosts, now)
    except (OSError, URLError, ValueError, KeyError, TypeError) as error:
        # Exception messages and upstream response bodies can contain credentials.
        print(
            f"GitLab runner collection failed ({type(error).__name__})",
            file=sys.stderr,
            flush=True,
        )
        return metric("a305_gitlab_scrape_success", {}, 0)
    return (
        metric("a305_gitlab_scrape_success", {}, 1)
        + metric("a305_gitlab_last_success_timestamp_seconds", {}, now)
        + metrics
    )


def make_handler(client, runner_hosts):
    class MetricsHandler(BaseHTTPRequestHandler):
        def do_GET(self):
            if self.path == "/metrics":
                payload = collect_metrics(client, runner_hosts).encode("utf-8")
            elif self.path == "/health":
                payload = b"ok\n"
            else:
                self.send_error(404)
                return
            self.send_response(200)
            self.send_header("Content-Type", "text/plain; version=0.0.4; charset=utf-8")
            self.send_header("Content-Length", str(len(payload)))
            self.end_headers()
            self.wfile.write(payload)

        def log_message(self, format_string, *arguments):
            # Only collection failures need logs; avoid a line for every scrape.
            pass

    return MetricsHandler


def main():
    client = GitLabClient(
        os.environ["GITLAB_URL"],
        os.environ["GITLAB_PROJECT_ID"],
        os.environ.get("GITLAB_TOKEN_FILE", "/run/secrets/gitlab-monitoring-token"),
    )
    hosts_file = Path(os.environ.get("RUNNER_HOSTS_FILE", "/app/runner-hosts.json"))
    runner_hosts = json.loads(hosts_file.read_text(encoding="utf-8"))
    HTTPServer(("0.0.0.0", 9253), make_handler(client, runner_hosts)).serve_forever()


if __name__ == "__main__":
    main()
