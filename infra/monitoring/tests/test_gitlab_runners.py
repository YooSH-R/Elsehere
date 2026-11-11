"""Check the runner exporter's observable metrics without contacting GitLab."""

import importlib.util
import json
import unittest
from pathlib import Path


EXPORTER_PATH = Path(__file__).resolve().parents[1] / "gitlab_runners.py"
specification = importlib.util.spec_from_file_location("gitlab_runners", EXPORTER_PATH)
exporter = importlib.util.module_from_spec(specification)
specification.loader.exec_module(exporter)


class RunnerMetricsTests(unittest.TestCase):
    def test_dashboard_and_runner_configuration_use_the_same_categories(self):
        monitoring = EXPORTER_PATH.parent
        hosts = json.loads((monitoring / "runner-hosts.json").read_text(encoding="utf-8"))
        dashboard_path = monitoring / "grafana" / "dashboards" / "runners.json"
        dashboard = json.loads(dashboard_path.read_text(encoding="utf-8"))
        team_panel = next(panel for panel in dashboard["panels"] if panel["id"] == 3)

        self.assertEqual(hosts["a305-linux-ci"], "EC2")
        self.assertEqual(hosts["a305-YSH-linux-ci"], "팀원 CI")
        self.assertEqual(hosts["a305-unity-YSH"], "팀원 Unity")
        self.assertEqual(hosts["a305-junghoon-linux-ci"], "팀원 CI")
        self.assertEqual(hosts["a305-unity-sjh"], "팀원 Unity")
        self.assertEqual(hosts["a305-unity-박민혁"], "팀원 Unity")
        self.assertIn("팀원 CI|팀원 Unity", team_panel["targets"][0]["expr"])

    def test_runner_classes_and_active_jobs_are_reported(self):
        runners = [
            {"id": 1, "description": "a305-linux-ci", "status": "online"},
            {"id": 2, "description": "a305-unity-YSH", "status": "offline"},
            {"id": 3, "description": "unknown", "status": "online"},
        ]
        jobs = [
            {"id": 10, "name": "build", "status": "running", "runner": runners[0]},
            {"id": 11, "name": "waiting", "status": "pending", "runner": None},
        ]
        hosts = {"a305-linux-ci": "EC2", "a305-unity-YSH": "팀원 Unity"}

        result = exporter.render_metrics(runners, jobs, hosts, now=100)

        self.assertIn('runner="a305-linux-ci",host="EC2"', result)
        self.assertIn('runner="a305-unity-YSH",host="팀원 Unity",status="offline"', result)
        self.assertIn('runner="unknown",host="미분류"', result)
        self.assertIn('a305_gitlab_pending_jobs 1', result)
        self.assertIn('job_id="10",job_name="build"', result)

    def test_failed_collection_does_not_publish_stale_success(self):
        class UnavailableGitLab:
            def get_list(self, *args, **kwargs):
                raise OSError("private upstream detail")

        result = exporter.collect_metrics(UnavailableGitLab(), {})

        self.assertEqual(result, "a305_gitlab_scrape_success 0\n")
        self.assertNotIn("private upstream detail", result)


if __name__ == "__main__":
    unittest.main()
