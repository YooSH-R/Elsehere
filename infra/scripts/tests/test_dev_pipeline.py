import subprocess
import tempfile
import unittest
from pathlib import Path

from dev_pipeline import create_child_pipeline, create_release_plan


class ChildPipelineTests(unittest.TestCase):
    def test_large_asset_change_keeps_child_variables_small_without_losing_builds(self):
        pipeline = create_child_pipeline(
            {
                "checks": ["unity"],
                "builds": ["web"],
                "deploy": True,
                "changed_paths": [
                    f"frontend/Assets/Models/model-{index}.fbx" for index in range(3000)
                ],
            },
            "123",
        )

        value = pipeline["variables"]["A305_DEPLOY_PLAN"]
        self.assertLess(len(value.encode()), 10000)
        self.assertEqual(json.loads(value)["builds"], ["web"])

    def test_ai_only_change_selects_ai_checks_without_backend_or_unity_jobs(self):
        pipeline = create_child_pipeline(
            {"checks": ["ai"], "builds": ["ai"], "deploy": True}, "123"
        )

        self.assertEqual(pipeline["variables"]["A305_CHECK_AI"], "true")
        self.assertEqual(pipeline["variables"]["A305_CHECK_BACKEND"], "false")
        self.assertEqual(pipeline["variables"]["A305_CHECK_UNITY"], "false")
        jobs = [need.get("job") for need in pipeline["dev:deliver"]["needs"]]
        self.assertIn("ai:check-and-test", jobs)
        self.assertIn("ai:panel-tests", jobs)
        self.assertNotIn("backend:check-and-build", jobs)
        self.assertNotIn("unity:editor-and-webgl", jobs)
        self.assertEqual(pipeline["variables"]["A305_PARENT_PIPELINE_ID"], "123")
        self.assertEqual(
            json.loads(pipeline["variables"]["A305_DEPLOY_PLAN"])["builds"], ["ai"]
        )
        self.assertTrue(
            all("pipeline" not in need for need in pipeline["dev:deliver"]["needs"])
        )

    def test_document_only_change_keeps_just_yaml_check_and_no_build_flag(self):
        plan = {"checks": [], "builds": [], "deploy": False}
        state = {
            "candidate_commit": "a" * 40,
            "images": {"web": {"reference": "existing"}},
        }

        pipeline = create_child_pipeline(plan, "123", state)

        self.assertEqual(
            pipeline["dev:deliver"]["needs"], [{"job": "ci:yaml", "artifacts": True}]
        )
        self.assertEqual(pipeline["variables"]["A305_BUILD_WEB"], "false")
        self.assertEqual(json.loads(pipeline["variables"]["A305_DEPLOY_STATE"]), state)

    def test_shared_api_change_checks_all_services_without_rebuilding_webgl(self):
        pipeline = create_child_pipeline(
            {
                "checks": ["ai", "backend", "infra", "unity"],
                "builds": [],
                "deploy": False,
            },
            "123",
        )

        jobs = {need["job"] for need in pipeline["dev:deliver"]["needs"]}
        self.assertIn("unity:editor-and-webgl", jobs)
        self.assertIn("backend:check-and-build", jobs)
        self.assertIn("infra:app-runtime", jobs)
        self.assertIn("ai:check-and-test", jobs)
        self.assertEqual(pipeline["variables"]["A305_BUILD_WEB"], "false")

    def test_backend_change_checks_real_runtime_without_selecting_unity(self):
        pipeline = create_child_pipeline(
            {"checks": ["backend"], "builds": ["backend"], "deploy": True}, "123"
        )

        jobs = {need["job"] for need in pipeline["dev:deliver"]["needs"]}
        self.assertIn("infra:app-runtime", jobs)
        self.assertNotIn("unity:editor-and-webgl", jobs)


class MonitoringCompatibilityTests(unittest.TestCase):
    def test_backend_candidate_without_deployed_monitoring_code_is_rejected(self):
        with tempfile.TemporaryDirectory(prefix="a305-monitoring-plan-") as directory:
            repository = Path(directory)

            def git(*arguments):
                return subprocess.run(
                    ["git", "-C", str(repository), *arguments],
                    check=True,
                    capture_output=True,
                    text=True,
                ).stdout.strip()

            git("init", "-q")
            git("config", "user.name", "Deployment test")
            git("config", "user.email", "deployment-test@example.invalid")
            (repository / "backend").mkdir()
            source = repository / "backend/app.txt"
            source.write_text("application")
            git("add", ".")
            git("commit", "-qm", "base")
            base = git("rev-parse", "HEAD")
            source.write_text("application with monitoring")
            git("commit", "-qam", "monitoring")
            monitoring = git("rev-parse", "HEAD")
            git("checkout", "--detach", base)
            source.write_text("new application without monitoring")
            git("commit", "-qam", "backend change")
            candidate = git("rev-parse", "HEAD")

            with self.assertRaisesRegex(ValueError, "monitoring"):
                create_release_plan(
                    repository,
                    {"candidate_commit": base, "minimum_backend_commit": monitoring},
                    candidate,
                )


if __name__ == "__main__":
    unittest.main()
import json
