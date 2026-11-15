import unittest
from pathlib import Path, PurePosixPath

import yaml

REPOSITORY = Path(__file__).resolve().parents[3]
UNITY_MR_JOBS = ("unity:format", "unity:external-mr")


class UnityJobSelectionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.jobs = yaml.load(
            (REPOSITORY / ".gitlab/ci/unity.yml").read_text(), Loader=yaml.BaseLoader
        )

    def selects_job(self, job_name, changed_paths):
        mr_rule = next(
            rule
            for rule in self.jobs[job_name]["rules"]
            if isinstance(rule, dict) and '"merge_request_event"' in rule.get("if", "")
        )
        patterns = mr_rule["changes"]["paths"]
        return any(
            PurePosixPath(path).full_match(pattern)
            for path in changed_paths
            for pattern in patterns
        )

    def test_unrelated_ci_changes_do_not_select_unity_jobs(self):
        for path in (
            ".gitlab-ci.yml",
            ".gitlab/ci/backend.yml",
            ".gitlab/ci/ai.yml",
            ".gitlab/ci/deploy.yml",
            ".gitlab/ci/validation.yml",
        ):
            for job_name in UNITY_MR_JOBS:
                with self.subTest(path=path, job=job_name):
                    self.assertFalse(self.selects_job(job_name, [path]))

    def test_unity_inputs_and_its_ci_configuration_still_select_required_jobs(self):
        for path in (
            ".gitlab/ci/unity.yml",
            "frontend/UnityClient/Assets/Scripts/Player.cs",
            "frontend/UnityClient/Assets/Tests/EditMode/PlayerTests.cs",
            "frontend/UnityClient/Assets/WebGLTemplates/A305Responsive/index.html",
            "frontend/UnityClient/Packages/manifest.json",
            "frontend/design/logo.png",
            "docs/api/mvp-api.md",
            "infra/scripts/run-unity-ci.ps1",
            "infra/scripts/unity-test-scope.ps1",
            "infra/scripts/prepare-unity-test-inputs.ps1",
            "infra/scripts/unity-code-quality.ps1",
            "infra/tests/tripo/create_ci_fixtures.py",
        ):
            for job_name in UNITY_MR_JOBS:
                with self.subTest(path=path, job=job_name):
                    self.assertTrue(self.selects_job(job_name, [path]))

    def test_convention_documents_select_only_the_format_check(self):
        for path in (
            "AGENTS.md",
            "docs/code-conventions.md",
            "docs/unity/conventions.md",
        ):
            with self.subTest(path=path):
                self.assertTrue(self.selects_job("unity:format", [path]))
                self.assertFalse(self.selects_job("unity:external-mr", [path]))

    def test_merge_requests_do_not_start_a_windows_editor_job(self):
        for name, job in self.jobs.items():
            if name.startswith(".") or job.get("extends") != ".unity-editor":
                continue
            with self.subTest(job=name):
                self.assertTrue(job.get("rules"))
                self.assertFalse(
                    any(
                        isinstance(rule, dict)
                        and '"merge_request_event"' in rule.get("if", "")
                        for rule in job["rules"]
                    )
                )

    def test_production_tests_run_the_full_suite(self):
        job = self.jobs["unity:editor-tests"]
        self.assertNotIn("-ChangedSince", " ".join(job["script"]))
        self.assertIn("-Action test", " ".join(job["script"]))
        self.assertNotIn("-Action build", " ".join(job["script"]))
        self.assertTrue(
            any(
                isinstance(rule, dict)
                and '"parent_pipeline"' in rule.get("if", "")
                and 'A305_CHECK_UNITY == "true"' in rule.get("if", "")
                and 'A305_DEPLOY_ENVIRONMENT == "prod"' in rule.get("if", "")
                for rule in job["rules"]
            )
        )

    def test_development_build_does_not_execute_editor_tests(self):
        job = self.jobs["unity:webgl-build"]
        script = " ".join(job["script"])
        self.assertIn("-Action build", script)
        self.assertNotIn("-Action test", script)
        rule = next(
            rule
            for rule in job["rules"]
            if 'A305_DEPLOY_ENVIRONMENT == "dev"' in rule.get("if", "")
        )
        self.assertIn('A305_BUILD_WEB == "true"', rule["if"])
        self.assertNotIn("needs", rule)
        self.assertEqual(job["interruptible"], "false")

    def test_production_and_manual_builds_wait_for_editor_tests(self):
        rules = self.jobs["unity:webgl-build"]["rules"]
        production = next(
            rule
            for rule in rules
            if 'A305_DEPLOY_ENVIRONMENT == "prod"' in rule.get("if", "")
        )
        manual = next(
            rule
            for rule in rules
            if isinstance(rule, dict) and "RUN_UNITY_BUILD" in rule.get("if", "")
        )
        self.assertIn('A305_BUILD_WEB == "true"', production["if"])
        self.assertIn('CI_COMMIT_REF_PROTECTED == "true"', manual["if"])
        for rule in (production, manual):
            self.assertEqual(
                rule["needs"], [{"job": "unity:editor-tests", "artifacts": "false"}]
            )

    def test_external_unity_changes_remain_blocked(self):
        job = self.jobs["unity:external-mr"]
        self.assertEqual(job["allow_failure"], "false")
        self.assertIn("exit 1", job["script"])
        self.assertTrue(
            any(
                "CI_MERGE_REQUEST_SOURCE_PROJECT_ID != $CI_MERGE_REQUEST_PROJECT_ID"
                in rule.get("if", "")
                for rule in job["rules"]
                if isinstance(rule, dict)
            )
        )

    def test_ci_changes_do_not_hide_unity_changes_in_the_same_mr(self):
        changed_paths = [
            ".gitlab-ci.yml",
            "frontend/UnityClient/Assets/Scenes/Home.unity",
        ]
        for job_name in UNITY_MR_JOBS:
            with self.subTest(job=job_name):
                self.assertTrue(self.selects_job(job_name, changed_paths))

    def test_unity_jobs_do_not_share_an_exclusive_resource_group(self):
        template_group = self.jobs[".unity-editor"].get("resource_group")
        self.assertIsNone(
            self.jobs["unity:editor-tests"].get("resource_group", template_group)
        )


if __name__ == "__main__":
    unittest.main()
