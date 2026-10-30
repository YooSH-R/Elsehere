import json
import subprocess
import tempfile
import unittest
from pathlib import Path

from docker_build import build_image


class DockerBuildCacheTests(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.repository = Path(directory.name)
        self.cache_directory = self.repository / "runner-cache"
        self.commands = []

    def exporting_docker(self, *arguments):
        self.commands.append(arguments)
        if arguments[:2] == ("buildx", "build"):
            export = arguments[arguments.index("--cache-to") + 1]
            options = dict(part.split("=", 1) for part in export.split(","))
            destination = Path(options["dest"])
            destination.mkdir(parents=True)
            (destination / "index.json").write_text(json.dumps({"cache": "new"}))

    def build_ai(self, docker=None):
        build_image(
            docker or self.exporting_docker,
            self.repository,
            "ai",
            "a305/ai:verified-commit",
            "infra/docker/ai-worker.Dockerfile",
            cache_directory=self.cache_directory,
        )

    def test_first_build_exports_reusable_cache_without_an_existing_cache(self):
        self.build_ai()

        saved = json.loads((self.cache_directory / "ai/index.json").read_text())
        self.assertEqual(saved, {"cache": "new"})
        self.assertNotIn("--cache-from", self.commands[0])
        self.assertIn("--load", self.commands[0])
        self.assertIn("a305/ai:verified-commit", self.commands[0])

    def test_next_build_imports_the_previous_cache_and_replaces_it(self):
        saved_cache = self.cache_directory / "ai"
        saved_cache.mkdir(parents=True)
        (saved_cache / "index.json").write_text(json.dumps({"cache": "previous"}))
        (saved_cache / "old-blob").write_text("obsolete layer")

        self.build_ai()

        command = self.commands[0]
        imported_cache = command[command.index("--cache-from") + 1]
        self.assertEqual(imported_cache, f"type=local,src={saved_cache}")
        self.assertEqual(
            json.loads((saved_cache / "index.json").read_text()), {"cache": "new"}
        )
        self.assertFalse((saved_cache / "old-blob").exists())

    def test_manual_build_without_cache_preserves_webgl_input_and_revision_label(self):
        build_image(
            self.exporting_docker,
            self.repository,
            "web",
            "a305/web:verified-commit",
            "infra/docker/web.Dockerfile",
            build_arguments={"WEBGL_DIRECTORY": "dist/runtime-webgl"},
            labels={"org.opencontainers.image.revision": "verified-commit"},
        )

        command = self.commands[0]
        self.assertEqual(command[0], "build")
        self.assertNotIn("--cache-to", command)
        self.assertIn("WEBGL_DIRECTORY=dist/runtime-webgl", command)
        self.assertIn("org.opencontainers.image.revision=verified-commit", command)
        self.assertEqual(command[-1], self.repository)

    def test_failed_rebuild_keeps_previous_cache_and_cleans_partial_export(self):
        saved_cache = self.cache_directory / "ai"
        saved_cache.mkdir(parents=True)
        (saved_cache / "index.json").write_text(json.dumps({"cache": "previous"}))

        def failing_docker(*arguments):
            self.exporting_docker(*arguments)
            raise subprocess.CalledProcessError(1, ["docker", "buildx", "build"])

        with self.assertRaises(subprocess.CalledProcessError):
            self.build_ai(failing_docker)

        self.assertEqual(
            json.loads((saved_cache / "index.json").read_text()), {"cache": "previous"}
        )
        self.assertEqual([path.name for path in self.cache_directory.iterdir()], ["ai"])

    def test_service_cache_does_not_replace_another_services_cache(self):
        self.build_ai()

        build_image(
            self.exporting_docker,
            self.repository,
            "backend",
            "a305/backend:verified-commit",
            "infra/docker/backend.Dockerfile",
            cache_directory=self.cache_directory,
        )

        self.assertTrue((self.cache_directory / "ai/index.json").is_file())
        self.assertTrue((self.cache_directory / "backend/index.json").is_file())
        self.assertNotIn("--cache-from", self.commands[1])
