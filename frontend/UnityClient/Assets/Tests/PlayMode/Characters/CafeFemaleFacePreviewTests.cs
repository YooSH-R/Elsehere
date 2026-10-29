#if UNITY_EDITOR
using System;
using System.Collections;
using A305.Characters;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace A305.Characters.PlayModeTests
{
    public sealed class CafeFemaleFacePreviewTests
    {
        [UnityTest]
        public IEnumerator AllSixPosesDeformTheRenderedMeshAndReturnToNeutral()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/Sandbox/CafeFemaleFacePreview.unity",
                new UnityEngine.SceneManagement.LoadSceneParameters(
                    UnityEngine.SceneManagement.LoadSceneMode.Single
                )
            );
            var face = Object.FindFirstObjectByType<CharacterFaceController>();
            Assert.That(face, Is.Not.Null);
            var skin = face.GetComponentInChildren<SkinnedMeshRenderer>();
            var baked = new Mesh();
            try
            {
                face.SetPose(CharacterFacePose.Neutral);
                skin.BakeMesh(baked);
                var neutral = baked.vertices;
                foreach (CharacterFacePose pose in Enum.GetValues(typeof(CharacterFacePose)))
                {
                    face.SetPose(pose);
                    yield return null;
                    skin.BakeMesh(baked);
                    var points = baked.vertices;
                    float maximum = 0;
                    for (int i = 0; i < points.Length; i++)
                    {
                        Assert.That(
                            float.IsNaN(points[i].x) || float.IsInfinity(points[i].x),
                            Is.False
                        );
                        maximum = Mathf.Max(maximum, (points[i] - neutral[i]).magnitude);
                    }
                    if (pose != CharacterFacePose.Neutral)
                        Assert.That(maximum, Is.GreaterThan(.0001f), pose.ToString());
                }
                face.SetPose(CharacterFacePose.Neutral);
                skin.BakeMesh(baked);
                Assert.That(baked.vertices, Is.EqualTo(neutral));
                Assert.That(Object.FindFirstObjectByType<FacePosePreviewPanel>(), Is.Not.Null);
            }
            finally
            {
                Object.Destroy(baked);
            }
        }
    }
}
#endif
