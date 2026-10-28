import json
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import dev_pipeline
from dev_pipeline import create_child_pipeline, create_release_plan, server_request


class ChildPipelineTests(unittest.TestCase):
    def test_production_child_always_runs_all_release_checks_and_only_prod_delivery(
        self,
    ):
        pipeline = create_child_pipeline(
            {"checks": [], "builds": [], "deploy": False}, "123", environment="prod"
        )

        self.assertNotIn("dev:deliver", pipeline)
        jobs = {need["job"] for need in pipeline["prod:deliver"]["needs"]}
        self.assertTrue(
            {
                "backend:check-and-build",
                "unity:editor-and-webgl",
                "ai:check-and-test",
                "infra:app-runtime",
                "infra:observability",
            }.issubset(jobs)
        )
        self.assertEqual(pipeline["variables"]["A305_DEPLOY_ENVIRONMENT"], "prod")
        self.assertEqual(pipeline["variables"]["A305_CHECK_INFRA"], "true")

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

    def test_backend_and_infrastructure_releases_wait_for_monitoring_checks(self):
        for area in ["backend", "infra"]:
            with self.subTest(area=area):
                pipeline = create_child_pipeline(
                    {"checks": [area], "builds": [], "deploy": False}, "123"
                )

                jobs = [need["job"] for need in pipeline["dev:deliver"]["needs"]]
                self.assertEqual(jobs.count("infra:observability"), 1)

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


class ServerRequestTests(unittest.TestCase):
    def test_production_ssh_uses_separate_protected_file_variables(self):
        with tempfile.TemporaryDirectory() as directory:
            key = Path(directory) / "key"
            hosts = Path(directory) / "hosts"
            key.write_text("fixture-key")
            hosts.write_text("fixture-hosts")
            with patch.dict(
                "os.environ",
                {
                    "PROD_DEPLOY_SSH_KEY": str(key),
                    "PROD_DEPLOY_KNOWN_HOSTS": str(hosts),
                },
                clear=True,
            ):
                command = dev_pipeline.ssh_command("prod")
            self.assertIn(str(key), command)
            self.assertIn(f"UserKnownHostsFile={hosts}", command)

    def test_ssh_failures_explain_the_category_without_revealing_stderr(self):
        cases = [
            (b'Load key "/tmp/deploy-key": error in libcrypto', "private key format"),
            (b"Host key verification failed.", "host key verification"),
            (b"Permission denied (publickey).", "authentication"),
            (b"Could not resolve hostname example.invalid", "hostname resolution"),
            (
                b"ssh: connect to host example.invalid port 22: Connection refused",
                "connection refused",
            ),
            (
                b"ssh: connect to host example.invalid port 22: Connection timed out",
                "connection timed out",
            ),
            (b"Unexpected SSH failure", "Deploy SSH connection failed"),
        ]
        for stderr, expected in cases:
            with self.subTest(expected=expected):
                result = subprocess.CompletedProcess(
                    ["ssh"], 255, stdout=b"", stderr=stderr + b"\nfixture-secret-value"
                )
                with (
                    patch("dev_pipeline.ssh_command", return_value=["ssh"]),
                    patch("dev_pipeline.subprocess.run", return_value=result),
                    self.assertRaises(RuntimeError) as raised,
                ):
                    server_request("state")

                message = str(raised.exception)
                self.assertIn(expected, message)
                self.assertNotIn("fixture-secret-value", message)
                self.assertNotIn("/tmp/deploy-key", message)

    def test_successful_state_json_is_returned(self):
        result = subprocess.CompletedProcess(
            ["ssh"], 0, stdout=b'{"candidate_commit": null}', stderr=b""
        )
        with (
            patch("dev_pipeline.ssh_command", return_value=["ssh"]),
            patch("dev_pipeline.subprocess.run", return_value=result),
        ):
            self.assertEqual(server_request("state"), {"candidate_commit": None})

    def test_server_json_error_keeps_the_server_failure_message(self):
        result = subprocess.CompletedProcess(
            ["ssh"],
            1,
            stdout=b'{"error": "Deployment state is already initialized."}',
            stderr=b"",
        )
        with (
            patch("dev_pipeline.ssh_command", return_value=["ssh"]),
            patch("dev_pipeline.subprocess.run", return_value=result),
            self.assertRaisesRegex(RuntimeError, "already initialized"),
        ):
            server_request("state")


