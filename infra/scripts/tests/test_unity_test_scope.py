import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "run-unity-ci.ps1"
TEMPLATE = "frontend/UnityClient/Assets/WebGLTemplates/A305Responsive/index.html"
PLAYER_SCRIPT = "frontend/UnityClient/Assets/Scripts/Player/PlayerMover.cs"


@unittest.skipUnless(sys.platform == "win32", "Requires the Windows Unity runner")
class UnityTestScopeTests(unittest.TestCase):
    def setUp(self):
        self.create_repository()

    def create_repository(self):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.repository = Path(directory.name)
        scripts = self.repository / "infra/scripts"
        scripts.mkdir(parents=True)
        shutil.copyfile(SCRIPT, scripts / SCRIPT.name)
        # The real preparation downloads Blender; the stub records that it ran.
        (scripts / "prepare-unity-test-inputs.ps1").write_text(
            "param($ProjectPath, $OutputPath)\n"
            "New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null\n"
            "Set-Content (Join-Path $OutputPath 'prepared.txt') 'prepared'\n"
            "[pscustomobject]@{ Blender = 'b'; Model = 'm'; Reports = 'r'; "
            "MaterialModel = 'f' }\n"
        )
        (self.repository / "editor.cmd").write_text(
            "@echo off\n"
            'powershell.exe -NoProfile -NonInteractive -File "%~dp0editor.ps1" %*\n'
            "exit /b %ERRORLEVEL%\n"
        )
        (self.repository / "editor.ps1").write_text(
            "Add-Content -LiteralPath $env:A305_TEST_EDITOR_CALLS -Value ($args -join ' ')\n"
            "$resultIndex = [array]::IndexOf($args, '-testResults')\n"
            "Set-Content -LiteralPath $args[$resultIndex + 1] -Value "
            '\'<test-run result="Passed" passed="1" failed="0" skipped="0" />\'\n'
        )
        settings = self.repository / "frontend/UnityClient/ProjectSettings"
        settings.mkdir(parents=True)
        (settings / "ProjectVersion.txt").write_text("m_EditorVersion: fixture\n")
        self.git("init", "--quiet")
        self.base = self.commit({PLAYER_SCRIPT: "base\n", TEMPLATE: "base\n"})

    def git(self, *arguments):
        environment = dict(
            os.environ,
            GIT_AUTHOR_NAME="Fixture",
            GIT_AUTHOR_EMAIL="fixture@example.com",
            GIT_COMMITTER_NAME="Fixture",
            GIT_COMMITTER_EMAIL="fixture@example.com",
        )
        return subprocess.run(
            ["git", "-C", str(self.repository), *arguments],
            env=environment,
            check=True,
            capture_output=True,
            text=True,
        ).stdout.strip()

    def commit(self, files):
        for path, content in files.items():
            file = self.repository / path
            if content is None:
                file.unlink()
            else:
                file.parent.mkdir(parents=True, exist_ok=True)
                file.write_text(content, encoding="utf-8")
        self.git("add", "--all")
        self.git("commit", "--quiet", "-m", "fixture")
        return self.git("rev-parse", "HEAD")

    def run_tests(self, *arguments):
        calls = self.repository / "editor-calls.log"
        result = subprocess.run(
            [
                "powershell.exe",
                "-NoProfile",
                "-NonInteractive",
                "-File",
                str(self.repository / "infra/scripts" / SCRIPT.name),
                "-Action",
                "test",
                "-UnityEditorPath",
                str(self.repository / "editor.cmd"),
                *arguments,
            ],
            env=dict(os.environ, A305_TEST_EDITOR_CALLS=str(calls)),
            check=False,
            capture_output=True,
            text=True,
            timeout=120,
        )
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        runs = calls.read_text().splitlines()
        prepared = self.repository / "dist/unity-ci/Tripo/prepared.txt"
        return runs, prepared.exists()

    def assert_full_run(self, runs, prepared):
        self.assertEqual(len(runs), 2, runs)
        self.assertIn("-testPlatform EditMode", runs[0])
        self.assertIn("-testPlatform PlayMode", runs[1])
        self.assertTrue(all("-assemblyNames" not in run for run in runs), runs)
        self.assertTrue(prepared)

    def test_template_design_and_document_changes_run_only_deployment_tests(self):
        self.commit(
            {
                TEMPLATE: "changed\n",
                "frontend/design/로고.png": "image\n",
                "docs/unity/setup.md": "changed\n",
            }
        )

        runs, prepared = self.run_tests("-ChangedSince", self.base)

        self.assertEqual(len(runs), 1, runs)
        self.assertIn("-testPlatform EditMode", runs[0])
        self.assertIn("-assemblyNames A305.Deployment.Tests", runs[0])
        self.assertFalse(prepared)

    def test_any_play_affecting_change_runs_every_test(self):
        for path in (
            PLAYER_SCRIPT,
            "docs/api/mvp-api.md",
            "frontend/UnityClient/Assets/Notes.md",
            "frontend/ArtSources/Characters/Face.json",
        ):
            with self.subTest(path=path):
                self.create_repository()
                self.commit({TEMPLATE: "changed\n", path: "changed\n"})

                self.assert_full_run(*self.run_tests("-ChangedSince", self.base))

    def test_moving_a_script_into_the_template_folder_runs_every_test(self):
        moved = "frontend/UnityClient/Assets/WebGLTemplates/A305Responsive/Mover.cs"
        self.commit({PLAYER_SCRIPT: None, moved: "base\n"})

        self.assert_full_run(*self.run_tests("-ChangedSince", self.base))

    def test_unavailable_merge_base_runs_every_test(self):
        self.commit({TEMPLATE: "changed\n"})

        self.assert_full_run(*self.run_tests("-ChangedSince", "0" * 40))

    def test_runs_without_a_merge_base_check_everything(self):
        self.commit({TEMPLATE: "changed\n"})

        self.assert_full_run(*self.run_tests())


if __name__ == "__main__":
    unittest.main()
