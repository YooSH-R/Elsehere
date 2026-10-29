import json
import os
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import dev_pipeline


class ReadyDeploymentTests(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory(prefix="a305-ready-deployment-")
        self.addCleanup(directory.cleanup)
        self.repository = Path(directory.name)
        self.git("init", "--initial-branch=dev")
        self.git("config", "user.name", "Deployment test")
        self.git("config", "user.email", "deployment@example.invalid")
        self.git("remote", "add", "origin", str(self.repository))
        compose = self.repository / "infra/compose/compose.app.yaml"
        compose.parent.mkdir(parents=True)
        compose.write_text("services: {}\n", encoding="utf-8")
        self.base = self.commit("initial")
        self.candidate = self.commit("verified candidate")
        self.head = self.commit("newer unverified candidate")
        self.state = {
            "environment": "dev",
            "candidate_commit": self.base,
            "pipeline_id": 10,
            "images": {},
            "state_token": "initial-state",
            "minimum_backend_commit": self.base,
        }
        self.plan = dev_pipeline.create_release_plan(
            self.repository, self.state, self.candidate
        )

    def git(self, *arguments):
        return (
            subprocess.run(
                ["git", "-C", str(self.repository), *arguments],
                check=True,
                capture_output=True,
            )
            .stdout.decode()
            .strip()
        )

    def commit(self, message, path="docs/notes.md"):
        document = self.repository / path
        document.parent.mkdir(parents=True, exist_ok=True)
        document.write_text(message + "\n", encoding="utf-8")
        self.git("add", ".")
        self.git("commit", "-m", message)
        return self.git("rev-parse", "HEAD")

    def deliver(self, state=None):
        result = {"result": "unchanged", "candidate_commit": self.candidate}
        current_state = self.state if state is None else state
        self.operations = []

        def server_request(operation, *arguments, **options):
            self.operations.append(operation)
            if operation == "state":
                return current_state
            return result

        with (
            patch.dict(
                os.environ,
                {
                    "A305_DEPLOY_PLAN": json.dumps(self.plan),
                    "A305_DEPLOY_STATE": json.dumps(self.state),
                },
            ),
            patch("dev_pipeline.server_request", side_effect=server_request) as server,
        ):
            dev_pipeline.deliver_pipeline(self.repository, self.candidate, "dev", "123")
        return server

    def test_verified_candidate_can_deploy_while_newer_dev_commit_is_unverified(self):
        server = self.deliver()

        operations = [call.args[0] for call in server.call_args_list]
        self.assertIn("apply", operations)
        result = json.loads(
            (self.repository / "dist/deployment/result.json").read_text()
        )
        self.assertEqual(result["candidate_commit"], self.candidate)

    def test_candidate_older_than_the_running_release_is_not_applied(self):
        deployed = {**self.state, "candidate_commit": self.head}

        with self.assertRaises(dev_pipeline.SupersededCandidateError):
            self.deliver(deployed)

        self.assertNotIn("apply", self.operations)

    def test_delivery_refreshes_the_baseline_and_reuses_newly_deployed_images(self):
        deployed = self.commit("backend change", "backend/src/Example.java")
        self.candidate = self.commit("documentation after backend change")
        self.plan = dev_pipeline.create_release_plan(
            self.repository, self.state, self.candidate
        )
        current_state = {
            **self.state,
            "candidate_commit": deployed,
            "state_token": "new-deployment-state",
            "images": {"backend": {"reference": "newly-deployed-backend"}},
        }

        with patch("dev_pipeline.docker") as docker:
            self.deliver(current_state)

        manifest = json.loads(
            (self.repository / "dist/deployment/manifest.json").read_text()
        )
        self.assertIn("backend", self.plan["builds"])
        self.assertEqual(manifest["builds"], [])
        self.assertEqual(manifest["images"], current_state["images"])
        self.assertEqual(manifest["expected_state_token"], "new-deployment-state")
        docker.assert_not_called()

    def test_same_candidate_is_not_deployed_twice(self):
        deployed = {**self.state, "candidate_commit": self.candidate}

        with self.assertRaises(dev_pipeline.SupersededCandidateError):
            self.deliver(deployed)

        self.assertNotIn("apply", self.operations)

    def test_missing_checks_or_builds_block_delivery_after_baseline_refresh(self):
        self.candidate = self.commit("backend change", "backend/src/Example.java")
        required = dev_pipeline.create_release_plan(
            self.repository, self.state, self.candidate
        )
        for selection in ("checks", "builds"):
            with self.subTest(selection=selection):
                self.plan = {**required, selection: []}
                with (
                    patch("dev_pipeline.docker") as docker,
                    self.assertRaisesRegex(ValueError, "not verified"),
                ):
                    self.deliver()
                self.assertNotIn("apply", self.operations)
                docker.assert_not_called()

    def test_candidate_outside_dev_history_cannot_deploy(self):
        self.git("checkout", "--orphan", "unmerged")
        self.candidate = self.commit("unmerged candidate")
        self.plan["candidate_commit"] = self.candidate

        with self.assertRaisesRegex(ValueError, "branch history"):
            self.deliver()

        self.assertNotIn("apply", self.operations)

    def test_git_fetch_failure_is_an_error_instead_of_a_skipped_deployment(self):
        self.git("remote", "set-url", "origin", str(self.repository / "missing"))

        with self.assertRaisesRegex(RuntimeError, "branch history"):
            self.deliver()

        self.assertNotIn("apply", self.operations)


if __name__ == "__main__":
    unittest.main()
