using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public class TripoBlenderBridgeTests
    {
        [Test, Category("BlenderIntegration")]
        public async Task RoundTrip_ImportsBonesWeightsAndExpressionIntoUnity_WithoutChangingSource()
        {
            string executable = Path.GetFullPath(
                "Library/TripoTools/blender-4.5.14-windows-x64/blender.exe"
            );
            if (!File.Exists(executable))
                Assert.Ignore("Blender 4.5.14 test installation is not available.");
            string folder = Path.GetFullPath(
                "UserSettings/Tripo/Tests/" + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(folder);
            string generated = Path.Combine(folder, "Generated.fbx");
            string blend = Path.Combine(folder, "Editable.blend");
            string assetFolder = "Assets/Tests/TripoBridge_" + Guid.NewGuid().ToString("N");
            string importedPath = assetFolder + "/Candidate.fbx";
            AssetDatabase.CreateFolder("Assets/Tests", Path.GetFileName(assetFolder));
            try
            {
                string fixture = Path.GetFullPath(
                    "../../infra/tests/tripo/create_blender_fixture.py"
                );
                var start = new ProcessStartInfo(
                    executable,
                    "--background --factory-startup --disable-autoexec --python-exit-code 1 --python \""
                        + fixture
                        + "\" -- \""
                        + generated
                        + "\""
                )
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using (Process process = Process.Start(start))
                {
                    Task<string> output = process.StandardOutput.ReadToEndAsync();
                    Task<string> errors = process.StandardError.ReadToEndAsync();
                    while (!process.HasExited)
                        await Task.Delay(100);
                    Assert.That(process.ExitCode, Is.Zero, await errors + await output);
                }
                var bridge = new TripoBlenderBridge(executable);
                await bridge.PrepareAsync(generated, blend, CancellationToken.None);
                Assert.That(File.Exists(blend), Is.True);
                byte[] savedSource = File.ReadAllBytes(blend);
                await bridge.ExportAsync(
                    blend,
                    Path.GetFullPath(importedPath),
                    CancellationToken.None
                );
                Assert.That(File.ReadAllBytes(blend), Is.EqualTo(savedSource));
                AssetDatabase.ImportAsset(importedPath, ImportAssetOptions.ForceSynchronousImport);
                var importer = (ModelImporter)AssetImporter.GetAtPath(importedPath);
                importer.importBlendShapes = true;
                importer.isReadable = true;
                importer.SaveAndReimport();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(importedPath);
                var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
                Assert.That(skin, Is.Not.Null);
                Assert.That(skin.bones, Is.Not.Empty);
                Assert.That(
                    skin.sharedMesh.GetBlendShapeIndex("mouthSmileLeft"),
                    Is.GreaterThanOrEqualTo(0)
                );
                Assert.That(skin.sharedMesh.boneWeights, Is.Not.Empty);
                var draftJob = new TripoJob
                {
                    autoRig = false,
                    modelPath = generated,
                    modelAssetPath = assetFolder + "/Draft.fbx",
                    prefabPath = assetFolder + "/Draft.prefab",
                    heightMeters = 1f,
                };
                var draft = TripoModelImporter.Import(draftJob);
                Assert.That(
                    draft,
                    Is.Not.Null,
                    "얼굴/부품 초안은 Humanoid 리깅 없이도 가져옵니다."
                );
                Assert.That(
                    ((ModelImporter)AssetImporter.GetAtPath(draftJob.modelAssetPath)).animationType,
                    Is.EqualTo(ModelImporterAnimationType.Generic)
                );
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetFolder);
                Directory.Delete(folder, true);
            }
        }
    }
}
