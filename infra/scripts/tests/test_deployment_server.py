import gzip
import io
import json
import sys
import tarfile
import tempfile
import unittest
from copy import deepcopy
from pathlib import Path
from unittest.mock import Mock, patch
from urllib.parse import parse_qs, urlparse

import deployment_server
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

    def retain_images(self, archive, images):
        Path(archive).write_text(json.dumps(images), encoding="utf-8")

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
            "DB_PASSWORD=server-only-test-secret\nMONITORING_PASSWORD=private-test\n"
            "GOOGLE_CLIENT_ID=dev-test.apps.googleusercontent.com\n"
            "GOOGLE_CLIENT_SECRET=server-only-google-test-secret\n",
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
        self.assertNotIn("server-only-google-test-secret", json.dumps(result))
        self.assertFalse((self.root / "deployments/last-success.json").exists())

    def release_archive(
        self,
        builds=None,
        pipeline_id=10,
        candidate="c" * 40,
        branch="dev",
        environment="dev",
        ready_deployment=False,
        verify_database_backup=None,
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
            "environment": environment,
            "expected_state_token": state["state_token"],
            "minimum_backend_commit": state["minimum_backend_commit"],
            "builds": builds,
            "configuration_changed": False,
            "images": images,
        }
        if ready_deployment:
            manifest["verified_base_commit"] = state["candidate_commit"]
        if verify_database_backup is not None:
            manifest["verify_database_backup"] = verify_database_backup
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

    def test_production_records_verified_backup_before_activating_release(self):
        self.config["environment"] = "prod"
        self.server = DeploymentServer(self.config, self.runtime)
        self.server.initialize()
        backup = "before-main-10-test.dump.cms"
        self.runtime.verify_database_backup = Mock(return_value=backup)
        activate = self.server.activate_release

        def activate_after_backup(previous, record, services):
            self.runtime.verify_database_backup.assert_called_once_with(10)
            return activate(previous, record, services)

        self.server.activate_release = activate_after_backup
        result = self.server.apply(
            self.release_archive(
                branch="main", environment="prod", verify_database_backup=True
            )
        )

        self.assertEqual(result["verified_database_backup"], backup)

    def test_failed_production_backup_verification_leaves_running_release_intact(self):
        self.config["environment"] = "prod"
        self.server = DeploymentServer(self.config, self.runtime)
        self.server.initialize()
        previous = self.server.state()
        self.runtime.verify_database_backup = Mock(
            side_effect=RuntimeError("Backup restoration failed.")
        )

        with self.assertRaisesRegex(RuntimeError, "Backup restoration failed"):
            self.server.apply(
                self.release_archive(
                    branch="main", environment="prod", verify_database_backup=True
                )
            )

        self.assertEqual(self.server.state(), previous)

    def test_development_release_cannot_request_production_backup(self):
        self.server.initialize()

        with self.assertRaisesRegex(ValueError, "production backup"):
            self.server.apply(self.release_archive(verify_database_backup=True))

    def test_production_receiver_accepts_main_and_rejects_development_releases(self):
        self.config["environment"] = "prod"
        self.server = DeploymentServer(self.config, self.runtime)
        self.server.initialize()
        with self.assertRaisesRegex(ValueError, "environment|main"):
            self.server.apply(self.release_archive())

        result = self.server.apply(
            self.release_archive(branch="main", environment="prod")
        )
        self.assertEqual(result["candidate_commit"], "c" * 40)
        self.assertEqual(result["environment"], "prod")
        self.assertTrue(
            (Path(result["compose_file"]).parent / "images.tar.gz").is_file()
        )

    def test_production_rollback_restores_previous_version_without_touching_database(
        self,
    ):
        self.config["environment"] = "prod"
        self.server = DeploymentServer(self.config, self.runtime)
        self.server.initialize()
        self.server.apply(self.release_archive(branch="main", environment="prod"))
        previous_images = self.runtime.images.copy()
        self.server.apply(
            self.release_archive(
                pipeline_id=11, candidate="d" * 40, branch="main", environment="prod"
            )
        )

        with self.assertRaisesRegex(ValueError, "database"):
            self.server.rollback("d" * 40)
        with self.assertRaisesRegex(ValueError, "changed"):
            self.server.rollback("e" * 40, database_compatible=True)
        result = self.server.rollback("d" * 40, database_compatible=True)

        self.assertEqual(result["result"], "rolled-back")
        self.assertEqual(self.runtime.images, previous_images)
        self.assertEqual(self.runtime.containers["database"], 1)
        self.assertEqual(self.server.state()["candidate_commit"], "c" * 40)
        self.assertEqual(self.server.state()["pipeline_id"], 11)
        self.assertIn("server-only-test-secret", self.environment.read_text())

    def test_first_production_release_can_restore_its_bootstrap_images(self):
        self.config["environment"] = "prod"
        self.server = DeploymentServer(self.config, self.runtime)
        self.server.initialize()
        initial_images = self.runtime.images.copy()
        self.server.apply(self.release_archive(branch="main", environment="prod"))

        result = self.server.rollback("c" * 40, database_compatible=True)

        self.assertEqual(result["result"], "rolled-back")
        self.assertEqual(self.runtime.images, initial_images)
        self.assertIsNone(self.server.state()["candidate_commit"])

    def test_initial_image_export_failure_can_be_retried_without_a_false_success(self):
        self.config["environment"] = "prod"
        self.server = DeploymentServer(self.config, self.runtime)
        with (
            patch.object(
                self.runtime,
                "retain_images",
                side_effect=RuntimeError("image export failed"),
            ),
            self.assertRaisesRegex(RuntimeError, "image export"),
        ):
            self.server.initialize()
        self.assertFalse((self.server.directory / "bootstrap.json").exists())

        self.server.initialize()
        self.assertTrue((self.server.directory / "bootstrap.json").is_file())
        self.assertFalse((self.server.directory / "last-success.json").exists())

    def test_failed_production_rollback_restores_current_release_and_keeps_success(
        self,
    ):
        self.config["environment"] = "prod"
        self.server = DeploymentServer(self.config, self.runtime)
        self.server.initialize()
        self.server.apply(self.release_archive(branch="main", environment="prod"))
        self.server.apply(
            self.release_archive(
                pipeline_id=11, candidate="d" * 40, branch="main", environment="prod"
            )
        )
        current_images = self.runtime.images.copy()
        current_success = (self.server.directory / "last-success.json").read_bytes()
        self.runtime.fail_next_check = True

        with self.assertRaisesRegex(RuntimeError, "previous release restored"):
            self.server.rollback("d" * 40, database_compatible=True)

        self.assertEqual(self.runtime.images, current_images)
        self.assertEqual(
            (self.server.directory / "last-success.json").read_bytes(), current_success
        )
        self.assertEqual(self.runtime.containers["database"], 1)

    def test_record_failure_keeps_previous_production_recovery_target(self):
        self.config["environment"] = "prod"
        self.server = DeploymentServer(self.config, self.runtime)
        self.server.initialize()
        self.server.apply(self.release_archive(branch="main", environment="prod"))
        previous_file = self.server.directory / "previous-success.json"
        previous_target = previous_file.read_bytes()
        write_json = deployment_server.write_json

        def fail_success_record(path, value):
            if path.name == "last-success.json":
                raise OSError("record storage unavailable")
            write_json(path, value)

        with (
            patch("deployment_server.write_json", side_effect=fail_success_record),
            self.assertRaisesRegex(RuntimeError, "previous release restored"),
        ):
            self.server.apply(
                self.release_archive(
                    pipeline_id=11,
                    candidate="d" * 40,
                    branch="main",
                    environment="prod",
                )
            )

        self.assertEqual(previous_file.read_bytes(), previous_target)
        self.assertEqual(self.server.state()["candidate_commit"], "c" * 40)

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

    def test_missing_deployment_branch_is_rejected(self):
        self.server.initialize()

        with self.assertRaisesRegex(ValueError, "Only dev"):
            self.server.apply(self.release_archive(branch=None))

        self.assertFalse((self.root / "deployments/last-success.json").exists())

    def test_development_receiver_rejects_retired_pilot_in_existing_config(self):
        retired_branch = "chore/retired-deploy-pilot"
        self.config["pilot_branch"] = retired_branch
        self.server.initialize()
        original_containers = self.runtime.containers.copy()

        with self.assertRaisesRegex(ValueError, "Only dev"):
            self.server.apply(self.release_archive(branch=retired_branch))

        self.assertEqual(self.runtime.containers, original_containers)
        self.assertFalse((self.root / "deployments/last-success.json").exists())

        result = self.server.apply(self.release_archive())
        self.assertEqual(result["candidate_commit"], "c" * 40)

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
            "GOOGLE_CLIENT_ID=dev-test.apps.googleusercontent.com\n",
            self.environment.read_text(),
        )
        self.assertIn(
            "GOOGLE_CLIENT_SECRET=server-only-google-test-secret\n",
            self.environment.read_text(),
        )
        self.assertNotIn("server-only-google-test-secret", json.dumps(result))
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

    def test_ready_development_release_uses_verified_base_instead_of_pipeline_age(self):
        self.server.initialize()
        self.server.apply(self.release_archive(pipeline_id=20, candidate="d" * 40))

        result = self.server.apply(
            self.release_archive(
                pipeline_id=19, candidate="e" * 40, ready_deployment=True
            )
        )

        self.assertEqual(result["candidate_commit"], "e" * 40)
        self.assertEqual(self.server.state()["pipeline_id"], 19)

    def test_ready_development_release_cannot_reuse_a_stale_state_token(self):
        self.server.initialize()
        older = self.release_archive(ready_deployment=True)
        self.server.apply(self.release_archive(pipeline_id=11, candidate="d" * 40))

        with self.assertRaisesRegex(ValueError, "state changed"):
            self.server.apply(older)

        self.assertEqual(self.server.state()["candidate_commit"], "d" * 40)

    def test_production_keeps_pipeline_order_check_with_a_verified_base_field(self):
        self.config["environment"] = "prod"
        self.server = DeploymentServer(self.config, self.runtime)
        self.server.initialize()
        self.server.apply(
            self.release_archive(pipeline_id=20, branch="main", environment="prod")
        )

        with self.assertRaisesRegex(ValueError, "outdated"):
            self.server.apply(
                self.release_archive(
                    pipeline_id=19,
                    candidate="d" * 40,
                    branch="main",
                    environment="prod",
                    ready_deployment=True,
                )
            )

        self.assertEqual(self.server.state()["pipeline_id"], 20)

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

    def test_environment_change_invalidates_an_already_planned_release(self):
        self.server.initialize()
        archive = self.release_archive()
        self.environment.write_text(
            self.environment.read_text() + "LLM_PROFILE=changed\n"
        )

        with self.assertRaisesRegex(ValueError, "state changed"):
            self.server.apply(archive)

        self.assertEqual(self.runtime.containers["backend"], 1)

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


