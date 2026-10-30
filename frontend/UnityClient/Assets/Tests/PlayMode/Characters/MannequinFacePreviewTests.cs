#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace A305.Characters.PlayModeTests
{
    public sealed class MannequinFacePreviewTests
    {
        [UnityTest]
        public IEnumerator FacePreviewDeformsAllPosesWithoutPlayerDependencies()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/Sandbox/Mannequin/LittleMung_Base_Male_Face.unity",
                new UnityEngine.SceneManagement.LoadSceneParameters(
                    UnityEngine.SceneManagement.LoadSceneMode.Single
                )
            );
            var face = Object.FindFirstObjectByType<CharacterFaceController>();
            Assert.That(face, Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<FacePosePreviewPanel>(), Is.Not.Null);
            var skins = face.GetComponentsInChildren<SkinnedMeshRenderer>();
            var mesh = new Mesh();
            try
            {
                face.SetPose(CharacterFacePose.Neutral);
                var neutral = skins.ToDictionary(
                    s => s,
                    s =>
                    {
                        s.BakeMesh(mesh, true);
                        return mesh.vertices;
                    }
                );
                foreach (CharacterFacePose pose in Enum.GetValues(typeof(CharacterFacePose)))
                {
                    face.SetPose(pose);
                    yield return null;
                    float maximumDeformation = 0;
                    foreach (var skin in skins)
                    {
                        skin.BakeMesh(mesh, true);
                        Assert.That(
                            mesh.vertices.All(v =>
                                float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z)
                            ),
                            Is.True
                        );
                        float moved = mesh
                            .vertices.Zip(neutral[skin], (a, b) => Vector3.Distance(a, b))
                            .Max();
                        maximumDeformation = Mathf.Max(maximumDeformation, moved);
                        if (
                            pose == CharacterFacePose.Blink
                            && (
                                skin.sharedMesh.name.EndsWith("Lashes", StringComparison.Ordinal)
                                || skin.sharedMesh.name.EndsWith("Lid", StringComparison.Ordinal)
                            )
                        )
                            Assert.That(
                                moved,
                                Is.GreaterThan(.001f),
                                skin.sharedMesh.name + " must follow the blink."
                            );
                        if (
                            pose == CharacterFacePose.Blink
                            && (
                                skin.sharedMesh.name == "LittleMungMannequinFace"
                                || skin.sharedMesh.name == "LeftEye"
                                || skin.sharedMesh.name == "RightEye"
                            )
                        )
                            Assert.That(
                                moved,
                                Is.LessThan(1e-6f),
                                "Blink must preserve the eyes and eyebrows."
                            );
                    }
                    if (pose != CharacterFacePose.Neutral)
                        Assert.That(maximumDeformation, Is.GreaterThan(.0001f), pose.ToString());
                    Capture(pose.ToString());
                }
                face.SetPose(CharacterFacePose.Neutral);
                foreach (var skin in skins)
                {
                    skin.BakeMesh(mesh, true);
                    CollectionAssert.AreEqual(neutral[skin], mesh.vertices);
                }
            }
            finally
            {
                Object.Destroy(mesh);
            }
        }

        private static void Capture(string name)
        {
            string folder = Environment.GetEnvironmentVariable("MANNEQUIN_FACE_CAPTURE");
            if (string.IsNullOrEmpty(folder))
                return;
            Directory.CreateDirectory(folder);
            var camera = Object.FindFirstObjectByType<Camera>();
            var target = RenderTexture.GetTemporary(800, 800, 24);
            var previousTarget = camera.targetTexture;
            var active = RenderTexture.active;
            var texture = new Texture2D(800, 800, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 800, 800), 0, 0);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(texture);
            }
        }
    }
}
#endif
