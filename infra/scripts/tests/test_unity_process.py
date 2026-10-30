import os
import shutil
import subprocess
import sys
import tempfile
import time
import unittest
from pathlib import Path


@unittest.skipUnless(sys.platform == "win32", "Requires the Windows Unity runner")
class UnityProcessTests(unittest.TestCase):
    def test_editor_failure_does_not_wait_for_surviving_child_and_keeps_log(self):
        with tempfile.TemporaryDirectory() as directory:
            repository = Path(directory)
            scripts = repository / "infra/scripts"
            scripts.mkdir(parents=True)
            source = Path(__file__).resolve().parents[1] / "run-unity-ci.ps1"
            shutil.copyfile(source, scripts / source.name)
            settings = repository / "frontend/UnityClient/ProjectSettings"
            settings.mkdir(parents=True)
            (settings / "ProjectVersion.txt").write_text("m_EditorVersion: fixture\n")

            release = repository / "release-child"
            child_done = repository / "child-done"
            child = repository / "child.ps1"
            child.write_text(
                "$deadline = [DateTime]::UtcNow.AddSeconds(20)\n"
                "while (-not (Test-Path $env:A305_TEST_RELEASE_CHILD) -and "
                "[DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 100 }\n"
                "Set-Content $env:A305_TEST_CHILD_DONE 'done'\n"
            )
            editor = repository / "editor.cmd"
            editor.write_text(
                "@echo off\n"
                ":arguments\n"
                'if "%~1"=="" goto launch\n'
                'if "%~1"=="-logFile" echo Editor crashed>"%~2"\n'
                "shift\n"
                "goto arguments\n"
                ":launch\n"
                'start "" /b powershell.exe -NoProfile -NonInteractive '
                f'-File "{child}" >nul 2>&1\n'
                "exit /b 23\n"
            )
            environment = dict(
                os.environ,
                A305_TEST_RELEASE_CHILD=str(release),
                A305_TEST_CHILD_DONE=str(child_done),
            )
            with (repository / "runner-output.log").open("w") as output:
                process = subprocess.Popen(
                    [
                        "powershell.exe",
                        "-NoProfile",
                        "-NonInteractive",
                        "-File",
                        str(scripts / source.name),
                        "-Action",
                        "build",
                        "-UnityEditorPath",
                        str(editor),
                    ],
                    env=environment,
                    stdout=output,
                    stderr=subprocess.STDOUT,
                )
                try:
                    try:
                        process.wait(timeout=6)
                    except subprocess.TimeoutExpired:
                        self.fail("Editor failure waited for its surviving child")
                    self.assertNotEqual(process.returncode, 0)
                    log = repository / "dist/unity-ci/WebGL.log"
                    self.assertIn("Editor crashed", log.read_text())
                    runner_output = (repository / "runner-output.log").read_text()
                    self.assertIn("exit code 23", runner_output)
                finally:
                    release.touch()
                    process.wait(timeout=25)
                    deadline = time.monotonic() + 6
                    while not child_done.exists() and time.monotonic() < deadline:
                        time.sleep(0.05)
                    self.assertTrue(child_done.exists(), "Fixture child did not finish")


if __name__ == "__main__":
    unittest.main()
