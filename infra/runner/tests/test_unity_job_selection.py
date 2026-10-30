import unittest
from pathlib import Path, PurePosixPath

import yaml

REPOSITORY = Path(__file__).resolve().parents[3]
UNITY_MR_JOBS = ("unity:format", "unity:editor-tests", "unity:external-mr")


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
            "frontend/UnityClient/Packages/manifest.json",
            "docs/api/mvp-api.md",
            "infra/scripts/run-unity-ci.ps1",
            "infra/scripts/prepare-unity-test-inputs.ps1",
            "infra/scripts/unity-code-quality.ps1",
            "infra/tests/tripo/create_ci_fixtures.py",
            "AGENTS.md",
            "docs/code-conventions.md",
            "docs/unity/conventions.md",
        ):
            for job_name in UNITY_MR_JOBS:
                with self.subTest(path=path, job=job_name):
                    self.assertTrue(self.selects_job(job_name, [path]))

    def test_ci_changes_do_not_hide_unity_changes_in_the_same_mr(self):
        changed_paths = [
            ".gitlab-ci.yml",
            "frontend/UnityClient/Assets/Scenes/Home.unity",
        ]
        for job_name in UNITY_MR_JOBS:
            with self.subTest(job=job_name):
                self.assertTrue(self.selects_job(job_name, changed_paths))

    def test_editor_and_build_jobs_use_the_same_resource_group(self):
        template_group = self.jobs[".unity-editor"].get("resource_group")
        for job_name in ("unity:editor-tests", "unity:editor-and-webgl"):
            with self.subTest(job=job_name):
                self.assertEqual(
                    self.jobs[job_name].get("resource_group", template_group),
                    "a305-unity-editor",
                )


if __name__ == "__main__":
    unittest.main()
