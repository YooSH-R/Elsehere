using System.Linq;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public class TripoCharacterHeadFitTests
    {
        [Test]
        public void CompositionRejectsDifferentHeadSizeAndOpenNeck()
        {
            var recipe = AssetDatabase.LoadAssetAtPath<TripoCharacterComposition>(
                TripoCharacterBootstrap.Folder + "/Variants/FemaleCasual/Composition.asset"
            );
            var report = TripoCharacterAssembly.Validate(recipe);
            Assert.That(
                report.Errors.Any(e => e.Contains("목 경계")),
                Is.True,
                "An incompatible neck cannot pass automatic validation."
            );
            Assert.That(
                report.Errors.Any(e => e.Contains("머리 크기")),
                Is.True,
                "A different head envelope must be reported separately."
            );
        }

        [Test]
        public void FemaleHeadFitsReferenceEnvelopeAndBodySeamWithoutChangingOriginal()
        {
            string folder = TripoCharacterBootstrap.Folder;
            var female = AssetDatabase
                .LoadAssetAtPath<GameObject>(folder + "/Heads/Female/Part.prefab")
                .GetComponentInChildren<SkinnedMeshRenderer>();
            var male = AssetDatabase
                .LoadAssetAtPath<GameObject>(folder + "/Head/Part.prefab")
                .GetComponentInChildren<SkinnedMeshRenderer>();
            var body = AssetDatabase
                .LoadAssetAtPath<GameObject>(folder + "/DressedBody/Part.prefab")
                .GetComponentInChildren<SkinnedMeshRenderer>();
            var original = female.sharedMesh.vertices;
            var mesh = TripoCharacterHeadFit.Fit(female, male);
            try
            {
                Vector3[] points = Common(female, mesh.vertices),
                    expected = Common(male, male.sharedMesh.vertices);
                var actualBounds = BoundsOf(points);
                var expectedBounds = BoundsOf(expected);
                Assert.That(
                    Vector3.Distance(actualBounds.size, expectedBounds.size),
                    Is.LessThan(.001f),
                    "Head size must match the reference in skeleton coordinates."
                );
                Assert.That(
                    Vector3.Distance(actualBounds.center, expectedBounds.center),
                    Is.LessThan(.001f)
                );
                var bodyPoints = Common(body, body.sharedMesh.vertices);
                var bodyWeights = body.sharedMesh.boneWeights;
                var weights = mesh.boneWeights;
                int seamCount = 0;
                for (int i = 0; i < bodyPoints.Length; i++)
                {
                    // The original reference partition shares this exact boundary with the body.
                    if (!expected.Any(p => Vector3.Distance(p, bodyPoints[i]) < .000001f))
                        continue;
                    seamCount++;
                    int match = System.Array.FindIndex(
                        points,
                        p => Vector3.Distance(p, bodyPoints[i]) < .000001f
                    );
                    Assert.That(
                        match,
                        Is.GreaterThanOrEqualTo(0),
                        "The fitted neck must meet every body seam vertex."
                    );
                    foreach (string bone in body.bones.Select(b => b.name))
                        Assert.That(
                            Influence(weights[match], female, bone),
                            Is.EqualTo(Influence(bodyWeights[i], body, bone)).Within(.00001f),
                            "Neck seam weights must remain identical during motion."
                        );
                }
                Assert.That(seamCount, Is.GreaterThanOrEqualTo(27));
                Assert.That(female.sharedMesh.vertices, Is.EqualTo(original));
                Assert.That(mesh.bindposes, Is.EqualTo(female.sharedMesh.bindposes));
                Assert.That(mesh.blendShapeCount, Is.EqualTo(female.sharedMesh.blendShapeCount));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void FittedNeckStaysClosedDuringSharedMotion()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                TripoCharacterBootstrap.Folder + "/Variants/FemaleCasual/Visual.prefab"
            );
            Assert.That(prefab, Is.Not.Null, "The original common female candidate must exist.");
            var model = Object.Instantiate(prefab);
            var head = model
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .Single(s => s.name.StartsWith("Head_"));
            var body = model
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .Single(s => s.name.StartsWith("DressedBody_"));
            var reference = AssetDatabase
                .LoadAssetAtPath<GameObject>(TripoCharacterBootstrap.Folder + "/Head/Part.prefab")
                .GetComponentInChildren<SkinnedMeshRenderer>();
            var fitted = TripoCharacterHeadFit.Fit(head, reference);
            head.sharedMesh = fitted;
            var points = Common(head, head.sharedMesh.vertices);
            var bodyPoints = Common(body, body.sharedMesh.vertices);
            var pairs = bodyPoints
                .Select(
                    (p, i) =>
                        (
                            body: i,
                            head: System.Array.FindIndex(
                                points,
                                q => Vector3.Distance(p, q) < .000001f
                            )
                        )
                )
                .Where(p => p.head >= 0)
                .ToArray();
            Assert.That(pairs.Length, Is.GreaterThanOrEqualTo(27));
            var bakedHead = new Mesh();
            var bakedBody = new Mesh();
            float worst = 0;
            try
            {
                using (var motion = new TripoMotionPreview(model))
                    foreach (
                        var action in new[]
                        {
                            TripoReviewMotion.Idle,
                            TripoReviewMotion.Walk,
                            TripoReviewMotion.Run,
                            TripoReviewMotion.Rise,
                            TripoReviewMotion.Fall,
                            TripoReviewMotion.Land,
                        }
                    )
                        for (int phase = 0; phase < 5; phase++)
                        {
                            motion.Apply(action, phase / 4f);
                            head.BakeMesh(bakedHead, true);
                            body.BakeMesh(bakedBody, true);
                            var h = bakedHead.vertices;
                            var b = bakedBody.vertices;
                            foreach (var pair in pairs)
                                worst = Mathf.Max(
                                    worst,
                                    Vector3.Distance(
                                        head.transform.TransformPoint(h[pair.head]),
                                        body.transform.TransformPoint(b[pair.body])
                                    )
                                );
                        }
                Debug.Log("Fitted neck maximum animated seam gap: " + worst.ToString("G9"));
                Assert.That(
                    worst,
                    Is.LessThan(.00001f),
                    "The shared body/head seam must not reopen during animation."
                );
            }
            finally
            {
                Object.DestroyImmediate(model);
                Object.DestroyImmediate(bakedHead);
                Object.DestroyImmediate(bakedBody);
                Object.DestroyImmediate(fitted);
            }
        }

        [Test]
        public void NeckBridgeDoesNotIntroduceOpenEdges()
        {
            string folder = TripoCharacterBootstrap.Folder;
            var head = AssetDatabase
                .LoadAssetAtPath<GameObject>(folder + "/Heads/Female/Part.prefab")
                .GetComponentInChildren<SkinnedMeshRenderer>();
            var reference = AssetDatabase
                .LoadAssetAtPath<GameObject>(folder + "/Head/Part.prefab")
                .GetComponentInChildren<SkinnedMeshRenderer>();
            var fitted = TripoCharacterHeadFit.Fit(head, reference);
            try
            {
                Assert.That(
                    BoundaryCount(fitted, Common(head, fitted.vertices)),
                    Is.EqualTo(
                        BoundaryCount(
                            reference.sharedMesh,
                            Common(reference, reference.sharedMesh.vertices)
                        )
                    ),
                    "The neck bridge must not create triangular holes between zigzag boundary edges."
                );
            }
            finally
            {
                Object.DestroyImmediate(fitted);
            }
        }

        static int BoundaryCount(Mesh mesh, Vector3[] points)
        {
            var keys = points.Select(p => Vector3Int.RoundToInt(p * 1000000)).ToArray();
            var ids = keys.Distinct().Select((p, i) => (p, i)).ToDictionary(v => v.p, v => v.i);
            var count = new System.Collections.Generic.Dictionary<(int, int), int>();
            var triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            for (int corner = 0; corner < 3; corner++)
            {
                int a = ids[keys[triangles[i + corner]]],
                    b = ids[keys[triangles[i + (corner + 1) % 3]]];
                if (a == b)
                    continue;
                var edge = a < b ? (a, b) : (b, a);
                count.TryGetValue(edge, out int uses);
                count[edge] = uses + 1;
            }
            return count.Count(pair => pair.Value == 1);
        }

        static float Influence(BoneWeight w, SkinnedMeshRenderer skin, string name)
        {
            return (skin.bones[w.boneIndex0].name == name ? w.weight0 : 0)
                + (skin.bones[w.boneIndex1].name == name ? w.weight1 : 0)
                + (skin.bones[w.boneIndex2].name == name ? w.weight2 : 0)
                + (skin.bones[w.boneIndex3].name == name ? w.weight3 : 0);
        }

        static Vector3[] Common(SkinnedMeshRenderer skin, Vector3[] points)
        {
            var root = skin.transform.root.GetComponentInChildren<Animator>().transform;
            return points
                .Select(p => root.InverseTransformPoint(skin.transform.TransformPoint(p)))
                .ToArray();
        }

        static Bounds BoundsOf(Vector3[] points)
        {
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var p in points)
                bounds.Encapsulate(p);
            return bounds;
        }
    }
}
