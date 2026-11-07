using System;
using System.IO;
using System.Linq;
using A305.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Characters
{
    public static partial class NpcEmotionBuilder
    {
        private static (
            GameObject root,
            CharacterSpeechPlayer player,
            Camera camera,
            Vector3 center,
            Vector3 front
        ) PrepareReview(string path)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            root.name = Path.GetFileNameWithoutExtension(path);
            foreach (var animation in root.GetComponentsInChildren<Animator>())
                animation.enabled = false;
            foreach (var blink in root.GetComponentsInChildren<CharacterIdleBlink>())
                blink.Suspended = true;
            var face = root.GetComponentInChildren<CharacterFaceController>();
            var player = ConfigurePlayer(face);
            var skin = face.GetComponentsInChildren<SkinnedMeshRenderer>()
                .OrderByDescending(s => s.sharedMesh.vertexCount)
                .First();
            var mesh = new Mesh();
            skin.BakeMesh(mesh);
            int head = Array.FindIndex(skin.bones, b => b.name == "Head");
            var weights = skin.sharedMesh.boneWeights;
            var points = mesh
                .vertices.Where((v, i) => HeadWeight(weights[i], head) > .8f)
                .Select(v => skin.transform.TransformPoint(v))
                .ToArray();
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var point in points)
                bounds.Encapsulate(point);
            Object.DestroyImmediate(mesh);
            var center = bounds.center;
            var front = skin.transform.TransformDirection(Vector3.left).normalized;
            var camera = new GameObject("표정 검수 카메라").AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(
                center + front * 3,
                Quaternion.LookRotation(-front)
            );
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(bounds.size.y, bounds.size.x, bounds.size.z) * .62f;
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 30;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.15f, .18f, .20f);
            var light = new GameObject("표정 검수 조명").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1;
            light.transform.rotation = Quaternion.LookRotation(-front + Vector3.down * .5f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.65f, .65f, .65f);
            return (root, player, camera, center, front);
        }

        public static void Render()
        {
            string only = Environment.GetEnvironmentVariable("NPC_EMOTION_CHARACTER");
            string phase = Environment.GetEnvironmentVariable("NPC_EMOTION_PHASE") ?? "After";
            bool speech = Environment.GetEnvironmentVariable("NPC_EMOTION_BATCH_SPEECH") == "1";
            var profile = AssetDatabase.LoadAssetAtPath<EmotionExpressionProfile>(ProfilePath);
            foreach (
                string name in Names
                    .Append("LittleMung_Cafe_Female")
                    .Where(n => string.IsNullOrEmpty(only) || only == n)
            )
            {
                string folder = Path.Combine(Output, name, phase);
                Directory.CreateDirectory(folder);
                var review = PrepareReview(PrefabPath(name));
                var target = new RenderTexture(640, 640, 24);
                var image = new Texture2D(640, 640, TextureFormat.RGB24, false);
                review.camera.targetTexture = target;
                try
                {
                    void Save(string file)
                    {
                        CafeFemaleFaceBuilder.RenderBakedPose(review.player.Face, review.camera);
                        RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
                        image.Apply();
                        File.WriteAllBytes(
                            Path.Combine(folder, file + ".png"),
                            image.EncodeToPNG()
                        );
                    }
                    foreach (
                        var emotion in Enum.GetValues(typeof(CharacterEmotion))
                            .Cast<CharacterEmotion>()
                    )
                    {
                        if (phase == "Before" && emotion != CharacterEmotion.Calm)
                            continue;
                        review.player.Face.ApplySpeech(
                            SpeechMouthSample.Rest,
                            profile.Sample(emotion),
                            0
                        );
                        foreach (int angle in new[] { 0, -30, 30 })
                        {
                            var direction = Quaternion.AngleAxis(angle, Vector3.up) * review.front;
                            review.camera.transform.SetPositionAndRotation(
                                review.center + direction * 3,
                                Quaternion.LookRotation(-direction)
                            );
                            Save(emotion + "_" + angle);
                        }
                    }
                    if (speech && phase != "Before")
                    {
                        review.camera.transform.SetPositionAndRotation(
                            review.center + review.front * 3,
                            Quaternion.LookRotation(-review.front)
                        );
                        var clip = AssetDatabase.LoadAssetAtPath<CharacterSpeechClip>(
                            CafeEmotionBuilder.DataFolder + "/CuriousPlayfulLaugh.asset"
                        );
                        review.player.Configure(
                            review.player.Face,
                            review.player.GetComponent<AudioSource>(),
                            clip
                        );
                        review.player.GetComponent<AudioSource>().mute = true;
                        review.player.Play();
                        review.player.Pause();
                        Directory.CreateDirectory(Path.Combine(folder, "Speech"));
                        for (int i = 0; i < Mathf.CeilToInt((clip.Audio.length + .8f) * 30); i++)
                        {
                            review.player.ApplyAtAudioTime(i / 30f - .3f);
                            Save("Speech/" + i.ToString("D4"));
                        }
                        review.player.Stop();
                    }
                }
                finally
                {
                    review.camera.targetTexture = null;
                    RenderTexture.active = null;
                    Object.DestroyImmediate(target);
                    Object.DestroyImmediate(image);
                }
            }
        }
    }
}
