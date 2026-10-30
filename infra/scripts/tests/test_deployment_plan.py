import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

import deployment_plan
from deployment_plan import select_deployment_work


class DeploymentPlanTest(unittest.TestCase):
    def test_documentation_does_not_build_or_deploy_applications(self):
        plan = select_deployment_work(["README.md", "docs/development.md"])

        self.assertEqual(plan["checks"], [])
        self.assertEqual(plan["builds"], [])
        self.assertFalse(plan["deploy"])

    def test_backend_change_selects_backend_checks_and_image(self):
        plan = select_deployment_work(
            ["backend/src/main/java/com/elsehere/Example.java"]
        )

        self.assertEqual(plan["checks"], ["backend"])
        self.assertEqual(plan["builds"], ["backend"])
        self.assertTrue(plan["deploy"])

    def test_webgl_scene_changes_select_unity_without_backend_rebuild(self):
        plan = select_deployment_work(["frontend/UnityClient/Assets/Scenes/Home.unity"])

        self.assertEqual(plan["checks"], ["unity"])
        self.assertEqual(plan["builds"], ["web"])

    def test_ai_code_changes_select_ai_without_unity_rebuild(self):
        plan = select_deployment_work(["ai/worker/app/main.py"])

        self.assertEqual(plan["checks"], ["ai"])
        self.assertEqual(plan["builds"], ["ai"])

    def test_feedback_prompt_changes_rebuild_ai_instead_of_skipping_markdown(self):
        plan = select_deployment_work(["ai/feedback/prompts/feedback_system.ko.md"])

        self.assertEqual(plan["checks"], ["ai"])
        self.assertEqual(plan["builds"], ["ai"])
        self.assertTrue(plan["deploy"])

    def test_unity_build_script_change_rebuilds_only_the_web_image(self):
        plan = select_deployment_work(["infra/scripts/run-unity-ci.ps1"])

        self.assertEqual(plan["checks"], ["infra", "unity"])
        self.assertEqual(plan["builds"], ["web"])
        self.assertTrue(plan["deploy"])

    def test_unity_test_input_changes_run_unity_without_rebuilding_images(self):
        for path in [
            "infra/scripts/prepare-unity-test-inputs.ps1",
            "infra/tests/tripo/create_ci_fixtures.py",
            "infra/tests/tripo/test_ci_fixtures.py",
        ]:
            with self.subTest(path=path):
                plan = select_deployment_work([path])
                self.assertEqual(plan["checks"], ["infra", "unity"])
                self.assertEqual(plan["builds"], [])
                self.assertFalse(plan["deploy"])

    def test_api_contract_requires_all_service_checks_without_new_images(self):
        plan = select_deployment_work(["docs/api/mvp-api.md"])

        self.assertEqual(plan["checks"], ["ai", "backend", "infra", "unity"])
        self.assertEqual(plan["builds"], [])

    def test_ci_configuration_requires_checks_without_app_deployment(self):
        plan = select_deployment_work([".gitlab/ci/backend.yml"])

        self.assertEqual(plan["checks"], ["ai", "backend", "infra", "unity"])
        self.assertEqual(plan["builds"], [])
        self.assertFalse(plan["deploy"])

    def test_compose_change_reuses_images_and_verifies_all_services(self):
        plan = select_deployment_work(["infra/compose/compose.app.yaml"])

        self.assertEqual(plan["checks"], ["ai", "backend", "infra", "unity"])
        self.assertEqual(plan["builds"], [])
        self.assertTrue(plan["deploy"])

    def test_service_tests_do_not_change_runtime_images(self):
        for path, check in [
            ("backend/src/test/java/ExampleTest.java", "backend"),
            ("ai/worker/tests/test_api.py", "ai"),
            ("frontend/UnityClient/Assets/Tests/EditMode/PlayerTests.cs", "unity"),
        ]:
            with self.subTest(path=path):
                plan = select_deployment_work([path])
                self.assertEqual(plan["checks"], [check])
                self.assertEqual(plan["builds"], [])
                self.assertFalse(plan["deploy"])

    def test_runtime_image_configuration_selects_its_service(self):
        plan = select_deployment_work(["infra/docker/ai-worker.Dockerfile"])

        self.assertEqual(plan["checks"], ["ai", "infra"])
        self.assertEqual(plan["builds"], ["ai"])

    def test_shared_image_builder_runs_runtime_checks_without_deploying_applications(
        self,
    ):
        plan = select_deployment_work(["infra/scripts/docker_build.py"])

        self.assertEqual(plan["checks"], ["backend", "infra"])
        self.assertEqual(plan["builds"], [])
        self.assertFalse(plan["deploy"])

    def test_shared_build_context_exclusion_rebuilds_all_images(self):
        plan = select_deployment_work([".dockerignore"])

        self.assertEqual(plan["builds"], ["ai", "backend", "web"])

    def test_multiple_areas_combine_without_duplicate_work(self):
        plan = select_deployment_work(
            [
                "backend/src/main/java/Example.java",
                "backend/src/main/resources/application.properties",
                "ai/worker/app/main.py",
            ]
        )

        self.assertEqual(plan["checks"], ["ai", "backend"])
        self.assertEqual(plan["builds"], ["ai", "backend"])


