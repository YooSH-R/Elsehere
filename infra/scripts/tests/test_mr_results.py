import hashlib
import json
import os
import shutil
import subprocess
import tempfile
import unittest
import zipfile
from pathlib import Path
from unittest.mock import patch
from urllib.error import HTTPError

import mr_results


class MergeRequestResultsTests(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory(prefix="a305-mr-results-")
        self.addCleanup(directory.cleanup)
        self.repository = Path(directory.name)
        self.git("init", "--initial-branch=dev")
        self.git("config", "user.name", "CI reuse test")
        self.git("config", "user.email", "ci@example.invalid")
        self.source = self.commit_file("backend/build.gradle", "verified backend\n")
        self.candidate = self.commit_file("frontend/view.cs", "unrelated frontend\n")
        self.job = "backend:check-and-build"
        self.receipt = {
            "schema_version": 1,
            "commit": self.source,
            "project_id": "1456165",
            "pipeline_id": "11",
            "job_id": "21",
            "job": self.job,
        }
        self.merge_request = {
            "iid": 5,
            "state": "merged",
            "target_branch": "dev",
            "source_project_id": 1456165,
            "target_project_id": 1456165,
            "sha": self.source,
            "merge_commit_sha": self.candidate,
        }
        self.archive = self.repository / "fixture.zip"
        self.write_archive()
        environment = patch.dict(
            "os.environ",
            {
                "CI_PROJECT_ID": "1456165",
                "CI_API_V4_URL": "https://gitlab.example.invalid/api/v4",
                "CI_JOB_TOKEN": "fixture-token",
            },
        )
        environment.start()
        self.addCleanup(environment.stop)

    def git(self, *arguments):
        return (
            subprocess.check_output(
                ["git", "-C", str(self.repository), *arguments],
                stderr=subprocess.DEVNULL,
            )
            .decode()
            .strip()
        )

    def commit_file(self, name, content):
        path = self.repository / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")
        self.git("add", "--", name)
        self.git("-c", "gc.auto=0", "commit", "--quiet", "-m", name)
        return self.git("rev-parse", "HEAD")

    def write_archive(self):
        with zipfile.ZipFile(self.archive, "w") as archive:
            archive.writestr("dist/ci/success.json", json.dumps(self.receipt))
            archive.writestr("backend/build/commit.txt", self.source + "\n")
            archive.writestr("backend/build/libs/app.jar", b"verified jar")

    def find_results(self):
        def fetch_json(endpoint):
            return (
                [self.merge_request]
                if endpoint.startswith("merge_requests?")
                else self.receipt
            )

        def download_archive(endpoint, destination):
            shutil.copyfile(self.archive, destination)

        with (
            patch("mr_results.fetch_json", side_effect=fetch_json),
            patch("mr_results.download_archive", side_effect=download_archive),
        ):
            return mr_results.find_reusable_jobs(
                self.repository,
                self.candidate,
                [self.job],
                self.repository / "dist/deployment",
            )

    def test_merged_mr_jar_is_reused_after_an_unrelated_frontend_change(self):
        reused = self.find_results()

        self.assertIn(self.job, reused)
        self.assertEqual(reused[self.job]["source_commit"], self.source)
        self.assertEqual(reused[self.job]["mr_iid"], 5)
        self.assertTrue((self.repository / reused[self.job]["archive"]).is_file())

    def test_changed_code_dependency_api_or_ci_configuration_requires_new_checks(self):
        for path in (
            "backend/src/Main.java",
            "backend/build.gradle",
            "docs/api/contracts.md",
            ".gitlab/ci/backend.yml",
            ".editorconfig",
            ".gitattributes",
        ):
            with self.subTest(path=path):
                self.git("checkout", "--detach", self.candidate)
                candidate = self.commit_file(path, "changed input\n")
                self.assertFalse(
                    mr_results.same_job_inputs(
                        self.repository, self.job, self.source, candidate
                    )
                )

    def test_missing_artifacts_or_unavailable_api_fall_back_to_new_checks(self):
        for status in (401, 403, 404, 500):
            with (
                self.subTest(status=status),
                patch(
                    "mr_results.fetch_json",
                    side_effect=HTTPError("fixture", status, "unavailable", {}, None),
                ),
            ):
                self.assertEqual(
                    mr_results.find_reusable_jobs(
                        self.repository,
                        self.candidate,
                        [self.job],
                        self.repository / "dist/deployment",
                    ),
                    {},
                )

    def test_unmerged_external_or_unrelated_merge_requests_cannot_supply_results(self):
        for changes in (
            {"state": "opened"},
            {"source_project_id": 123},
            {"target_branch": "main"},
            {"merge_commit_sha": "f" * 40},
        ):
            with self.subTest(changes=changes):
                original = self.merge_request
                self.merge_request = {**original, **changes}
                self.assertEqual(self.find_results(), {})
                self.merge_request = original

    def test_receipt_from_another_commit_job_or_project_is_not_reused(self):
        for changes in (
            {"commit": self.candidate},
            {"job": "different-job"},
            {"project_id": "1"},
        ):
            with self.subTest(changes=changes):
                original = self.receipt
                self.receipt = {**original, **changes}
                self.assertEqual(self.find_results(), {})
                self.receipt = original

    def test_mismatched_build_source_is_not_reused(self):
        with zipfile.ZipFile(self.archive, "w") as archive:
            archive.writestr("dist/ci/success.json", json.dumps(self.receipt))
            archive.writestr("backend/build/commit.txt", self.candidate)
            archive.writestr("backend/build/libs/app.jar", b"unverified jar")
        self.assertEqual(self.find_results(), {})

    def test_legacy_mr_without_a_success_receipt_runs_checks_again(self):
        with patch(
            "mr_results.fetch_json",
            side_effect=[
                [self.merge_request],
                HTTPError("fixture", 404, "missing", {}, None),
            ],
        ):
            self.assertEqual(
                mr_results.find_reusable_jobs(
                    self.repository,
                    self.candidate,
                    [self.job],
                    self.repository / "dist/deployment",
                ),
                {},
            )

    def test_restore_keeps_original_source_and_extracts_only_required_build_files(self):
        reused = self.find_results()
        with zipfile.ZipFile(
            self.repository / reused[self.job]["archive"], "a"
        ) as archive:
            archive.writestr("../../unexpected.txt", "not a build input")
        archive_path = self.repository / reused[self.job]["archive"]
        reused[self.job]["sha256"] = hashlib.sha256(
            archive_path.read_bytes()
        ).hexdigest()
        plan_path = self.repository / "dist/deployment/plan.json"
        plan_path.write_text(
            json.dumps({"candidate_commit": self.candidate, "reused_jobs": reused})
        )

        mr_results.restore_results(self.repository, self.candidate)

        self.assertEqual(
            (self.repository / "backend/build/libs/app.jar").read_bytes(),
            b"verified jar",
        )
        self.assertEqual(
            (self.repository / "backend/build/commit.txt").read_text().strip(),
            self.source,
        )
        self.assertFalse((self.repository.parent / "unexpected.txt").exists())
        mr_results.verify_build_artifact(
            self.repository, "backend", self.candidate, reused
        )

    def test_corrupt_archive_cannot_restore_a_verified_build(self):
        reused = self.find_results()
        (self.repository / reused[self.job]["archive"]).write_bytes(b"corrupt archive")
        plan_path = self.repository / "dist/deployment/plan.json"
        plan_path.write_text(
            json.dumps({"candidate_commit": self.candidate, "reused_jobs": reused})
        )

        with self.assertRaisesRegex(ValueError, "checksum"):
            mr_results.restore_results(self.repository, self.candidate)
        self.assertFalse((self.repository / "backend/build/libs/app.jar").exists())

    def test_changed_backend_cannot_use_a_matching_marker_from_an_older_build(self):
        reused = self.find_results()
        self.candidate = self.commit_file(
            "backend/src/Main.java", "new implementation\n"
        )
        marker = self.repository / "backend/build/commit.txt"
        marker.parent.mkdir(parents=True)
        marker.write_text(self.source)
        with self.assertRaisesRegex(ValueError, "inputs"):
            mr_results.verify_build_artifact(
                self.repository, "backend", self.candidate, reused
            )

    def test_reused_archives_are_downloaded_from_the_exact_plan_job_without_pipeline_needs(
        self,
    ):
        reused = self.find_results()
        plan = {
            "candidate_commit": self.candidate,
            "plan_job_id": "90",
            "reused_jobs": reused,
        }

        def download(endpoint, destination):
            shutil.copyfile(self.archive, destination)

        with patch("mr_results.download_archive", side_effect=download) as request:
            mr_results.download_and_restore_results(
                self.repository, self.candidate, plan
            )
        self.assertEqual(
            request.call_args.args[0],
            "jobs/90/artifacts/dist/deployment/backend-mr-artifacts.zip",
        )
        self.assertEqual(
            (self.repository / "backend/build/libs/app.jar").read_bytes(),
            b"verified jar",
        )

    def test_api_token_is_not_forwarded_in_redirect_headers(self):
        request = mr_results.api_request("merge_requests")
        self.assertEqual(request.unredirected_hdrs["Job-token"], "fixture-token")
        self.assertNotIn("Job-token", request.headers)
        self.assertNotIn("fixture-token", request.full_url)


class SuccessReceiptTests(unittest.TestCase):
    def test_only_a_successful_mr_check_leaves_its_own_receipt(self):
        shell = shutil.which("sh") or "C:/Program Files/Git/bin/bash.exe"
        script = Path(__file__).resolve().parents[1] / "record-ci-success.sh"
        environment = {
            **os.environ,
            "CI_COMMIT_SHA": "a" * 40,
            "CI_PROJECT_ID": "1456165",
            "CI_PIPELINE_ID": "11",
            "CI_JOB_ID": "21",
            "CI_JOB_NAME": "backend:check-and-build",
        }
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "dist/ci/success.json"
            for source, status in (
                ("merge_request_event", "success"),
                ("merge_request_event", "failed"),
                ("parent_pipeline", "success"),
            ):
                output.parent.mkdir(parents=True, exist_ok=True)
                output.write_text("stale receipt")
                subprocess.run(
                    [shell, str(script)],
                    cwd=directory,
                    env={
                        **environment,
                        "CI_PIPELINE_SOURCE": source,
                        "CI_JOB_STATUS": status,
                    },
                    check=True,
                )
                if source == "merge_request_event" and status == "success":
                    self.assertEqual(json.loads(output.read_text())["job_id"], "21")
                else:
                    self.assertFalse(output.exists())


if __name__ == "__main__":
    unittest.main()
