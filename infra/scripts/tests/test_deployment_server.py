import io
import json
import tarfile
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
from urllib.parse import parse_qs, urlparse

from deployment_server import ComposeRuntime, DeploymentServer


class FakeRuntime:
    def __init__(self):
        self.images = {
            service: {
                "reference": f"a305/{service}:existing",
                "id": "sha256:" + str(index) * 64,
                "source_commit": "a" * 40,
            }
            for index, service in enumerate(["backend", "ai", "web"], start=1)
        }
        self.containers = {
            service: 1 for service in ["database", "backend", "ai", "web"]
        }
        self.fail_next_check = False
        self.health_failures = 0
        self.registry = {image["reference"]: image for image in self.images.values()}

    def snapshot(self):
        return {"images": self.images.copy(), "containers": self.containers.copy()}

    def validate(self, compose_file, images_file, previous_compose_file):
        return None

    def check(self, images):
        if self.health_failures:
            self.health_failures -= 1
            raise RuntimeError("post-deploy health failed")
        if self.fail_next_check:
            self.fail_next_check = False
            raise RuntimeError("post-deploy health failed")

    def load_images(self, archive):
        for image in json.loads(Path(archive).read_text(encoding="utf-8")).values():
            self.registry[image["reference"]] = image

    def up(self, compose_file, images_file, services):
        image_names = {"backend": "BACKEND_IMAGE", "ai": "AI_IMAGE", "web": "WEB_IMAGE"}
        references = dict(
            line.split("=", 1)
            for line in Path(images_file).read_text(encoding="utf-8").splitlines()
        )
        for service in services:
            if service == "ai-worker":
                service = "ai"
            self.images[service] = self.registry[references[image_names[service]]]
            self.containers[service] += 1


class DeploymentServerTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="a305-deploy-server-")
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.environment = self.root / "deploy.env"
        self.environment.write_text(
            "DB_PASSWORD=server-only-test-secret\nMONITORING_PASSWORD=private-test\n",
            encoding="utf-8",
        )
        self.compose = self.root / "compose.app.yaml"
        self.environment.chmod(0o600)
        self.compose.write_text("services: {}\n", encoding="utf-8")
        self.overlay = self.root / "compose.observability.yaml"
        self.overlay.write_text("services: {}\n", encoding="utf-8")
        self.config = {
            "directory": str(self.root / "deployments"),
            "environment_file": str(self.environment),
            "initial_compose_file": str(self.compose),
            "overlays": [str(self.overlay)],
            "minimum_backend_commit": "b" * 40,
            "bootstrap_sources": {name: "a" * 40 for name in ["backend", "ai", "web"]},
        }
        self.runtime = FakeRuntime()
        self.server = DeploymentServer(self.config, self.runtime)

    def test_initialization_keeps_mixed_manual_images_without_claiming_a_ci_success(
        self,
    ):
        result = self.server.initialize()

        self.assertIsNone(result["candidate_commit"])
        self.assertEqual(result["pipeline_id"], 0)
        self.assertEqual(
            result["images"]["backend"]["reference"], "a305/backend:existing"
        )
        self.assertEqual(result["minimum_backend_commit"], "b" * 40)
        self.assertNotIn("server-only-test-secret", json.dumps(result))
        self.assertNotIn("private-test", json.dumps(result))
        self.assertFalse((self.root / "deployments/last-success.json").exists())

    def release_archive(
        self, builds=None, pipeline_id=10, candidate="c" * 40, branch="dev"
    ):
        builds = ["backend"] if builds is None else builds
        state = self.server.state()
        images = {
            name: {
                "reference": f"a305/{name}:{candidate}",
                "id": "sha256:" + "4" * 64,
                "source_commit": candidate,
            }
            if name in builds
            else image
            for name, image in state["images"].items()
        }
        manifest = {
            "schema_version": 1,
            "candidate_commit": candidate,
            "pipeline_id": pipeline_id,
            "branch": branch,
            "expected_state_token": state["state_token"],
            "minimum_backend_commit": state["minimum_backend_commit"],
            "builds": builds,
            "configuration_changed": False,
            "images": images,
        }
        files = {
            "manifest.json": json.dumps(manifest).encode(),
            "compose.app.yaml": b"services: {}\n",
        }
        if builds:
            files["images.tar.gz"] = json.dumps(images).encode()
        output = io.BytesIO()
        with tarfile.open(fileobj=output, mode="w") as archive:
            for name, content in files.items():
                member = tarfile.TarInfo(name)
                member.size = len(content)
                archive.addfile(member, io.BytesIO(content))
        output.seek(0)
        return output

    def test_failed_health_restores_images_and_keeps_secrets_and_success_record(self):
        self.server.initialize()
        original_images = self.runtime.images.copy()
        original_environment = self.environment.read_bytes()
        original_overlay = self.overlay.read_bytes()
        archive = self.release_archive()
        self.runtime.fail_next_check = True

        with self.assertRaisesRegex(RuntimeError, "post-deploy"):
            self.server.apply(archive)

        self.assertEqual(self.runtime.images, original_images)
        self.assertEqual(self.environment.read_bytes(), original_environment)
        self.assertEqual(self.overlay.read_bytes(), original_overlay)
        self.assertFalse((self.root / "deployments/last-success.json").exists())
        self.assertEqual(self.runtime.containers["database"], 1)
        self.assertEqual(self.runtime.containers["ai"], 1)
        self.assertEqual(self.runtime.containers["web"], 1)

    def test_missing_branch_cannot_use_an_unconfigured_pilot(self):
        self.server.initialize()

        with self.assertRaisesRegex(ValueError, "Only dev"):
            self.server.apply(self.release_archive(branch=None))

        self.assertFalse((self.root / "deployments/last-success.json").exists())

    def test_backend_success_keeps_other_containers_and_monitoring_secrets(self):
        self.server.initialize()
        overlay = self.overlay.read_bytes()

        result = self.server.apply(self.release_archive())

        self.assertEqual(result["candidate_commit"], "c" * 40)
        self.assertEqual(self.server.state()["pipeline_id"], 10)
        self.assertEqual(self.runtime.containers["backend"], 2)
        for service in ["database", "ai", "web"]:
            self.assertEqual(self.runtime.containers[service], 1)
        self.assertIn(
            "MONITORING_PASSWORD=private-test\n", self.environment.read_text()
        )
        self.assertIn(
            "DB_PASSWORD=server-only-test-secret\n", self.environment.read_text()
        )
        self.assertIn(
            "BACKEND_IMAGE=a305/backend:" + "c" * 40, self.environment.read_text()
        )
        self.assertEqual(self.overlay.read_bytes(), overlay)

    def test_document_only_success_reuses_every_image_without_restarting_containers(
        self,
    ):
        self.server.initialize()
        images = self.runtime.images.copy()
        containers = self.runtime.containers.copy()

        result = self.server.apply(self.release_archive(builds=[]))

        self.assertEqual(result["result"], "unchanged")
        self.assertEqual(result["images"], images)
        self.assertEqual(self.runtime.containers, containers)
        self.assertEqual(self.server.state()["candidate_commit"], "c" * 40)

    def test_older_queued_pipeline_cannot_overwrite_a_new_success(self):
        self.server.initialize()
        older = self.release_archive(pipeline_id=10)
        self.server.apply(self.release_archive(pipeline_id=11, candidate="d" * 40))
        images = self.runtime.images.copy()

        with self.assertRaisesRegex(ValueError, "outdated"):
            self.server.apply(older)

        self.assertEqual(self.runtime.images, images)
        self.assertEqual(self.server.state()["candidate_commit"], "d" * 40)

    def test_monitoring_overlay_change_invalidates_an_already_planned_release(self):
        self.server.initialize()
        archive = self.release_archive()
        self.overlay.write_text("services: {backend: {environment: {NEW: value}}}\n")

        with self.assertRaisesRegex(ValueError, "state changed"):
            self.server.apply(archive)

        self.assertEqual(self.runtime.containers["backend"], 1)
        self.assertFalse((self.root / "deployments/last-success.json").exists())

    def test_failed_release_can_be_retried_and_only_then_records_success(self):
        self.server.initialize()
        self.runtime.fail_next_check = True
        with self.assertRaises(RuntimeError):
            self.server.apply(self.release_archive())

        result = self.server.apply(self.release_archive())

        self.assertEqual(result["result"], "deployed")
        self.assertEqual(self.server.state()["candidate_commit"], "c" * 40)
        self.assertEqual(self.runtime.containers["database"], 1)

    def test_bundle_path_traversal_is_rejected_before_any_container_change(self):
        self.server.initialize()
        output = io.BytesIO()
        with tarfile.open(fileobj=output, mode="w") as archive:
            member = tarfile.TarInfo("../outside")
            member.size = 4
            archive.addfile(member, io.BytesIO(b"test"))
        output.seek(0)

        with self.assertRaisesRegex(ValueError, "bundle member"):
            self.server.apply(output)

        self.assertFalse((self.root / "deployments/outside").exists())
        self.assertEqual(self.runtime.containers["backend"], 1)

    def test_rollback_health_failure_records_manual_recovery_without_a_new_success(
        self,
    ):
        self.server.initialize()
        self.server.apply(self.release_archive())
        success_file = self.root / "deployments/last-success.json"
        previous_success = success_file.read_bytes()
        self.runtime.health_failures = 2

        with self.assertRaisesRegex(RuntimeError, "manual recovery"):
            self.server.apply(self.release_archive(pipeline_id=11, candidate="d" * 40))

        failure = json.loads((self.root / "deployments/last-failure.json").read_text())
        self.assertFalse(failure["restored"])
        self.assertEqual(success_file.read_bytes(), previous_success)

    def test_record_write_failure_does_not_prevent_restoring_the_running_images(self):
        self.server.initialize()
        original_images = self.runtime.images.copy()
        original_environment = self.environment.read_bytes()
        archive = self.release_archive()

        with (
            patch(
                "deployment_server.write_json",
                side_effect=OSError("record storage unavailable"),
            ),
            self.assertRaises(OSError),
        ):
            self.server.apply(archive)

        self.assertEqual(self.runtime.images, original_images)
        self.assertEqual(self.environment.read_bytes(), original_environment)
        self.assertFalse((self.root / "deployments/last-success.json").exists())