class DevelopmentEntryPointTests(unittest.TestCase):
    def test_protected_dev_can_plan_a_development_release(self):
        environment = {
            "CI_COMMIT_BRANCH": "dev",
            "CI_COMMIT_REF_PROTECTED": "true",
            "CI_COMMIT_SHA": "a" * 40,
            "CI_PROJECT_DIR": str(Path.cwd()),
            "CI_PIPELINE_ID": "123",
            "CI_PIPELINE_SOURCE": "push",
        }
        with (
            patch("sys.argv", ["dev_pipeline.py", "plan"]),
            patch.dict("os.environ", environment, clear=True),
            patch("dev_pipeline.plan_pipeline") as plan,
        ):
            dev_pipeline.main()

        plan.assert_called_once()
        self.assertEqual(plan.call_args.args[2], "dev")
        self.assertEqual(plan.call_args.args[-1], "dev")

    def test_retired_deployment_branch_is_rejected_before_planning(self):
        environment = {
            "CI_COMMIT_BRANCH": "chore/S15P31A305-37-server-environments",
            "CI_COMMIT_REF_PROTECTED": "true",
            "CI_COMMIT_SHA": "a" * 40,
            "CI_PROJECT_DIR": str(Path.cwd()),
            "CI_PIPELINE_ID": "123",
            "CI_PIPELINE_SOURCE": "web",
        }
        with (
            patch("sys.argv", ["dev_pipeline.py", "plan"]),
            patch.dict("os.environ", environment, clear=True),
            patch("dev_pipeline.plan_pipeline") as plan,
            self.assertRaisesRegex(ValueError, "protected deployment branch"),
        ):
            dev_pipeline.main()

        plan.assert_not_called()


