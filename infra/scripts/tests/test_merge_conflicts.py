import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "check_merge_conflicts.py"


class MergeConflictTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.target = Path(self.directory.name) / "target.git"
        self.source = Path(self.directory.name) / "source"
        self.target.mkdir()
        self.git(self.target, "init", "-q", "--initial-branch=dev")
        self.configure_author(self.target)
        (self.target / "audio.txt").write_text("original audio\n", encoding="utf-8")
        self.git(self.target, "add", ".")
        self.git(self.target, "commit", "-qm", "base")
        self.git(self.target, "clone", "-q", str(self.target), str(self.source))
        self.configure_author(self.source)
        self.git(self.source, "switch", "-qc", "feature")

    def git(self, repository, *arguments):
        result = subprocess.run(
            ["git", *arguments],
            cwd=repository,
            check=True,
            capture_output=True,
            text=True,
            encoding="utf-8",
        )
        return result.stdout.strip()

    def configure_author(self, repository):
        self.git(repository, "config", "user.name", "Conflict test")
        self.git(repository, "config", "user.email", "ci-test@example.invalid")

    def commit_file(self, repository, filename, content):
        (repository / filename).write_text(content, encoding="utf-8")
        self.git(repository, "add", ".")
        self.git(repository, "commit", "-qm", "change")

    def check(self, **variables):
        environment = {
            **os.environ,
            "CI_PIPELINE_SOURCE": "merge_request_event",
            "CI_PROJECT_ID": "123",
            "CI_MERGE_REQUEST_PROJECT_ID": "123",
            "CI_MERGE_REQUEST_TARGET_BRANCH_NAME": "dev",
            "CI_COMMIT_SHA": self.git(self.source, "rev-parse", "HEAD"),
            "CI_MERGE_REQUEST_SOURCE_BRANCH_SHA": "",
            **variables,
        }
        return subprocess.run(
            [sys.executable, str(SCRIPT)],
            check=False,
            cwd=self.source,
            env=environment,
            capture_output=True,
            text=True,
            encoding="utf-8",
        )

    def test_conflicting_latest_target_blocks_checks_without_changing_checkout(self):
        self.commit_file(self.source, "audio.txt", "feature audio\n")
        self.commit_file(self.target, "audio.txt", "latest dev audio\n")
        previous_head = self.git(self.source, "rev-parse", "HEAD")
        (self.source / "local.txt").write_text("keep local work", encoding="utf-8")
        previous_status = self.git(self.source, "status", "--porcelain")

        result = self.check()

        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("audio.txt", result.stdout)
        self.assertEqual(self.git(self.source, "rev-parse", "HEAD"), previous_head)
        self.assertEqual(
            self.git(self.source, "status", "--porcelain"), previous_status
        )
        self.assertNotEqual(
            self.git(self.source, "rev-parse", "origin/dev"),
            self.git(self.target, "rev-parse", "HEAD"),
        )

    def test_independent_changes_continue(self):
        self.commit_file(self.source, "feature.txt", "feature\n")
        self.commit_file(self.target, "target.txt", "latest dev\n")

        result = self.check()

        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("No merge conflicts", result.stdout)

    def test_modify_delete_conflict_blocks_checks(self):
        self.commit_file(self.source, "audio.txt", "feature audio\n")
        self.git(self.target, "rm", "-q", "audio.txt")
        self.git(self.target, "commit", "-qm", "delete")

        result = self.check()

        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("audio.txt", result.stdout)

    def test_old_checkout_still_checks_pipeline_source_commit(self):
        base = self.git(self.source, "rev-parse", "HEAD")
        self.commit_file(self.source, "audio.txt", "feature audio\n")
        feature = self.git(self.source, "rev-parse", "HEAD")
        self.commit_file(self.target, "audio.txt", "latest dev audio\n")
        self.git(self.source, "checkout", "--detach", base)

        result = self.check(CI_COMMIT_SHA=feature)

        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertEqual(self.git(self.source, "rev-parse", "HEAD"), base)

    def test_target_fetch_failure_blocks_checks(self):
        result = self.check(CI_MERGE_REQUEST_TARGET_BRANCH_NAME="missing")

        self.assertEqual(result.returncode, 2, result.stdout + result.stderr)
        self.assertIn("Could not fetch", result.stderr)

    def test_invalid_source_commit_blocks_checks(self):
        result = self.check(CI_COMMIT_SHA="0" * 40)

        self.assertEqual(result.returncode, 2, result.stdout + result.stderr)
        self.assertIn("Could not check", result.stderr)

    def test_merged_result_checks_original_source_commit(self):
        base = self.git(self.source, "rev-parse", "HEAD")
        self.commit_file(self.source, "audio.txt", "feature audio\n")
        feature = self.git(self.source, "rev-parse", "HEAD")
        self.commit_file(self.target, "audio.txt", "latest dev audio\n")

        result = self.check(
            CI_COMMIT_SHA=base, CI_MERGE_REQUEST_SOURCE_BRANCH_SHA=feature
        )

        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)

    def test_fork_checks_target_project_instead_of_source_origin(self):
        self.commit_file(self.source, "audio.txt", "feature audio\n")
        self.commit_file(self.target, "audio.txt", "latest dev audio\n")
        self.git(self.source, "remote", "set-url", "origin", "missing-source")

        result = self.check(
            CI_PROJECT_ID="456",
            CI_MERGE_REQUEST_PROJECT_URL=self.target.as_uri().removesuffix(".git"),
        )

        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)

    def test_non_mr_pipeline_skips_target_check(self):
        result = self.check(CI_PIPELINE_SOURCE="parent_pipeline")

        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("Skipping", result.stdout)


if __name__ == "__main__":
    unittest.main()
