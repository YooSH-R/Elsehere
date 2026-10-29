import io
import json
import tempfile
import unittest
from contextlib import redirect_stdout
from datetime import UTC, datetime, timedelta
from pathlib import Path

from traffic_baseline import main, summarize


class TrafficBaselineTests(unittest.TestCase):
    def test_ignores_legacy_partial_and_invalid_events_without_echoing_them(self):
        end = datetime(2026, 10, 28, 1, tzinfo=UTC)
        event = {
            "time": (end - timedelta(seconds=1)).isoformat(),
            "environment": "dev",
            "route": "/api/**",
            "status": 200,
            "duration_seconds": 0.1,
        }
        invalid_lines = ["private-legacy-line", "{", "[]", "{}"]
        for change in [
            {"status": True},
            {"status": 600},
            {"duration_seconds": float("nan")},
            {"duration_seconds": float("inf")},
            {"duration_seconds": -1},
            {"duration_seconds": True},
            {"time": "2026-10-28T00:59:59"},
        ]:
            invalid_lines.append(json.dumps(event | change))
        report = summarize(invalid_lines, end - timedelta(minutes=1), end)
        self.assertEqual(report["groups"], [])
        self.assertEqual(report["unrecognized_lines"], len(invalid_lines))
        self.assertNotIn("private-", json.dumps(report, allow_nan=False))

    def test_keeps_environments_separate_and_uses_a_half_open_time_window(self):
        end = datetime(2026, 10, 28, 1, tzinfo=UTC)
        event = {
            "time": (end - timedelta(minutes=1)).isoformat(),
            "environment": "dev",
            "route": "/api/**",
            "status": 200,
            "duration_seconds": 0.1,
        }
        report = summarize(
            [
                json.dumps(event | change)
                for change in [
                    {},
                    {"environment": "prod"},
                    {"time": end.isoformat()},
                    {"time": (end - timedelta(minutes=2)).isoformat()},
                ]
            ],
            end - timedelta(minutes=1),
            end,
        )
        self.assertEqual([group["requests"] for group in report["groups"]], [1, 1])
        self.assertEqual(report["outside_window_lines"], 2)

    def test_ten_second_peak_does_not_include_events_ten_seconds_apart(self):
        end = datetime(2026, 10, 28, 1, tzinfo=UTC)
        lines = [
            json.dumps(
                {
                    "time": (end - timedelta(seconds=seconds)).isoformat(),
                    "environment": "dev",
                    "route": "/api/**",
                    "status": 200,
                    "duration_seconds": 0.1,
                }
            )
            for seconds in [11, 1]
        ]
        report = summarize(lines, end - timedelta(minutes=1), end)
        self.assertEqual(report["groups"][0]["peak_completed_in_10s"], 1)

    def test_command_reads_a_selected_log_window_without_changing_the_file(self):
        event = {
            "time": "2026-10-28T09:59:55+09:00",
            "environment": "dev",
            "route": "/api/**",
            "status": 200,
            "duration_seconds": 0.1,
        }
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "access.log"
            content = json.dumps(event) + "\n"
            path.write_text(content, encoding="utf-8")
            output = io.StringIO()
            with redirect_stdout(output):
                main(
                    [
                        "--log",
                        str(path),
                        "--minutes",
                        "5",
                        "--end",
                        "2026-10-28T01:00:00+00:00",
                    ]
                )
            self.assertEqual(path.read_text(encoding="utf-8"), content)
        report = json.loads(output.getvalue())
        self.assertEqual(report["groups"][0]["requests"], 1)
        self.assertEqual(report["window_start"], "2026-10-28T00:55:00+00:00")

    def test_never_exports_unknown_routes_or_sensitive_log_fields(self):
        end = datetime(2026, 10, 28, 1, tzinfo=UTC)
        event = {
            "time": (end - timedelta(seconds=1)).isoformat(),
            "environment": "dev",
            "route": "/api/**",
            "status": 200,
            "duration_seconds": 0.1,
            "request_id": "private-request-id",
            "authorization": "private-token",
            "request": "/api/users/private-user?token=private-query",
            "remote_addr": "private-address",
        }
        lines = [
            json.dumps(event),
            json.dumps(event | {"route": "/api/private-user?token=private-query"}),
            json.dumps(event | {"environment": "private-environment"}),
        ]

        report = summarize(lines, end - timedelta(minutes=1), end)

        self.assertEqual(len(report["groups"]), 1)
        self.assertEqual(report["groups"][0]["requests"], 1)
        self.assertEqual(report["unrecognized_lines"], 2)
        self.assertNotIn("private-", json.dumps(report))

    def test_counts_api_completion_bursts_and_response_times(self):
        end = datetime(2026, 10, 28, 1, tzinfo=UTC)
        lines = []
        for seconds, duration, status in [
            (20, 0.1, 200),
            (20, 0.2, 201),
            (19, 0.3, 200),
            (11, 0.4, 401),
            (1, 0.5, 500),
        ]:
            lines.append(
                json.dumps(
                    {
                        "time": (end - timedelta(seconds=seconds)).isoformat(),
                        "environment": "dev",
                        "route": "/api/**",
                        "status": status,
                        "duration_seconds": duration,
                    }
                )
            )

        report = summarize(lines, end - timedelta(minutes=1), end)

        self.assertEqual(
            report["groups"],
            [
                {
                    "environment": "dev",
                    "route": "/api/**",
                    "requests": 5,
                    "peak_completed_in_1s": 2,
                    "peak_completed_in_10s": 4,
                    "responses_2xx_3xx": 3,
                    "responses_4xx": 1,
                    "responses_5xx": 1,
                    "duration_p95_seconds": 0.5,
                    "duration_max_seconds": 0.5,
                }
            ],
        )


if __name__ == "__main__":
    unittest.main()