class ComposeConfigurationTests(unittest.TestCase):
    def setUp(self):
        self.configuration = {
            "services": {
                "database": {},
                "ai-worker": {},
                "backend": {
                    "environment": {
                        "MANAGEMENT_ADDRESS": "0.0.0.0",
                        "GOOGLE_CLIENT_ID": "dev-test.apps.googleusercontent.com",
                        "GOOGLE_CLIENT_SECRET": "server-only-google-test-secret",
                        "NPC_AUDIO_DIR": "/app/data/npc-audio",
                    },
                    "volumes": [
                        {
                            "type": "volume",
                            "source": "npc-audio-data",
                            "target": "/app/data/npc-audio",
                        }
                    ],
                },
                "web": {
                    "ports": [
                        {"host_ip": "127.0.0.1", "published": "8081", "target": 80}
                    ],
                    "volumes": [
                        {
                            "type": "bind",
                            "source": "/srv/observability-web.conf",
                            "target": "/etc/nginx/conf.d/00-observability.conf",
                            "read_only": True,
                        }
                    ],
                },
            },
            "volumes": {"npc-audio-data": {"name": "a305-dev_npc-audio-data"}},
        }
        self.runtime = ComposeRuntime({})
        self.backend_mounts = [
            {
                "Type": "volume",
                "Name": "a305-dev_npc-audio-data",
                "Destination": "/app/data/npc-audio",
                "RW": True,
            }
        ]

    def validate_candidate(self, candidate):
        def inspect_service(service):
            if service == "backend":
                return {"mounts": self.backend_mounts}
            return {
                "mounts": [
                    {
                        "Type": "bind",
                        "Source": "/srv/observability-web.conf",
                        "Destination": "/etc/nginx/conf.d/00-observability.conf",
                    }
                ]
            }

        with (
            patch.object(
                self.runtime,
                "compose",
                side_effect=[json.dumps(candidate), json.dumps(self.configuration)],
            ),
            patch.object(
                self.runtime,
                "inspect",
                side_effect=inspect_service,
            ),
        ):
            self.runtime.validate("candidate.yaml", "images.env", "previous.yaml")

    def test_fixed_worker_policy_rejects_dynamic_worker_before_replacement(self):
        self.runtime.config["fixed_worker_network"] = True
        with self.assertRaisesRegex(ValueError, "external"):
            self.validate_candidate(deepcopy(self.configuration))

    def test_development_worker_policy_cannot_be_enabled_for_production(self):
        self.runtime.config["fixed_worker_network"] = True
        self.runtime.environment = "prod"
        with self.assertRaisesRegex(ValueError, "prod"):
            self.validate_candidate(deepcopy(self.configuration))

    def test_missing_google_credentials_rejects_deployment_configuration(self):
        for key in ("GOOGLE_CLIENT_ID", "GOOGLE_CLIENT_SECRET"):
            with self.subTest(key=key):
                candidate = deepcopy(self.configuration)
                del candidate["services"]["backend"]["environment"][key]

                with self.assertRaisesRegex(ValueError, "Google|GOOGLE"):
                    self.validate_candidate(candidate)

    def test_changed_google_credentials_cannot_replace_server_settings(self):
        for key in ("GOOGLE_CLIENT_ID", "GOOGLE_CLIENT_SECRET"):
            with self.subTest(key=key):
                candidate = deepcopy(self.configuration)
                candidate["services"]["backend"]["environment"][key] = "different-value"

                with self.assertRaisesRegex(ValueError, "Google|GOOGLE") as raised:
                    self.validate_candidate(candidate)

                self.assertNotIn(
                    "server-only-google-test-secret", str(raised.exception)
                )
                self.assertNotIn("different-value", str(raised.exception))

    def test_empty_and_placeholder_google_credentials_are_rejected(self):
        for key in ("GOOGLE_CLIENT_ID", "GOOGLE_CLIENT_SECRET"):
            for value in ("", "   ", "not-configured"):
                with self.subTest(key=key, value=value):
                    candidate = deepcopy(self.configuration)
                    candidate["services"]["backend"]["environment"][key] = value

                    with self.assertRaisesRegex(ValueError, "Google|GOOGLE"):
                        self.validate_candidate(candidate)

    def test_server_google_credentials_allow_deployment_validation(self):
        self.validate_candidate(deepcopy(self.configuration))

    def test_production_configuration_uses_its_own_port_and_persistent_volume(self):
        self.runtime = ComposeRuntime({"environment": "prod"})
        self.configuration["services"]["web"]["ports"][0]["published"] = "8080"
        self.configuration["services"]["backend"]["environment"]["JPA_DDL_AUTO"] = (
            "validate"
        )
        self.configuration["volumes"]["database-data"] = {
            "name": "a305-prod_database-data"
        }
        self.configuration["volumes"]["npc-audio-data"]["name"] = (
            "a305-prod_npc-audio-data"
        )
        self.backend_mounts[0]["Name"] = "a305-prod_npc-audio-data"

        self.validate_candidate(deepcopy(self.configuration))

        for ddl_mode in ("update", "create", "none", None):
            with self.subTest(ddl_mode=ddl_mode):
                candidate = deepcopy(self.configuration)
                candidate["services"]["backend"]["environment"]["JPA_DDL_AUTO"] = (
                    ddl_mode
                )
                with self.assertRaisesRegex(ValueError, "validate"):
                    self.validate_candidate(candidate)

        candidate = deepcopy(self.configuration)
        candidate["volumes"]["database-data"]["name"] = "a305-dev_database-data"
        with self.assertRaisesRegex(ValueError, "database volume"):
            self.validate_candidate(candidate)

    def test_missing_audio_mount_rejects_deployment(self):
        candidate = deepcopy(self.configuration)
        candidate["services"]["backend"]["volumes"] = []

        with self.assertRaisesRegex(ValueError, "NPC audio"):
            self.validate_candidate(candidate)

    def test_replaced_or_readonly_audio_mount_rejects_deployment(self):
        for override in (
            {"type": "tmpfs"},
            {"source": "replacement-audio-data"},
            {"read_only": True},
        ):
            with self.subTest(override=override):
                candidate = deepcopy(self.configuration)
                candidate["services"]["backend"]["volumes"][0].update(override)

                with self.assertRaisesRegex(ValueError, "NPC audio"):
                    self.validate_candidate(candidate)

    def test_changed_resolved_audio_volume_rejects_deployment(self):
        candidate = deepcopy(self.configuration)
        candidate["volumes"]["npc-audio-data"]["name"] = "a305-prod_npc-audio-data"

        with self.assertRaisesRegex(ValueError, "NPC audio"):
            self.validate_candidate(candidate)

    def test_changed_audio_directory_rejects_deployment(self):
        for directory in (None, "/tmp/npc-audio"):
            with self.subTest(directory=directory):
                candidate = deepcopy(self.configuration)
                candidate["services"]["backend"]["environment"]["NPC_AUDIO_DIR"] = (
                    directory
                )

                with self.assertRaisesRegex(ValueError, "NPC audio"):
                    self.validate_candidate(candidate)

    def test_missing_installed_audio_mount_rejects_deployment(self):
        self.backend_mounts = []

        with self.assertRaisesRegex(ValueError, "NPC audio"):
            self.validate_candidate(deepcopy(self.configuration))

    def test_installed_audio_volume_must_match_the_candidate(self):
        original_mount = deepcopy(self.backend_mounts[0])
        for override in (
            {"Type": "tmpfs"},
            {"Name": "different-existing-audio"},
            {"RW": False},
        ):
            with self.subTest(override=override):
                self.backend_mounts = [{**original_mount, **override}]

                with self.assertRaisesRegex(ValueError, "NPC audio"):
                    self.validate_candidate(deepcopy(self.configuration))