class CumulativeDeploymentPlanTest(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.repository = Path(self.directory.name)
        self.git("init", "--quiet")
        self.git("config", "user.email", "ci@example.invalid")
        self.git("config", "user.name", "Deployment plan test")
        self.initial = self.commit_file("README.md", "initial\n")

    def git(self, *arguments):
        return subprocess.check_output(
            ["git", "-C", str(self.repository), *arguments], text=True
        ).strip()

    def commit_file(self, path, content):
        file_path = self.repository / path
        file_path.parent.mkdir(parents=True, exist_ok=True)
        file_path.write_text(content, encoding="utf-8")
        self.git("add", "--", path)
        self.git("-c", "gc.auto=0", "commit", "--quiet", "-m", path)
        return self.git("rev-parse", "HEAD")

    def test_docs_after_failed_backend_deployment_still_include_backend(self):
        self.commit_file("backend/src/main/java/Example.java", "backend change\n")
        candidate = self.commit_file("docs/development.md", "docs change\n")

        plan = deployment_plan.create_plan(self.repository, self.initial, candidate)

        self.assertEqual(plan["builds"], ["backend"])
        self.assertEqual(plan["base_commit"], self.initial)
        self.assertEqual(plan["candidate_commit"], candidate)

    def test_successful_backend_deployment_is_not_rebuilt_for_docs(self):
        deployed = self.commit_file(
            "backend/src/main/java/Example.java", "backend change\n"
        )
        candidate = self.commit_file("README.md", "docs change\n")

        plan = deployment_plan.create_plan(self.repository, deployed, candidate)

        self.assertEqual(plan["builds"], [])
        self.assertFalse(plan["deploy"])

    def test_first_deployment_builds_and_checks_all_services(self):
        plan = deployment_plan.create_plan(self.repository, None, self.initial)

        self.assertEqual(plan["checks"], ["ai", "backend", "infra", "unity"])
        self.assertEqual(plan["builds"], ["ai", "backend", "web"])
        self.assertTrue(plan["deploy"])

    def test_missing_base_history_uses_full_validation(self):
        plan = deployment_plan.create_plan(self.repository, "f" * 40, self.initial)

        self.assertEqual(plan["reason"], "missing-base-history")
        self.assertEqual(plan["builds"], ["ai", "backend", "web"])

    def test_unrelated_base_history_uses_full_validation(self):
        self.git("checkout", "--quiet", "--orphan", "unrelated")
        self.git("rm", "--quiet", "--cached", "-r", "--", ".")
        candidate = self.commit_file("README.md", "unrelated history\n")

        plan = deployment_plan.create_plan(self.repository, self.initial, candidate)

        self.assertEqual(plan["reason"], "base-not-ancestor")
        self.assertEqual(plan["builds"], ["ai", "backend", "web"])

    def test_deleted_runtime_file_still_selects_its_service(self):
        deployed = self.commit_file("ai/worker/app/obsolete.py", "old code\n")
        self.git("rm", "--", "ai/worker/app/obsolete.py")
        self.git("-c", "gc.auto=0", "commit", "--quiet", "-m", "remove obsolete code")

        plan = deployment_plan.create_plan(
            self.repository, deployed, self.git("rev-parse", "HEAD")
        )

        self.assertEqual(plan["builds"], ["ai"])

    def test_invalid_candidate_fails_instead_of_planning_another_revision(self):
        for candidate in ["--all", "dev", "f" * 40]:
            with self.subTest(candidate=candidate), self.assertRaises(ValueError):
                deployment_plan.create_plan(self.repository, self.initial, candidate)

    def test_command_line_writes_a_machine_readable_plan(self):
        output = self.repository / "plan.json"
        subprocess.run(
            [
                sys.executable,
                str(Path(deployment_plan.__file__).resolve()),
                "--repository",
                str(self.repository),
                "--candidate",
                self.initial,
                "--output",
                str(output),
            ],
            check=True,
        )

        plan = json.loads(output.read_text(encoding="utf-8"))
        self.assertEqual(plan["candidate_commit"], self.initial)
        self.assertEqual(plan["builds"], ["ai", "backend", "web"])


if __name__ == "__main__":
    unittest.main()
