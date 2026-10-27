import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import runtime_probe


def fetch_response(url, **request_options):
    responses = {
        "http://web/api/health": (json.dumps({"status": "UP"}).encode(), {}),
        "http://web/api/private-resource": (b"", {}),
        "http://127.0.0.1:8020/health": (json.dumps({"ok": True}).encode(), {}),
        "http://127.0.0.1:8020/v1/judge": (b"", {}),
        "http://web/": (b"<html></html>", {"Content-Type": "text/html"}),
        "http://web/version.json": (json.dumps({"service": "web"}).encode(), {}),
    }
    return responses[url]


class RuntimeProbeTests(unittest.TestCase):
    def test_missing_feedback_prompt_fails_even_when_health_is_ready(self):
        with tempfile.TemporaryDirectory(prefix="a305-runtime-probe-") as directory:
            prompt_path = Path(directory) / "feedback_system.ko.md"
            with (
                patch.object(
                    runtime_probe, "FEEDBACK_PROMPT_PATH", prompt_path, create=True
                ),
                patch.object(runtime_probe, "fetch", side_effect=fetch_response),
                patch.dict("os.environ", {"AI_WORKER_TOKEN": "runtime-test-only"}),
                self.assertRaises(FileNotFoundError),
            ):
                runtime_probe.main()

    def test_packaged_feedback_prompt_allows_ready_runtime_to_pass(self):
        with tempfile.TemporaryDirectory(prefix="a305-runtime-probe-") as directory:
            prompt_path = Path(directory) / "feedback_system.ko.md"
            prompt_path.write_text("Feedback instructions", encoding="utf-8")
            with (
                patch.object(runtime_probe, "FEEDBACK_PROMPT_PATH", prompt_path),
                patch.object(runtime_probe, "fetch", side_effect=fetch_response),
                patch.dict("os.environ", {"AI_WORKER_TOKEN": "runtime-test-only"}),
                patch("builtins.print"),
            ):
                runtime_probe.main()


if __name__ == "__main__":
    unittest.main()
