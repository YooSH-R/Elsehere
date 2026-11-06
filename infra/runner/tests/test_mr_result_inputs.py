import sys
import unittest
from pathlib import Path

import yaml

REPOSITORY = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(REPOSITORY / "infra/scripts"))
import mr_results


class MergeRequestInputCoverageTests(unittest.TestCase):
    def test_every_mr_selection_input_invalidates_a_reused_check(self):
        jobs = {}
        for path in (REPOSITORY / ".gitlab/ci").glob("*.yml"):
            jobs.update(yaml.load(path.read_text(), Loader=yaml.BaseLoader))
        for name, inputs in mr_results.JOB_INPUTS.items():
            rules = [rule for rule in jobs[name]["rules"] if isinstance(rule, dict)]
            patterns = next(
                rule["changes"]["paths"]
                for rule in rules
                if '"merge_request_event"' in rule.get("if", "")
            )
            for pattern in patterns:
                # A conservative directory input must cover the literal prefix of each CI glob.
                prefix = pattern.split("*", 1)[0].rstrip("/")
                with self.subTest(job=name, path=pattern):
                    self.assertTrue(
                        any(
                            prefix == entry.rstrip("/")
                            or prefix.startswith(entry.rstrip("/") + "/")
                            for entry in (*mr_results.COMMON_INPUTS, *inputs)
                        )
                    )
