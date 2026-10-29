using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public class TripoLocalRoundTripTests
    {
        [Serializable]
        private class Result
        {
            public string source;
            public string[] initialErrors;
            public string[] initialUvChecks;
            public int uvVerticesBefore;
            public int uvVerticesAfter;
            public string[] roundTripErrors;
            public string[] manualChecks;
            public int bonesBefore;
            public int bonesAfter;
            public int shapesBefore;
            public int shapesAfter;
            public string[] socketProbeErrors;
            public bool sourceUnchanged;
            public bool activeUnchanged;
            public bool automaticallyApproved;
        }

        [Serializable]
        private class SocketState
        {
            public string path;
            public Vector3 beforePosition;
            public Vector3 afterPosition;
            public Vector3 beforeScale;
            public Vector3 afterScale;
            public Vector3 beforeRotation;
            public Vector3 afterRotation;
            public float distance;
            public float angle;
        }

        [Serializable]
        private class SocketDiagnostics
        {
            public float beforeHeight;
            public float afterHeight;
            public SocketState[] bones;
            public string[] errors;
        }

        [TestCase(1), TestCase(2), Category("LocalModelIntegration")]
        public async Task UneditedRoundTrip_PreservesSocketPositionRotationAndScale(int roundTrips)
        {
            string source = Environment.GetEnvironmentVariable("TRIPO_REVIEW_MODEL");
            string executable = Environment.GetEnvironmentVariable("TRIPO_REVIEW_BLENDER");
            string output = Environment.GetEnvironmentVariable("TRIPO_REVIEW_OUTPUT");
            if (
                string.IsNullOrEmpty(source)
                || string.IsNullOrEmpty(executable)
                || string.IsNullOrEmpty(output)
            )
                Assert.Ignore("Local model and Blender paths are required.");
            string folder = "Assets/Tests/TripoSocketRoundTrip_" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(output);
            try
            {
                var record = TripoLocalWorkflow.ImportLocalFbx(
                    source,
                    TripoAssetKind.Character,
                    TripoAttachmentKind.Skinned,
                    folder
                );
                var before = AssetDatabase.LoadAssetAtPath<GameObject>(record.ModelPath);
                var head = before
                    .GetComponentsInChildren<Transform>(true)
                    .First(bone => bone.name == "Head");
                string headPath = AnimationUtility.CalculateTransformPath(head, before.transform);
                record.parts.Add(
                    new TripoPartSlot
                    {
                        name = "LeftEar",
                        bonePath = headPath,
                        position = Vector3.left * .02f,
                        rotation = new Vector3(10, 20, 30),
                    }
                );
                record.parts.Add(
                    new TripoPartSlot
                    {
                        name = "RightEar",
                        bonePath = headPath,
                        position = Vector3.right * .02f,
                        rotation = new Vector3(-10, -20, -30),
                    }
                );
                for (int roundTrip = 1; roundTrip <= roundTrips; roundTrip++)
                {
                    record.blenderSourcePath = Path.Combine(
                        output,
                        "SocketSource_" + Guid.NewGuid().ToString("N") + ".blend"
                    );
                    var bridge = new TripoBlenderBridge(executable);
                    await bridge.PrepareAsync(
                        Path.GetFullPath(record.ModelPath),
                        record.blenderSourcePath,
                        CancellationToken.None
                    );
                    var report = await TripoLocalWorkflow.ImportBlenderCandidateAsync(
                        record,
                        executable,
                        CancellationToken.None,
                        folder
                    );
                    var after = AssetDatabase.LoadAssetAtPath<GameObject>(record.ModelPath);
                    var diagnostics = new SocketDiagnostics
                    {
                        beforeHeight = TripoWorkbench.GetHeight(before),
                        afterHeight = TripoWorkbench.GetHeight(after),
                        errors = report.Errors.ToArray(),
                        bones = before
                            .GetComponentsInChildren<Transform>(true)
                            .Select(bone =>
                            {
                                string path = AnimationUtility.CalculateTransformPath(
                                    bone,
                                    before.transform
                                );
                                var target = string.IsNullOrEmpty(path)
                                    ? after.transform
                                    : after.transform.Find(path);
                                Assert.That(target, Is.Not.Null, "왕복 후 변환 경로 누락: " + path);
                                Vector3 beforePosition = before.transform.InverseTransformPoint(
                                    bone.TransformPoint(Vector3.right * .02f)
                                );
                                Vector3 afterPosition = after.transform.InverseTransformPoint(
                                    target.TransformPoint(Vector3.right * .02f)
                                );
                                Quaternion beforeRotation =
                                    Quaternion.Inverse(before.transform.rotation) * bone.rotation;
                                Quaternion afterRotation =
                                    Quaternion.Inverse(after.transform.rotation) * target.rotation;
                                return new SocketState
                                {
                                    path = path,
                                    beforePosition = beforePosition,
                                    afterPosition = afterPosition,
                                    beforeRotation = beforeRotation.eulerAngles,
                                    afterRotation = afterRotation.eulerAngles,
                                    beforeScale = bone.lossyScale,
                                    afterScale = target.lossyScale,
                                    distance = Vector3.Distance(beforePosition, afterPosition),
                                    angle = Quaternion.Angle(beforeRotation, afterRotation),
                                };
                            })
                            .ToArray(),
                    };
                    File.WriteAllText(
                        Path.Combine(output, $"socket-diagnostics-{roundTrips}-{roundTrip}.json"),
                        JsonUtility.ToJson(diagnostics, true)
                    );
                    Assert.That(
                        report.Errors.Where(error => !error.Contains("마스터")),
                        Is.Empty,
                        "왕복 보존 오류는 없어야 하며 서비스 마스터 불일치는 별도로 검출해야 합니다."
                    );
                    Assert.That(report.Errors, Has.Some.Contains("마스터"));
                    Assert.That(record.revision.validationPassed, Is.False);
                    var measuredHead = diagnostics.bones.First(bone => bone.path == headPath);
                    Assert.That(
                        measuredHead.distance,
                        Is.LessThan(
                            diagnostics.beforeHeight * record.profile.SocketPositionHeightRatio
                        )
                    );
                    Assert.That(measuredHead.angle, Is.LessThan(record.profile.SocketAngleDegrees));
                    Assert.That(
                        Vector3.Distance(measuredHead.beforeScale, measuredHead.afterScale),
                        Is.LessThan(.0001f)
                    );
                    Assert.That(record.parts[0].position, Is.EqualTo(Vector3.left * .02f));
                    Assert.That(record.parts[0].rotation, Is.EqualTo(new Vector3(10, 20, 30)));
                    foreach (var bone in diagnostics.bones)
                    {
                        Assert.That(
                            bone.distance,
                            Is.LessThan(
                                diagnostics.beforeHeight * record.profile.SocketPositionHeightRatio
                            ),
                            bone.path
                        );
                        Assert.That(
                            bone.angle,
                            Is.LessThan(record.profile.SocketAngleDegrees),
                            bone.path
                        );
                        Assert.That(
                            Vector3.Distance(bone.beforeScale, bone.afterScale),
                            Is.LessThan(.0001f),
                            bone.path
                        );
                    }
                }
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test, Category("LocalModelIntegration")]
        public async Task LocalFbx_BlenderRoundTrip_RechecksCandidateAndPreservesOriginal()
        {
            string source = Environment.GetEnvironmentVariable("TRIPO_REVIEW_MODEL");
            string executable = Environment.GetEnvironmentVariable("TRIPO_REVIEW_BLENDER");
            string output = Environment.GetEnvironmentVariable("TRIPO_REVIEW_OUTPUT");
            if (
                string.IsNullOrEmpty(source)
                || string.IsNullOrEmpty(executable)
                || string.IsNullOrEmpty(output)
            )
                Assert.Ignore(
                    "Supply local model, Blender and report paths to run without API or UI."
                );
            string folder = "Assets/Tests/TripoLocalRoundTrip_" + Guid.NewGuid().ToString("N");
            byte[] original = File.ReadAllBytes(source);
            Directory.CreateDirectory(output);
            try
            {
                var record = TripoLocalWorkflow.ImportLocalFbx(
                    source,
                    TripoAssetKind.Character,
                    TripoAttachmentKind.Skinned,
                    folder
                );
                string active = record.revision.activePath;
                Assert.That(
                    record.revision.validatedFingerprint,
                    Is.EqualTo(record.GetFingerprint())
                );
                byte[] activeBytes = File.ReadAllBytes(active);
                var before = AssetDatabase.LoadAssetAtPath<GameObject>(active);
                var beforeSkins = before.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var result = new Result
                {
                    source = source,
                    initialErrors = record.validationErrors.ToArray(),
                    initialUvChecks = record
                        .manualChecks.Where(item => item.Contains("UV0"))
                        .ToArray(),
                    uvVerticesBefore = beforeSkins.Sum(skin => skin.sharedMesh.uv.Length),
                    bonesBefore = beforeSkins.Sum(skin => skin.bones.Length),
                    shapesBefore = beforeSkins.Sum(skin => skin.sharedMesh.blendShapeCount),
                };
                record.blenderSourcePath = Path.Combine(
                    output,
                    "Editable_" + Guid.NewGuid().ToString("N") + ".blend"
                );
                var bridge = new TripoBlenderBridge(executable);
                await bridge.PrepareAsync(
                    Path.GetFullPath(active),
                    record.blenderSourcePath,
                    CancellationToken.None
                );
                byte[] editable = File.ReadAllBytes(record.blenderSourcePath);
                var report = await TripoLocalWorkflow.ImportBlenderCandidateAsync(
                    record,
                    executable,
                    CancellationToken.None,
                    folder
                );
                var candidate = AssetDatabase.LoadAssetAtPath<GameObject>(record.ModelPath);
                var afterSkins = candidate.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Transform head = before
                    .GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(bone => bone.name == "Head");
                if (head != null)
                {
                    string headPath = AnimationUtility.CalculateTransformPath(
                        head,
                        before.transform
                    );
                    var slots = new[]
                    {
                        new TripoPartSlot
                        {
                            name = "LeftEarProbe",
                            bonePath = headPath,
                            position = Vector3.left * 0.02f,
                        },
                        new TripoPartSlot
                        {
                            name = "RightEarProbe",
                            bonePath = headPath,
                            position = Vector3.right * 0.02f,
                        },
                    };
                    result.socketProbeErrors = TripoRoundTripAudit
                        .Compare(before, candidate, slots, record.profile)
                        .Errors.ToArray();
                    record.parts.AddRange(slots);
                    report = TripoWorkbench.Validate(record);
                    Assert.That(report.Errors, Is.SupersetOf(result.socketProbeErrors));
                }
                result.roundTripErrors = report.Errors.ToArray();
                result.manualChecks = report.ManualChecks.ToArray();
                result.bonesAfter = afterSkins.Sum(skin => skin.bones.Length);
                result.uvVerticesAfter = afterSkins.Sum(skin => skin.sharedMesh.uv.Length);
                result.shapesAfter = afterSkins.Sum(skin => skin.sharedMesh.blendShapeCount);
                result.sourceUnchanged = original.SequenceEqual(File.ReadAllBytes(source));
                result.activeUnchanged = activeBytes.SequenceEqual(File.ReadAllBytes(active));
                result.automaticallyApproved = record.revision.visuallyApproved;
                File.WriteAllText(
                    Path.Combine(output, "roundtrip.json"),
                    JsonUtility.ToJson(result, true)
                );
                Assert.That(result.sourceUnchanged && result.activeUnchanged, Is.True);
                Assert.That(File.ReadAllBytes(record.blenderSourcePath), Is.EqualTo(editable));
                Assert.That(record.revision.activePath, Is.EqualTo(active));
                Assert.That(result.automaticallyApproved, Is.False);
                Assert.That(
                    record.revision.validatedFingerprint,
                    Is.EqualTo(record.GetFingerprint())
                );
                Assert.That(result.bonesBefore, Is.GreaterThan(0));
                Assert.That(result.bonesAfter, Is.EqualTo(result.bonesBefore));
                Assert.That(result.shapesAfter, Is.EqualTo(result.shapesBefore));
                Assert.That(report.Errors, Has.None.Contains("재가져오기"));
                Assert.That(record.validationErrors, Is.EqualTo(report.Errors));
                Assert.Throws<InvalidOperationException>(() =>
                    record.revision.Apply(record.GetFingerprint())
                );
                TripoLocalWorkflow.DiscardCandidate(record);
                Assert.That(record.ModelPath, Is.EqualTo(active));
                Assert.That(record.revision.visuallyApproved, Is.False);
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