class ProductionRuntimeTests(unittest.TestCase):
    def test_image_archive_is_complete_and_failed_export_preserves_the_existing_file(
        self,
    ):
        with tempfile.TemporaryDirectory() as directory:
            archive = Path(directory) / "images.tar.gz"
            runtime = ComposeRuntime(
                {
                    "environment": "prod",
                    "docker_command": [
                        sys.executable,
                        "-c",
                        "import sys; sys.stdout.buffer.write(b'image-archive')",
                    ],
                }
            )
            images = {"backend": {"reference": "a305/backend:fixture"}}
            runtime.retain_images(archive, images)
            self.assertEqual(gzip.decompress(archive.read_bytes()), b"image-archive")
            retained = archive.read_bytes()
            runtime.config["docker_command"] = [
                sys.executable,
                "-c",
                "import sys; sys.exit(1)",
            ]
            with self.assertRaisesRegex(RuntimeError, "retain production images"):
                runtime.retain_images(archive, images)
            self.assertEqual(archive.read_bytes(), retained)

    def test_restricted_ssh_cannot_initialize_or_start_operator_rollback(self):
        with tempfile.TemporaryDirectory() as directory:
            config = Path(directory) / "config.json"
            config.write_text(
                json.dumps({"environment": "prod", "directory": directory})
            )
            for command in ("initialize", "rollback", "sh", "apply extra"):
                with (
                    self.subTest(command=command),
                    patch(
                        "sys.argv", ["deployment_server.py", "--config", str(config)]
                    ),
                    patch.dict("os.environ", {"SSH_ORIGINAL_COMMAND": command}),
                    self.assertRaisesRegex(ValueError, "Only state and apply"),
                ):
                    deployment_server.main()

    def test_production_commands_use_only_the_production_project_and_containers(self):
        runtime = ComposeRuntime(
            {
                "environment": "prod",
                "environment_file": "/prod/deploy.env",
                "overlays": [],
            }
        )
        with patch.object(runtime, "docker", return_value="{}") as docker:
            runtime.compose("app.yaml", "images.env", "config")
            self.assertEqual(docker.call_args.args[2], "a305-prod")
            runtime.inspect("backend")
            self.assertEqual(docker.call_args.args[-1], "a305-prod-backend-1")

    def test_production_readiness_rejects_another_webgl_version_at_the_public_route(
        self,
    ):
        runtime = ComposeRuntime(
            {
                "environment": "prod",
                "public_health_url": "https://example.test/api/health",
                "public_version_url": "https://example.test/version.json",
            }
        )
        runtime.health_after = 100
        images = {
            name: {"reference": "test", "id": "test", "source_commit": "a" * 40}
            for name in ["backend", "ai", "web"]
        }
        visited = []

        def response(url, timeout):
            visited.append(url)
            if "/api/v1/query?" in url:
                self.assertIn(
                    'job="elsehere-api-prod"', parse_qs(urlparse(url).query)["query"][0]
                )
                payload = {"data": {"result": [{"value": [200, "110"]}]}}
            elif url.endswith("version.json"):
                payload = {
                    "commit": "b" * 40 if url.startswith("https:") else "a" * 40,
                    "build_type": "unity-webgl",
                }
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
            self.assertRaisesRegex(RuntimeError, "WebGL"),
        ):
            runtime.check_once(images)
        self.assertIn("http://127.0.0.1:8080/api/health", visited)


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
