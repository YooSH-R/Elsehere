#if UNITY_EDITOR
using System.Collections;
using System.Globalization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace A305.Characters.Tests
{
    public sealed class LittleMungCorrectiveIntegrationTests
    {
        [Test]
        public void AllReferencePosesMatchBlenderAtThreeCharacterScales()
        {
            const string folder = "Assets/Art/Characters/LittleMungCorrectiveReview/";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Characters/LittleMungCorrectiveReview.prefab"
            );
            AnimationClip diagnostic = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                folder + "Diagnostic.anim"
            );
            TextAsset reference = AssetDatabase.LoadAssetAtPath<TextAsset>(
                folder + "CorrectiveReference.csv"
            );
            Assert.That(reference, Is.Not.Null);
            string[] lines = reference.text.Trim().Split('\n');
            string[] headers = lines[0].Trim().Split(',');
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                instance.GetComponentInChildren<Animator>().enabled = false;
                var driver = instance.GetComponentInChildren<PoseCorrectiveDriver>();
                var renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                foreach (float scale in new[] { 0.85f, 1f, 1.15f })
                {
                    instance.transform.localScale = Vector3.one * scale;
                    for (int frame = 1; frame <= 121; frame++)
                    {
                        diagnostic.SampleAnimation(driver.gameObject, (frame - 1) / 24f);
                        driver.ApplyCorrections();
                        string[] values = lines[frame].Trim().Split(',');
                        for (int column = 1; column < headers.Length; column++)
                        {
                            float expected =
                                float.Parse(values[column], CultureInfo.InvariantCulture) * 100f;
                            Assert.That(
                                renderer.GetBlendShapeWeight(
                                    FindShape(renderer.sharedMesh, headers[column])
                                ),
                                Is.EqualTo(expected).Within(0.05f),
                                $"frame {frame}, {headers[column]}, scale {scale}"
                            );
                        }
                        Assert.That(
                            instance.transform.lossyScale.x,
                            Is.EqualTo(scale).Within(0.0001f)
                        );
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [UnityTest]
        public IEnumerator ImportedCharacterAppliesCrouchCorrectionsAndClearsThemForGreeting()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Characters/LittleMungCorrectiveReview.prefab"
            );
            Assert.That(prefab, Is.Not.Null);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                foreach (float scale in new[] { 0.85f, 1f, 1.15f })
                {
                    instance.transform.localScale = Vector3.one * scale;
                    var animator = instance.GetComponentInChildren<Animator>();
                    var renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                    animator.speed = 0f;
                    animator.SetBool("Diagnostic", true);
                    animator.Play("Diagnostic - Original 121 Frames", 0, 61f / 120f);
                    animator.Update(0f);
                    yield return null;
                    yield return null;
                    foreach (
                        string name in new[]
                        {
                            "Corrective_KneeBend_Left_100",
                            "Corrective_KneeBend_Right_100",
                            "Corrective_ShortsCrouch_Left",
                            "Corrective_ShortsCrouch_Right",
                        }
                    )
                        Assert.That(
                            renderer.GetBlendShapeWeight(FindShape(renderer.sharedMesh, name)),
                            Is.GreaterThan(99f),
                            name
                        );
                    animator.SetBool("Diagnostic", false);
                    animator.Play("Shoulder Greeting - Palm Forward", 0, 0.25f);
                    animator.Update(0f);
                    yield return null;
                    yield return null;
                    for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                        Assert.That(renderer.GetBlendShapeWeight(i), Is.LessThan(0.01f));
                    Assert.That(instance.transform.lossyScale.x, Is.EqualTo(scale).Within(0.0001f));
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static int FindShape(Mesh mesh, string name)
        {
            for (int i = 0; i < mesh.blendShapeCount; i++)
                if (
                    mesh.GetBlendShapeName(i) == name
                    || mesh.GetBlendShapeName(i)
                        .EndsWith("." + name, System.StringComparison.Ordinal)
                )
                    return i;
            Assert.Fail("Missing shape: " + name);
            return -1;
        }
    }
}
#endif
