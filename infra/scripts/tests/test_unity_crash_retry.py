import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


@unittest.skipUnless(sys.platform == "win32", "Requires the Windows Unity runner")
class UnityCrashRetryTests(unittest.TestCase):
    def run_editor(self, exit_code, result=None, action="test"):
        with tempfile.TemporaryDirectory() as directory:
            repository = Path(directory)
            scripts = repository / "infra/scripts"
            scripts.mkdir(parents=True)
            source = Path(__file__).resolve().parents[1] / "run-unity-ci.ps1"
            shutil.copyfile(source, scripts / source.name)
            settings = repository / "frontend/UnityClient/ProjectSettings"
            settings.mkdir(parents=True)
            (settings / "ProjectVersion.txt").write_text("m_EditorVersion: fixture\n")
            (scripts / "prepare-unity-test-inputs.ps1").write_text(
                "param($ProjectPath, $OutputPath)\n"
                "New-Item -ItemType Directory -Path $OutputPath | Out-Null\n"
                "Set-Content (Join-Path $OutputPath 'fixture.txt') 'test input'\n"
                "[pscustomobject]@{ Blender=''; Model=''; Reports=''; MaterialModel='' }\n"
            )
            result_file = repository / "result.xml"
            if result is not None:
                result_file.write_text(result)
            editor = repository / "editor.cmd"
            editor.write_text(
                "@echo off\n"
                ":arguments\n"
                'if "%~1"=="" goto finish\n'
                'if "%~1"=="-logFile" echo Unity fixture log>"%~2"\n'
                'if "%~1"=="-testResults" if exist "%A305_TEST_RESULT%" '
                'copy /y "%A305_TEST_RESULT%" "%~2" >nul\n'
                "shift\n"
                "goto arguments\n"
                ":finish\n"
                f"exit /b {exit_code}\n"
            )
            completed = subprocess.run(
                [
                    "powershell.exe",
                    "-NoProfile",
                    "-NonInteractive",
                    "-File",
                    str(scripts / source.name),
                    "-Action",
                    action,
                    "-UnityEditorPath",
                    str(editor),
                ],
                check=False,
                env=dict(os.environ, A305_TEST_RESULT=str(result_file)),
                capture_output=True,
                timeout=20,
            )
            artifacts = {
                path.relative_to(repository / "dist/unity-ci").as_posix(): (
                    path.read_text()
                )
                for path in (repository / "dist/unity-ci").rglob("*")
                if path.is_file()
            }
            return completed.returncode, artifacts, completed.stdout + completed.stderr

    def test_mono_crash_requests_retry_and_keeps_diagnostic_artifacts(self):
        exit_code, artifacts, output = self.run_editor(-536870911)
        self.assertEqual(exit_code, 75, output)
        self.assertIn("Unity fixture log", artifacts["EditMode.log"])
        self.assertIn("test input", artifacts["Tripo/fixture.txt"])

    def test_native_windows_crashes_request_retry(self):
        for code in (0xC0000005, 0xC000001D, 0xC00000FD, 0xC0000374, 0xC0000409):
            with self.subTest(code=hex(code)):
                exit_code, _, output = self.run_editor(code - 2**32)
                self.assertEqual(exit_code, 75, output)

    def test_failed_tests_are_not_retried_even_if_editor_also_crashes(self):
        result = '<test-run result="Failed" passed="1" failed="1" skipped="0" />'
        for code in (2, -536870911):
            with self.subTest(code=code):
                exit_code, artifacts, output = self.run_editor(code, result)
                self.assertEqual(exit_code, 1, output)
                self.assertEqual(artifacts["EditMode.xml"], result)

    def test_crash_after_passing_tests_still_requests_retry(self):
        result = '<test-run result="Passed" passed="1" failed="0" skipped="0" />'
        exit_code, artifacts, output = self.run_editor(-536870911, result)
        self.assertEqual(exit_code, 75, output)
        self.assertEqual(artifacts["EditMode.xml"], result)

    def test_general_failures_and_cancellation_do_not_request_retry(self):
        for code in (1, 3, 75, -1, -1073741510):
            with self.subTest(code=code):
                exit_code, _, output = self.run_editor(code)
                self.assertEqual(exit_code, 1, output)

    def test_missing_invalid_or_incomplete_results_do_not_request_retry(self):
        for result in (
            None,
            "invalid XML",
            '<test-run result="Passed" passed="0" failed="0" skipped="0" />',
            '<test-run result="Passed" passed="1" failed="0" skipped="1" />',
        ):
            with self.subTest(result=result):
                exit_code, _, output = self.run_editor(0, result)
                self.assertEqual(exit_code, 1, output)

    def test_passing_tests_do_not_request_retry(self):
        result = '<test-run result="Passed" passed="1" failed="0" skipped="0" />'
        exit_code, artifacts, output = self.run_editor(0, result)
        self.assertEqual(exit_code, 0, output)
        self.assertEqual(artifacts["EditMode.xml"], result)
        self.assertEqual(artifacts["PlayMode.xml"], result)

    def test_build_crash_uses_the_same_retry_signal(self):
        exit_code, artifacts, output = self.run_editor(-536870911, action="build")
        self.assertEqual(exit_code, 75, output)
        self.assertIn("Unity fixture log", artifacts["WebGL.log"])


if __name__ == "__main__":
    unittest.main()
