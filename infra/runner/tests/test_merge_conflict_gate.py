import unittest
from pathlib import Path

import yaml

REPOSITORY = Path(__file__).resolve().parents[3]


class MergeConflictGateTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.jobs = yaml.load(
            (REPOSITORY / ".gitlab-ci.yml").read_text(), Loader=yaml.BaseLoader
        )
        for path in (REPOSITORY / ".gitlab/ci").glob("*.yml"):
            cls.jobs.update(yaml.load(path.read_text(), Loader=yaml.BaseLoader))

    def resolve_job(self, name):
        job = self.jobs[name]
        parents = job.get("extends", [])
        if isinstance(parents, str):
            parents = [parents]
        resolved = {}
        for parent in parents:
            resolved.update(self.resolve_job(parent))
        resolved.update(job)
        return resolved

    def waits_for_gate(self, name):
        if name == "ci:merge-conflicts":
            return True
        job = self.resolve_job(name)
        if job.get("when", "on_success") != "on_success":
            return False
        if "needs" not in job:
            return job.get("stage", "test") not in (".pre", ".post")
        return any(
            self.waits_for_gate(need if isinstance(need, str) else need["job"])
            for need in job["needs"]
            if isinstance(need, str) or need.get("optional") != "true"
        )

    def test_gate_runs_first_and_only_for_merge_requests(self):
        gate = self.resolve_job("ci:merge-conflicts")
        self.assertEqual(gate["stage"], ".pre")
        self.assertEqual(gate["tags"], ["a305-linux-light"])
        self.assertEqual(gate["allow_failure"], "false")
        self.assertEqual(
            gate["rules"], [{"if": '$CI_PIPELINE_SOURCE == "merge_request_event"'}]
        )
        self.assertEqual(gate["variables"]["GIT_DEPTH"], "0")
        self.assertEqual(gate["variables"]["GIT_CHECKOUT"], "false")

    def test_all_possible_mr_jobs_wait_for_conflict_check(self):
        self.assertIn("ci:merge-conflicts", self.jobs)
        checked = []
        for name, job in self.jobs.items():
            if name.startswith(".") or not isinstance(job, dict):
                continue
            resolved = self.resolve_job(name)
            if "script" not in resolved and "trigger" not in resolved:
                continue
            rules = resolved.get("rules")
            runs_for_mr = rules is None or any(
                isinstance(rule, dict)
                and '== "merge_request_event"' in rule.get("if", "")
                and rule.get("when") != "never"
                for rule in rules
            )
            if runs_for_mr:
                checked.append(name)
                with self.subTest(job=name):
                    self.assertTrue(self.waits_for_gate(name), name)
        self.assertIn("unity:editor-tests", checked)
        self.assertIn("backend:check-and-build", checked)
        self.assertIn("infra:app-runtime", checked)


if __name__ == "__main__":
    unittest.main()
