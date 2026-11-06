import tomllib
import unittest
from pathlib import Path

import yaml

REPOSITORY = Path(__file__).resolve().parents[3]


class RunnerConfigurationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.jobs = yaml.load(
            (REPOSITORY / ".gitlab-ci.yml").read_text(), Loader=yaml.BaseLoader
        )
        for path in (REPOSITORY / ".gitlab/ci").glob("*.yml"):
            cls.jobs.update(yaml.load(path.read_text(), Loader=yaml.BaseLoader))

    def tags_for(self, name):
        job = self.jobs[name]
        tags = []
        parents = job.get("extends", [])
        if isinstance(parents, str):
            parents = [parents]
        for parent in parents:
            inherited = self.tags_for(parent)
            if inherited:
                tags = inherited
        return job.get("tags", tags)

    def test_general_checks_can_run_on_both_linux_hosts(self):
        for name in (
            "backend:check-and-build",
            "ai:check-and-test",
            "infra:compose-and-nginx",
            "infra:observability",
            "infra:app-runtime",
        ):
            with self.subTest(job=name):
                self.assertEqual(self.tags_for(name), ["a305-linux-ci"])

    def test_all_deployment_commands_require_an_ec2_runner(self):
        for name in ("dev:plan", "prod:plan", "dev:deliver", "prod:deliver"):
            with self.subTest(job=name):
                self.assertIn("a305-ec2", self.tags_for(name))
        self.assertIn("a305-linux-light", self.tags_for("dev:plan"))
        self.assertIn("a305-linux-light", self.tags_for("prod:plan"))

    def test_new_dev_commit_cancels_obsolete_parent_and_child_checks(self):
        workflow = self.jobs["workflow"]
        self.assertEqual(workflow["auto_cancel"]["on_new_commit"], "none")

        dev_parent_rule = next(
            rule
            for rule in workflow["rules"]
            if rule["if"] == '$CI_PIPELINE_SOURCE == "push" && $CI_COMMIT_BRANCH == "dev"'
        )
        dev_child_rule = next(
            rule
            for rule in workflow["rules"]
            if rule["if"]
            == '$CI_PIPELINE_SOURCE == "parent_pipeline" && $A305_DEPLOY_CHILD == "true" && $A305_DEPLOY_ENVIRONMENT == "dev"'
        )
        self.assertEqual(dev_parent_rule["auto_cancel"]["on_new_commit"], "interruptible")
        self.assertEqual(dev_child_rule["auto_cancel"]["on_new_commit"], "interruptible")
        self.assertEqual(self.jobs["dev:child"]["interruptible"], "true")
        child_fallback_rule = next(
            rule
            for rule in workflow["rules"]
            if rule["if"] == '$CI_PIPELINE_SOURCE == "parent_pipeline" && $A305_DEPLOY_CHILD == "true"'
        )
        branch_fallback_rule = next(
            rule
            for rule in workflow["rules"]
            if rule["if"] == '$CI_COMMIT_BRANCH == "dev" || $CI_COMMIT_BRANCH == "main"'
        )
        self.assertLess(
            workflow["rules"].index(dev_child_rule), workflow["rules"].index(child_fallback_rule)
        )
        self.assertLess(
            workflow["rules"].index(dev_parent_rule), workflow["rules"].index(branch_fallback_rule)
        )

    def test_running_deployment_and_production_pipeline_remain_protected(self):
        self.assertEqual(self.jobs["dev:deliver"]["interruptible"], "false")
        self.assertEqual(self.jobs["prod:child"]["interruptible"], "false")
        self.assertEqual(self.jobs["prod:deliver"]["interruptible"], "false")

        workflow = self.jobs["workflow"]
        production_child_rule = next(
            rule
            for rule in workflow["rules"]
            if rule["if"] == '$CI_PIPELINE_SOURCE == "parent_pipeline" && $A305_DEPLOY_CHILD == "true"'
        )
        branch_rule = next(
            rule
            for rule in workflow["rules"]
            if rule["if"] == '$CI_COMMIT_BRANCH == "dev" || $CI_COMMIT_BRANCH == "main"'
        )
        self.assertNotIn("auto_cancel", production_child_rule)
        self.assertNotIn("auto_cancel", branch_rule)

    def test_laptop_accepts_two_jobs_and_keeps_the_host_socket_out_of_jobs(self):
        path = REPOSITORY / "infra/runner/laptop.config.template.toml"
        config = tomllib.loads(path.read_text())
        self.assertEqual(config["concurrent"], 2)
        self.assertEqual(len(config["runners"]), 1)
        runner = config["runners"][0]
        self.assertEqual(runner["limit"], 2)
        self.assertEqual(runner["executor"], "docker")
        self.assertFalse(runner["docker"]["privileged"])
        self.assertEqual(runner["docker"]["volumes"], ["/cache"])
        self.assertEqual(runner["docker"]["services_limit"], 1)

    def test_laptop_nested_network_avoids_docker_desktop_subnet(self):
        path = REPOSITORY / "infra/runner/laptop.config.template.toml"
        runner = tomllib.loads(path.read_text())["runners"][0]
        self.assertIn(
            "DOCKERD_ROOTLESS_ROOTLESSKIT_NET=slirp4netns", runner["environment"]
        )


if __name__ == "__main__":
    unittest.main()
