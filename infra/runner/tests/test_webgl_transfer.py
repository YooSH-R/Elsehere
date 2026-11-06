import unittest
from pathlib import Path

import yaml


class WebglTransferConfigurationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        repository = Path(__file__).resolve().parents[3]
        cls.jobs = yaml.load(
            (repository / ".gitlab/ci/unity.yml").read_text(), Loader=yaml.BaseLoader
        )

    def test_webgl_files_use_packages_instead_of_the_limited_artifact_archive(self):
        job = self.jobs["unity:editor-and-webgl"]
        self.assertNotIn("dist/webgl/", job["artifacts"]["paths"])
        self.assertIn("dist/webgl-package.json", job["artifacts"]["paths"])
        self.assertIn("publish-webgl-package.ps1", " ".join(job["script"]))

    def test_cleanup_runs_after_delivery_even_when_a_check_fails(self):
        cleanup = self.jobs["unity:package-cleanup"]
        self.assertEqual(cleanup["when"], "always")
        needs = {need["job"]: need for need in cleanup["needs"]}
        self.assertEqual(needs["unity:editor-and-webgl"]["artifacts"], "true")
        for delivery in ("dev:deliver", "prod:deliver"):
            self.assertEqual(needs[delivery]["optional"], "true")
