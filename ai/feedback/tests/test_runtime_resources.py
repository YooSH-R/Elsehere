"""실제 이미지 대신 별도 폴더로 배포 자원 누락을 재현한다."""

import json
import shutil
import socket
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from feedback.runtime_resources import check_runtime_resources


class RuntimeResourceTests(unittest.TestCase):
    def make_resources(self, root):
        source = Path(__file__).resolve().parents[1]
        for name in ("prompts", "content"):
            shutil.copytree(source / name, root / name)

    def test_missing_prompt_is_reported_before_any_model_call(self):
        with tempfile.TemporaryDirectory() as directory:
            report = check_runtime_resources(Path(directory))
        self.assertFalse(report["ready"])
        self.assertIn({"resource": "prompts/feedback_system.ko.md", "code": "missing"}, report["issues"])

    def test_present_but_invalid_resources_are_not_reported_ready(self):
        catalog_path = "content/spoken_usage.ko.json"
        prompt_path = "prompts/feedback_system.ko.md"
        cases = [
            (prompt_path, b" \n", "empty"),
            (prompt_path, b"\xff", "invalid_utf8"),
            (catalog_path, b"{broken", "invalid_json"),
            (catalog_path, b"[]", "invalid_catalog"),
            (catalog_path, b"{}", "invalid_catalog"),
        ]
        for resource, content, code in cases:
            with self.subTest(code=code), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                self.make_resources(root)
                (root / resource).write_bytes(content)
                report = check_runtime_resources(root)
                self.assertFalse(report["ready"])
                self.assertIn({"resource": resource, "code": code}, report["issues"])

    def test_catalog_entries_must_support_candidate_search_and_note_rendering(self):
        for problem in ("missing_usage", "duplicate_id", "wrong_sources", "wrong_patterns", "bad_regex", "nonobject"):
            with self.subTest(problem=problem), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                self.make_resources(root)
                path = root / "content/spoken_usage.ko.json"
                catalog = json.loads(path.read_text(encoding="utf-8"))
                if problem == "missing_usage":
                    del catalog[0]["usage_ko"]
                elif problem == "duplicate_id":
                    catalog[1]["catalog_id"] = catalog[0]["catalog_id"]
                elif problem == "wrong_sources":
                    catalog[0]["sources"] = "https://example.com"
                elif problem == "wrong_patterns":
                    catalog[0]["patterns"] = "not a list"
                elif problem == "bad_regex":
                    catalog[0]["patterns"] = ["["]
                else:
                    catalog[0] = "not an entry"
                path.write_text(json.dumps(catalog), encoding="utf-8")
                report = check_runtime_resources(root)
                self.assertFalse(report["ready"])
                self.assertIn({"resource": "content/spoken_usage.ko.json", "code": "invalid_catalog"}, report["issues"])

    def test_current_assets_pass_without_environment_or_network(self):
        with patch.object(socket.socket, "connect", side_effect=AssertionError("network forbidden")):
            report = check_runtime_resources(Path(__file__).resolve().parents[1])
        self.assertEqual(report, {"ready": True, "issues": []})

    def test_cli_works_from_another_directory_and_returns_failure_exit_code(self):
        module_root = Path(__file__).resolve().parents[1]
        with tempfile.TemporaryDirectory() as directory:
            for root, expected_code in ((module_root, 0), (Path(directory), 1)):
                result = subprocess.run(
                    [sys.executable, "-X", "utf8", str(module_root / "check_runtime.py"), "--root", str(root)],
                    cwd=directory,
                    capture_output=True,
                    text=True,
                    encoding="utf-8",
                    timeout=10,
                    check=False,
                )
                self.assertEqual(result.returncode, expected_code, result.stderr)
                self.assertEqual(json.loads(result.stdout)["ready"], expected_code == 0)
                self.assertEqual(result.stderr, "")

    def test_read_errors_do_not_disclose_file_contents_or_exception_details(self):
        with patch.object(Path, "read_text", side_effect=PermissionError("PRIVATE_DETAIL_SENTINEL")):
            report = check_runtime_resources(Path("unused"))
        self.assertFalse(report["ready"])
        self.assertEqual(len(report["issues"]), 2)
        self.assertTrue(all(issue["code"] == "unreadable" for issue in report["issues"]))
        self.assertNotIn("PRIVATE_DETAIL_SENTINEL", json.dumps(report))


if __name__ == "__main__":
    unittest.main()
