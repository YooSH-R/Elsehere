import hashlib
import io
import json
import os
import subprocess
import sys
import tarfile
import tempfile
import unittest
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from threading import Thread
from unittest.mock import patch
from urllib.error import HTTPError

import webgl_package


class WebglPackageTests(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.repository = Path(directory.name)
        self.candidate = "a" * 40
        self.files = {
            "index.html": b"<html>verified build</html>",
            "Build/game.data.gz": b"\x00\xffcompressed-game-data",
            "version.json": json.dumps(
                {"commit": self.candidate, "build_type": "unity-webgl"}
            ).encode(),
        }
        archive = io.BytesIO()
        with tarfile.open(fileobj=archive, mode="w") as bundle:
            for name, content in self.files.items():
                member = tarfile.TarInfo(name)
                member.size = len(content)
                bundle.addfile(member, io.BytesIO(content))
        self.archive = archive.getvalue()
        self.metadata = {
            "schema_version": 1,
            "commit": self.candidate,
            "pipeline_id": "123",
            "job_id": "456",
            "sha256": hashlib.sha256(self.archive).hexdigest(),
            "size": len(self.archive),
        }
        (self.repository / "dist").mkdir()
        self.write_metadata()
        environment = patch.dict(
            os.environ,
            {
                "CI_API_V4_URL": "https://gitlab.example.invalid/api/v4",
                "CI_PROJECT_ID": "42",
                "CI_PIPELINE_ID": "123",
                "CI_JOB_TOKEN": "fixture-secret-token",
            },
        )
        environment.start()
        self.addCleanup(environment.stop)

    def write_metadata(self):
        (self.repository / "dist/webgl-package.json").write_text(
            json.dumps(self.metadata), encoding="utf-8"
        )

    def test_restores_all_build_files_from_the_exact_pipeline_package(self):
        with patch(
            "webgl_package.urlopen", return_value=io.BytesIO(self.archive)
        ) as fetch:
            webgl_package.restore_webgl(self.repository, self.candidate)

        for name, content in self.files.items():
            self.assertEqual(
                (self.repository / "dist/webgl" / name).read_bytes(), content
            )
        request = fetch.call_args.args[0]
        self.assertEqual(
            request.full_url,
            "https://gitlab.example.invalid/api/v4/projects/42/packages/generic/"
            "a305-webgl/123-456/webgl.tar",
        )
        self.assertEqual(request.get_header("Job-token"), "fixture-secret-token")

    def test_corrupt_download_is_rejected_before_extracting_any_file(self):
        with (
            patch(
                "webgl_package.urlopen", return_value=io.BytesIO(self.archive + b"x")
            ),
            self.assertRaisesRegex(ValueError, "checksum|size"),
        ):
            webgl_package.restore_webgl(self.repository, self.candidate)
        self.assertFalse((self.repository / "dist/webgl").exists())

    def test_another_candidate_or_pipeline_is_rejected_before_network_access(self):
        for field, value in (
            ("commit", "b" * 40),
            ("pipeline_id", "999"),
            ("job_id", "../other"),
        ):
            with self.subTest(field=field):
                original = self.metadata[field]
                self.metadata[field] = value
                self.write_metadata()
                with (
                    patch(
                        "webgl_package.urlopen", return_value=io.BytesIO(self.archive)
                    ) as fetch,
                    self.assertRaises(ValueError),
                ):
                    webgl_package.restore_webgl(self.repository, self.candidate)
                fetch.assert_not_called()
                self.metadata[field] = original

    def test_cleanup_deletes_only_this_jobs_temporary_package(self):
        packages = [
            {
                "id": 11,
                "name": "a305-webgl",
                "version": "123-456",
                "package_type": "generic",
            },
            {
                "id": 12,
                "name": "a305-webgl",
                "version": "123-999",
                "package_type": "generic",
            },
            {
                "id": 13,
                "name": "release",
                "version": "123-456",
                "package_type": "generic",
            },
        ]
        with patch(
            "webgl_package.urlopen",
            side_effect=[io.BytesIO(json.dumps(packages).encode()), io.BytesIO()],
        ) as fetch:
            webgl_package.cleanup_webgl(self.repository, self.candidate)
        requests = [call.args[0] for call in fetch.call_args_list]
        self.assertEqual(
            [request.get_method() for request in requests], ["GET", "DELETE"]
        )
        self.assertEqual(
            requests[1].full_url,
            "https://gitlab.example.invalid/api/v4/projects/42/packages/11",
        )

    def test_cleanup_without_a_web_build_does_not_contact_gitlab(self):
        (self.repository / "dist/webgl-package.json").unlink()
        with patch("webgl_package.urlopen") as fetch:
            webgl_package.cleanup_webgl(self.repository, self.candidate)
        fetch.assert_not_called()

    def test_package_download_failure_never_creates_build_files(self):
        with (
            patch(
                "webgl_package.urlopen",
                side_effect=HTTPError("fixture", 404, "missing", {}, None),
            ),
            self.assertRaises(HTTPError),
        ):
            webgl_package.restore_webgl(self.repository, self.candidate)
        self.assertFalse((self.repository / "dist/webgl").exists())

    def test_cleanup_permission_failure_has_a_distinct_maintainer_retry_result(self):
        with (
            patch("sys.argv", ["webgl_package.py", "cleanup"]),
            patch.dict(os.environ, {"CI_COMMIT_SHA": self.candidate}),
            patch(
                "webgl_package.cleanup_webgl",
                side_effect=HTTPError("fixture", 403, "forbidden", {}, None),
            ),
        ):
            self.assertEqual(webgl_package.main(), 77)


@unittest.skipUnless(sys.platform == "win32", "Requires the Windows Unity runner")
class WebglPublisherTests(unittest.TestCase):
    def test_windows_publisher_uploads_the_build_and_keeps_only_small_metadata(self):
        with tempfile.TemporaryDirectory() as directory:
            repository = Path(directory)
            output = repository / "dist/webgl"
            (output / "Build").mkdir(parents=True)
            (output / "index.html").write_bytes(b"verified-index")
            (output / "Build/game.data.gz").write_bytes(b"\x00\xffcompressed-data")
            candidate = "a" * 40
            (output / "version.json").write_text(
                json.dumps({"commit": candidate, "build_type": "unity-webgl"})
            )
            uploads = []

            class Handler(BaseHTTPRequestHandler):
                def do_PUT(self):
                    uploads.append(
                        (
                            self.path,
                            self.headers.get("JOB-TOKEN"),
                            self.rfile.read(int(self.headers["Content-Length"])),
                        )
                    )
                    self.send_response(201)
                    self.end_headers()

                def log_message(self, *arguments):
                    pass

            server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
            thread = Thread(target=server.serve_forever, daemon=True)
            thread.start()
            try:
                environment = dict(
                    os.environ,
                    CI_API_V4_URL=f"http://127.0.0.1:{server.server_port}/api/v4",
                    CI_PROJECT_ID="42",
                    CI_PIPELINE_ID="123",
                    CI_JOB_ID="456",
                    CI_COMMIT_SHA=candidate,
                    CI_JOB_TOKEN="fixture-secret-token",
                )
                script = (
                    Path(__file__).resolve().parents[1] / "publish-webgl-package.ps1"
                )
                result = subprocess.run(
                    [
                        "powershell.exe",
                        "-NoProfile",
                        "-NonInteractive",
                        "-File",
                        str(script),
                        "-RepositoryPath",
                        str(repository),
                    ],
                    check=False,
                    env=environment,
                    capture_output=True,
                    text=True,
                    timeout=30,
                )
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            finally:
                server.shutdown()
                server.server_close()
                thread.join()

            self.assertEqual(len(uploads), 1)
            url, token, archive = uploads[0]
            self.assertEqual(
                url, "/api/v4/projects/42/packages/generic/a305-webgl/123-456/webgl.tar"
            )
            self.assertEqual(token, "fixture-secret-token")
            self.assertNotIn(token, result.stdout + result.stderr)
            with tarfile.open(fileobj=io.BytesIO(archive)) as bundle:
                self.assertEqual(
                    bundle.extractfile("./Build/game.data.gz").read(),
                    b"\x00\xffcompressed-data",
                )
            metadata_path = repository / "dist/webgl-package.json"
            metadata = json.loads(metadata_path.read_text(encoding="utf-8-sig"))
            self.assertEqual(metadata["sha256"], hashlib.sha256(archive).hexdigest())
            self.assertEqual(metadata["size"], len(archive))
            self.assertEqual(metadata["commit"], candidate)
            self.assertLess(metadata_path.stat().st_size, 1024)


if __name__ == "__main__":
    unittest.main()
