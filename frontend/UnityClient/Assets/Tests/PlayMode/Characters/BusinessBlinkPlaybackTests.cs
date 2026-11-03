#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace A305.Characters.PlayModeTests
{
    public sealed class BusinessBlinkPlaybackTests
    {
        [UnityTest]
        public IEnumerator BusinessPrefabBlinksBothEyesWithoutResettingItsMouth()
        {
            yield return CheckBlink("LittleMung_Business_Male");
        }

        [UnityTest]
        public IEnumerator RemainingReviewNpcsBlinkWithoutResettingTheirMouth()
        {
            foreach (
                string name in new[]
                {
                    "LittleMung_MiddleAge_Male",
                    "LittleMung_MiddleAge_Female",
                    "LittleMung_BlackYoung_Male",
                    "LittleMung_Grandmother",
                }
            )
                yield return CheckBlink(name);
        }

        private static IEnumerator CheckBlink(string name)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Npc/Review/" + name + ".prefab"
            );
            var host = new GameObject("Character review host");
            host.AddComponent<CharacterController>();
            var root = Object.Instantiate(source, host.transform);
            try
            {
                var face = root.GetComponent<CharacterFaceController>();
                Assert.That(CharacterFaceController.MissingChannels(root.transform), Is.Empty);
                Assert.That(root.GetComponent<CharacterIdleBlink>(), Is.Not.Null);
                face.SetPose(CharacterFacePose.OpenMouth, .4f);
                var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>();
                float left = 0,
                    right = 0;
                float end = Time.time + 2f;
                while (Time.time < end)
                {
                    foreach (var skin in skins)
                    {
                        int l = skin.sharedMesh.GetBlendShapeIndex("eyeBlinkLeft");
                        int r = skin.sharedMesh.GetBlendShapeIndex("eyeBlinkRight");
                        if (l >= 0)
                            left = Mathf.Max(left, skin.GetBlendShapeWeight(l));
                        if (r >= 0)
                            right = Mathf.Max(right, skin.GetBlendShapeWeight(r));
                        int mouth = skin.sharedMesh.GetBlendShapeIndex("viseme_AA");
                        if (mouth >= 0)
                            Assert.That(
                                skin.GetBlendShapeWeight(mouth),
                                Is.EqualTo(40).Within(.01f)
                            );
                    }
                    yield return null;
                }
                Assert.That(left, Is.GreaterThan(99));
                Assert.That(right, Is.GreaterThan(99));
                var baked = new Mesh();
                try
                {
                    face.ApplyBlink(1);
                    foreach (var skin in skins)
                    {
                        skin.BakeMesh(baked, true);
                        Assert.That(
                            baked.vertices.All(v =>
                                float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z)
                            ),
                            Is.True
                        );
                    }
                }
                finally
                {
                    Object.DestroyImmediate(baked);
                }
            }
            finally
            {
                Object.Destroy(host);
            }
        }
    }
}
#endif