class MonitoringReadinessTests(unittest.TestCase):
    def check_runtime(self, scrape_time, up=1):
        images = {
            name: {"reference": "test", "id": "test", "source_commit": "a" * 40}
            for name in ["backend", "ai", "web"]
        }
        runtime = ComposeRuntime(
            {"public_health_url": "https://example.test/api/health"}
        )
        runtime.health_after = 100

        def response(url, timeout):
            if "/api/v1/query?" in url:
                query = parse_qs(urlparse(url).query)["query"][0]
                if query.startswith("timestamp("):
                    samples = [{"value": [200, str(scrape_time)]}] if up == 1 else []
                else:
                    samples = [{"value": [200, str(up)]}]
                payload = {"data": {"result": samples}}
            elif url.endswith("/version.json"):
                payload = {"commit": "a" * 40, "build_type": "unity-webgl"}
            else:
                payload = {"status": "UP"}
            return io.BytesIO(json.dumps(payload).encode())

        with (
            patch.object(
                runtime,
                "inspect",
                return_value={"health": "healthy", "reference": "test", "id": "test"},
            ),
            patch.object(runtime, "docker", return_value="401"),
            patch("deployment_server.urlopen", side_effect=response),
        ):
            runtime.check_once(images)

    def test_old_successful_scrape_cannot_confirm_a_new_deployment(self):
        with self.assertRaisesRegex(RuntimeError, "Prometheus"):
            self.check_runtime(scrape_time=90)

    def test_fresh_successful_scrape_confirms_monitoring_after_deployment(self):
        self.check_runtime(scrape_time=110)

    def test_fresh_failed_scrape_cannot_confirm_monitoring(self):
        with self.assertRaisesRegex(RuntimeError, "Prometheus"):
            self.check_runtime(scrape_time=110, up=0)


if __name__ == "__main__":
    unittest.main()
