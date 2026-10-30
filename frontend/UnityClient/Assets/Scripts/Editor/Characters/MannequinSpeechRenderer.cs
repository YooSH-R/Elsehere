using System;
using System.IO;
using System.Security.Cryptography;
using A305.Characters;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Characters
{
    // Offline evidence renderer: consumes an existing WAV and its exact phoneme
    // timeline. It neither synthesizes speech nor modifies a scene or source asset.
    public static class MannequinSpeechRenderer
    {
        [Serializable]
        private sealed class Identity
        {
            public string audioSha256 = "";
            public float duration = 0;
        }

        public static void Render()
        {
            string audioPath = RequirePath("MANNEQUIN_SPEECH_AUDIO");
            string timelinePath = RequirePath("MANNEQUIN_SPEECH_TIMELINE");
            string output = RequirePath("MANNEQUIN_SPEECH_OUTPUT");
            string json = File.ReadAllText(timelinePath);
            var identity = JsonUtility.FromJson<Identity>(json);
            using var hash = SHA256.Create();
            string actual = BitConverter
                .ToString(hash.ComputeHash(File.ReadAllBytes(audioPath)))
                .Replace("-", "");
            if (!string.Equals(actual, identity.audioSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "The phoneme timeline must match the existing audio."
                );
            var timeline = SpeechVisemeTimeline.Parse(json, identity.duration);
            Directory.CreateDirectory(output);
            EditorSceneManager.OpenScene(
                MannequinMasterBuilder.SceneFolder + "/LittleMung_Base_Female_Face.unity",
                OpenSceneMode.Single
            );
            var face = Object.FindFirstObjectByType<CharacterFaceController>();
            var camera = Object.FindFirstObjectByType<Camera>();
            const int size = 960;
            const int fps = 30;
            const float lead = .5f;
            int count = Mathf.CeilToInt((timeline.Duration + lead + .6f) * fps);
            var target = new RenderTexture(size, size, 24);
            var picture = new Texture2D(size, size, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                for (int frame = 0; frame < count; frame++)
                {
                    float time = frame / (float)fps - lead;
                    float blink = Mathf.SmoothStep(
                        0,
                        1,
                        Mathf.Max(0, 1 - Mathf.Abs(time - 1.5f) / .12f)
                    );
                    face.ApplySpeech(timeline.SampleArticulation(time), .15f, blink);
                    RenderBakedPose(face, camera);
                    RenderTexture.active = target;
                    picture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                    picture.Apply();
                    File.WriteAllBytes(
                        Path.Combine(output, frame.ToString("D4") + ".png"),
                        picture.EncodeToPNG()
                    );
                }
                Debug.Log(
                    $"Mannequin speech: {count} frames, {fps} fps, audio lead {lead}s, verified local audio {timeline.Duration}s."
                );
            }
            finally
            {
                face.SetPose(CharacterFacePose.Neutral);
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(picture);
                Object.DestroyImmediate(target);
            }
        }

        private static string RequirePath(string key)
        {
            string path = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Set " + key);
            return Path.GetFullPath(path);
        }

        private static void RenderBakedPose(CharacterFaceController face, Camera camera)
        {
            var skins = face.GetComponentsInChildren<SkinnedMeshRenderer>();
            var meshes = new Mesh[skins.Length];
            var objects = new GameObject[skins.Length];
            var enabled = new bool[skins.Length];
            try
            {
                for (int i = 0; i < skins.Length; i++)
                {
                    meshes[i] = new Mesh();
                    skins[i].BakeMesh(meshes[i], true);
                    objects[i] = new GameObject("ReviewPose");
                    objects[i].transform.SetParent(skins[i].transform, false);
                    objects[i].AddComponent<MeshFilter>().sharedMesh = meshes[i];
                    objects[i].AddComponent<MeshRenderer>().sharedMaterials = skins[
                        i
                    ].sharedMaterials;
                    enabled[i] = skins[i].enabled;
                    skins[i].enabled = false;
                }
                camera.Render();
            }
            finally
            {
                for (int i = 0; i < skins.Length; i++)
                {
                    skins[i].enabled = enabled[i];
                    if (objects[i] != null)
                        Object.DestroyImmediate(objects[i]);
                    if (meshes[i] != null)
                        Object.DestroyImmediate(meshes[i]);
                }
            }
        }
    }
}