class ProductionEntryPointTests(unittest.TestCase):
    def test_automatic_delivery_requires_its_protected_production_child(self):
        environment = {
            "CI_COMMIT_BRANCH": "main",
            "CI_COMMIT_REF_PROTECTED": "true",
            "CI_COMMIT_SHA": "a" * 40,
            "CI_PROJECT_DIR": str(Path.cwd()),
            "CI_PIPELINE_ID": "124",
            "CI_PIPELINE_SOURCE": "parent_pipeline",
            "A305_DEPLOY_CHILD": "true",
            "A305_DEPLOY_ENVIRONMENT": "prod",
            "A305_PROD_AUTOMATIC": "true",
        }
        with (
            patch("sys.argv", ["dev_pipeline.py", "deliver", "--environment", "prod"]),
            patch.dict("os.environ", environment, clear=True),
            patch("dev_pipeline.deliver_pipeline") as deliver,
        ):
            dev_pipeline.main()
            self.assertEqual(deliver.call_args.args[-1], "prod")
            for override in (
                {"CI_COMMIT_REF_PROTECTED": "false"},
                {"CI_COMMIT_BRANCH": "dev"},
                {"CI_PIPELINE_SOURCE": "merge_request_event"},
                {"CI_PIPELINE_SOURCE": "push"},
                {"CI_PIPELINE_SOURCE": "web"},
                {"A305_DEPLOY_CHILD": "false"},
                {"A305_DEPLOY_ENVIRONMENT": "dev"},
                {"A305_PROD_AUTOMATIC": "false"},
            ):
                with (
                    self.subTest(override=override),
                    patch.dict("os.environ", override),
                    self.assertRaises(ValueError),
                ):
                    dev_pipeline.main()
            self.assertEqual(deliver.call_count, 1)

    def test_automatic_plan_preserves_its_mode_in_the_generated_child(self):
        with tempfile.TemporaryDirectory(prefix="a305-auto-plan-") as directory:
            repository = Path(directory)
            with (
                patch("dev_pipeline.verify_current_candidate"),
                patch(
                    "dev_pipeline.server_request", return_value={"environment": "prod"}
                ),
                patch(
                    "dev_pipeline.create_release_plan",
                    return_value={
                        "checks": [],
                        "builds": [],
                        "deploy": False,
                        "reason": "cumulative-diff",
                    },
                ),
            ):
                dev_pipeline.plan_pipeline(
                    repository, "a" * 40, "main", "123", "prod", automatic=True
                )
            child = json.loads(
                (repository / "dist/deployment/prod-child.yml").read_text()
            )

        self.assertEqual(child["variables"]["A305_PROD_AUTOMATIC"], "true")
        self.assertEqual(child["variables"]["A305_CHECK_BACKEND"], "true")
        self.assertEqual(child["variables"]["A305_CHECK_UNITY"], "true")

    def test_main_push_plans_production_without_manual_release_variables(self):
        environment = {
            "CI_COMMIT_BRANCH": "main",
            "CI_COMMIT_REF_PROTECTED": "true",
            "CI_COMMIT_SHA": "a" * 40,
            "CI_PROJECT_DIR": str(Path.cwd()),
            "CI_PIPELINE_ID": "123",
            "CI_PIPELINE_SOURCE": "push",
        }
        with (
            patch("sys.argv", ["dev_pipeline.py", "plan", "--environment", "prod"]),
            patch.dict("os.environ", environment, clear=True),
            patch("dev_pipeline.plan_pipeline") as plan,
        ):
            dev_pipeline.main()

        self.assertEqual(plan.call_args.args[-1], "prod")
        self.assertTrue(plan.call_args.kwargs["automatic"])

    def test_protected_main_requires_evidence_for_the_exact_candidate(self):
        environment = {
            "CI_PIPELINE_SOURCE": "web",
            "CI_COMMIT_BRANCH": "main",
            "CI_COMMIT_REF_PROTECTED": "true",
            "CI_COMMIT_SHA": "a" * 40,
            "CI_PROJECT_DIR": str(Path.cwd()),
            "CI_PIPELINE_ID": "123",
            "PROD_VERIFIED_COMMIT": "a" * 40,
            "PROD_RELEASE_MR": "https://lab.ssafy.com/s15-final/S15P31A305/-/merge_requests/123",
            "PROD_DB_COMPATIBLE": "true",
        }
        with (
            patch("sys.argv", ["dev_pipeline.py", "plan", "--environment", "prod"]),
            patch.dict("os.environ", environment, clear=True),
            patch("dev_pipeline.plan_pipeline") as plan,
        ):
            dev_pipeline.main()
            self.assertEqual(plan.call_args.args[-1], "prod")
            for override in (
                {"PROD_VERIFIED_COMMIT": "b" * 40},
                {"PROD_RELEASE_MR": ""},
                {"PROD_DB_COMPATIBLE": "false"},
                {"CI_COMMIT_BRANCH": "dev"},
                {"CI_COMMIT_REF_PROTECTED": "false"},
                {"CI_PIPELINE_SOURCE": "schedule"},
                {"CI_PIPELINE_SOURCE": "merge_request_event"},
                {"CI_PIPELINE_SOURCE": "parent_pipeline"},
            ):
                with (
                    self.subTest(override=override),
                    patch.dict("os.environ", override),
                    self.assertRaises(ValueError),
                ):
                    dev_pipeline.main()

    def test_manual_production_child_keeps_exact_candidate_evidence_checks(self):
        environment = {
            "CI_PIPELINE_SOURCE": "parent_pipeline",
            "CI_COMMIT_BRANCH": "main",
            "CI_COMMIT_REF_PROTECTED": "true",
            "CI_COMMIT_SHA": "a" * 40,
            "CI_PROJECT_DIR": str(Path.cwd()),
            "CI_PIPELINE_ID": "124",
            "A305_DEPLOY_CHILD": "true",
            "A305_DEPLOY_ENVIRONMENT": "prod",
            "A305_PROD_AUTOMATIC": "false",
            "PROD_VERIFIED_COMMIT": "a" * 40,
            "PROD_RELEASE_MR": "https://lab.ssafy.com/s15-final/S15P31A305/-/merge_requests/123",
            "PROD_DB_COMPATIBLE": "true",
        }
        with (
            patch("sys.argv", ["dev_pipeline.py", "deliver", "--environment", "prod"]),
            patch.dict("os.environ", environment, clear=True),
            patch("dev_pipeline.deliver_pipeline") as deliver,
        ):
            dev_pipeline.main()
            for override in (
                {"PROD_VERIFIED_COMMIT": "b" * 40},
                {"PROD_RELEASE_MR": ""},
                {"PROD_DB_COMPATIBLE": "false"},
            ):
                with (
                    self.subTest(override=override),
                    patch.dict("os.environ", override),
                    self.assertRaises(ValueError),
                ):
                    dev_pipeline.main()
            self.assertEqual(deliver.call_count, 1)


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
