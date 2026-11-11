import json
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
        scope_script = SCRIPT.with_name("unity-test-scope.ps1")
        shutil.copyfile(scope_script, scripts / scope_script.name)
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
            errors="replace",
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

    def test_windows_powershell_accepts_utf8_reports_with_korean_test_names(self):
        report = self.repository / "korean-report.xml"
        report.write_text(
            '<?xml version="1.0" encoding="utf-8"?>\n'
            '<test-run result="Passed" passed="1" failed="0" skipped="0">\n'
            '<test-case name="추천 난이도를 미리 고른다" '
            'fullname="LevelSettingPreselectionTests.추천 난이도를 미리 고른다" '
            'result="Passed" />\n'
            "</test-run>\n",
            encoding="utf-8",
        )
        with (self.repository / "editor.ps1").open("a", encoding="ascii") as editor:
            editor.write(
                "Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'korean-report.xml') "
                "-Destination $args[$resultIndex + 1] -Force\n"
            )

        self.assert_full_run(*self.run_tests())

    def create_assemblies(self):
        definitions = {
            "Scripts/Api": ("A305.Api", []),
            "Scripts/Player": ("A305.Player", ["A305.Api"]),
            "Scripts/Dialogue": ("A305.Dialogue", ["A305.Player"]),
            "Scripts/Learning": ("A305.Learning", []),
            "Tests/EditMode/Player": ("A305.Player.Tests", ["A305.Player"]),
            "Tests/PlayMode/Player": ("A305.Player.PlayModeTests", ["A305.Player"]),
            "Tests/EditMode/Dialogue": ("A305.Dialogue.Tests", ["A305.Dialogue"]),
            "Tests/EditMode/Learning": ("A305.Learning.Tests", ["A305.Learning"]),
        }
        files = {}
        for directory, (name, references) in definitions.items():
            files[f"frontend/UnityClient/Assets/{directory}/{name}.asmdef"] = (
                json.dumps({"name": name, "references": references})
            )
        files["frontend/UnityClient/Assets/Tests/EditMode/Player/PlayerTests.cs"] = (
            "base\n"
        )
        self.base = self.commit(files)

    def test_changing_only_an_editmode_test_does_not_run_other_modules_or_playmode(
        self,
    ):
        self.create_assemblies()
        self.commit(
            {
                "frontend/UnityClient/Assets/Tests/EditMode/Player/PlayerTests.cs": "changed\n"
            }
        )

        runs, prepared = self.run_tests("-ChangedSince", self.base)

        self.assertEqual(len(runs), 1, runs)
        self.assertIn("-testPlatform EditMode", runs[0])
        self.assertIn("-assemblyNames A305.Player.Tests", runs[0])
        self.assertFalse(prepared)

    def test_runtime_change_selects_tests_of_transitive_dependents_only(self):
        self.create_assemblies()
        self.commit({"frontend/UnityClient/Assets/Scripts/Api/Client.cs": "changed\n"})

        runs, prepared = self.run_tests("-ChangedSince", self.base)

        self.assertEqual(len(runs), 2, runs)
        self.assertIn("-assemblyNames A305.Dialogue.Tests;A305.Player.Tests", runs[0])
        self.assertIn("-assemblyNames A305.Player.PlayModeTests", runs[1])
        self.assertTrue(all("A305.Learning.Tests" not in run for run in runs))
        self.assertFalse(prepared)

    def test_unrelated_backend_change_does_not_widen_a_mapped_unity_change(self):
        self.create_assemblies()
        self.commit({PLAYER_SCRIPT: "changed\n", "backend/Example.java": "changed\n"})
        runs, prepared = self.run_tests("-ChangedSince", self.base)
        self.assertIn("-assemblyNames A305.Dialogue.Tests;A305.Player.Tests", runs[0])
        self.assertIn("-assemblyNames A305.Player.PlayModeTests", runs[1])
        self.assertFalse(prepared)

    def test_deleted_test_still_selects_its_existing_test_assembly(self):
        self.create_assemblies()
        self.commit(
            {"frontend/UnityClient/Assets/Tests/EditMode/Player/PlayerTests.cs": None}
        )
        runs, prepared = self.run_tests("-ChangedSince", self.base)
        self.assertEqual(len(runs), 1)
        self.assertIn("-assemblyNames A305.Player.Tests", runs[0])
        self.assertFalse(prepared)

    def test_guid_references_are_included_in_the_dependency_graph(self):
        self.create_assemblies()
        self.base = self.commit(
            {
                "frontend/UnityClient/Assets/Scripts/Player/A305.Player.asmdef.meta": "guid: "
                + "1" * 32
                + "\n",
                "frontend/UnityClient/Assets/Tests/EditMode/Player/A305.Player.Tests.asmdef": json.dumps(
                    {"name": "A305.Player.Tests", "references": ["GUID:" + "1" * 32]}
                ),
            }
        )
        self.commit({PLAYER_SCRIPT: "changed\n"})
        runs, _ = self.run_tests("-ChangedSince", self.base)
        self.assertIn("-assemblyNames A305.Dialogue.Tests;A305.Player.Tests", runs[0])

    def test_playmode_test_change_runs_only_its_playmode_assembly(self):
        self.create_assemblies()
        self.commit(
            {
                "frontend/UnityClient/Assets/Tests/PlayMode/Player/CameraTests.cs": "changed\n"
            }
        )
        runs, prepared = self.run_tests("-ChangedSince", self.base)
        self.assertEqual(len(runs), 1)
        self.assertIn("-testPlatform PlayMode", runs[0])
        self.assertIn("-assemblyNames A305.Player.PlayModeTests", runs[0])
        self.assertFalse(prepared)

    def test_template_changes_also_run_deployment_tests_when_code_changes(self):
        self.create_assemblies()
        self.commit({PLAYER_SCRIPT: "changed\n", TEMPLATE: "changed\n"})
        runs, prepared = self.run_tests("-ChangedSince", self.base)
        self.assertIn(
            "-assemblyNames A305.Deployment.Tests;A305.Dialogue.Tests;A305.Player.Tests",
            runs[0],
        )
        self.assertIn("-assemblyNames A305.Player.PlayModeTests", runs[1])
        self.assertFalse(prepared)

    def test_tripo_test_change_prepares_inputs_for_the_selected_assembly(self):
        self.create_assemblies()
        assembly = (
            "frontend/UnityClient/Assets/Tests/EditMode/Tripo/A305.Tripo.Tests.asmdef"
        )
        self.base = self.commit(
            {assembly: json.dumps({"name": "A305.Tripo.Tests", "references": []})}
        )
        self.commit(
            {
                "frontend/UnityClient/Assets/Tests/EditMode/Tripo/ModelTests.cs": "changed\n"
            }
        )
        runs, prepared = self.run_tests("-ChangedSince", self.base)
        self.assertEqual(len(runs), 1)
        self.assertIn("-assemblyNames A305.Tripo.Tests", runs[0])
        self.assertTrue(prepared)

    def test_scene_and_assembly_configuration_changes_run_every_test(self):
        for path in (
            "frontend/UnityClient/Assets/Scenes/TownScene.unity",
            "frontend/UnityClient/Assets/Scripts/Player/A305.Player.asmdef",
        ):
            with self.subTest(path=path):
                self.create_repository()
                self.create_assemblies()
                self.commit({path: "changed\n"})
                self.assert_full_run(*self.run_tests("-ChangedSince", self.base))

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

    def test_shared_or_unmapped_inputs_run_every_test(self):
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
