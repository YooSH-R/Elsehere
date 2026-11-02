#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace A305.Characters.PlayModeTests
{
    public sealed class MaleNpcVariantPreviewTests
    {
        [UnityTest]
        public IEnumerator NpcPreviewKeepsSkinToneThroughSpeechAndBlink()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/Sandbox/Mannequin/LittleMung_Npc_Male_Face.unity",
                new UnityEngine.SceneManagement.LoadSceneParameters(
                    UnityEngine.SceneManagement.LoadSceneMode.Single
                )
            );
            var face = Object.FindFirstObjectByType<CharacterFaceController>();
            var tone = face.GetComponent<CharacterSkinTone>();
            Assert.That(tone, Is.Not.Null);
            var skins = face.GetComponentsInChildren<SkinnedMeshRenderer>();
            var lids = skins.First(s => s.sharedMesh.name == "UpperLeftLid");
            var lips = skins.First(s => s.sharedMesh.name == "SpeakingLips");
            var body = skins.OrderByDescending(s => s.sharedMesh.vertexCount).First();
            var mesh = new Mesh();
            try
            {
                face.SetPose(CharacterFacePose.Neutral);
                lips.BakeMesh(mesh, true);
                var rest = mesh.vertices;
                face.ApplySpeech(
                    SpeechMouthSample.Pose(CharacterFacePose.OpenMouth, .8f),
                    .2f,
                    .8f
                );
                yield return null;
                lips.BakeMesh(mesh, true);
                Assert.That(
                    mesh.vertices.Zip(rest, (a, b) => Vector3.Distance(a, b)).Max(),
                    Is.GreaterThan(.001f)
                );
                Assert.That(
                    lids.GetBlendShapeWeight(lids.sharedMesh.GetBlendShapeIndex("eyeBlinkLeft")),
                    Is.EqualTo(80).Within(.01f)
                );
                var block = new MaterialPropertyBlock();
                Vector4? expected = null;
                foreach (var skin in new[] { body, lids, lips })
                {
                    skin.GetPropertyBlock(block, 0);
                    var tint = block.GetVector(CharacterSkinTone.TintProperty);
                    Assert.That(tint.x, Is.InRange(.01f, .99f));
                    if (expected.HasValue)
                        Assert.That(tint, Is.EqualTo(expected.Value));
                    expected = tint;
                }
                face.SetPose(CharacterFacePose.Neutral);
                yield return null;
                lips.BakeMesh(mesh, true);
                CollectionAssert.AreEqual(rest, mesh.vertices);
            }
            finally
            {
                Object.Destroy(mesh);
            }
        }
    }
}
#endif
