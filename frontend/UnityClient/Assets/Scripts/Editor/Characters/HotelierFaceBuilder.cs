using System;
using System.IO;
using System.Linq;
using A305.Characters;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.Npc;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Characters
{
    public static class HotelierFaceBuilder
    {
        public const string OriginalPath =
            "Assets/Prefabs/Characters/Tripo/LittleMung_HotelStaff_male.prefab";
        public const string PrefabPath =
            "Assets/Prefabs/Npc/LittleMung_HotelStaff_male_Dialogue.prefab";

        public static void BuildFace()
        {
            if (!File.Exists(PrefabPath))
            {
                CafeFemaleFaceBuilder.BuildCopy(
                    OriginalPath,
                    PrefabPath,
                    "Assets/Art/Npc/HotelierFace",
                    "../ArtSources/Characters/HotelierFace/HotelierFaceShapes.json",
                    "HotelierFace",
                    false
                );
                FitCopyToServiceHeight();
            }
            RenderPoses();
        }

        private static (float height, float eyes, float minimum) MeasureEvaluatedCopy(
            GameObject root
        )
        {
            var animator = root.GetComponentInChildren<Animator>();
            animator.enabled = true;
            animator.Rebind();
            animator.Update(0);
            var body = root.GetComponentsInChildren<SkinnedMeshRenderer>()
                .Single(r => r.sharedMesh.name == "HotelierFace");
            var baked = new Mesh();
            try
            {
                body.BakeMesh(baked, true);
                var points = baked.vertices.Select(v => body.transform.TransformPoint(v)).ToArray();
                var original = body.sharedMesh.vertices;
                int eye = Enumerable
                    .Range(0, original.Length)
                    .OrderBy(i => (original[i] - new Vector3(-.215f, .582f, .105f)).sqrMagnitude)
                    .First();
                float low = points.Min(v => v.y),
                    high = points.Max(v => v.y);
                return (high - low, points[eye].y, low);
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
        }

        public static void FitCopyToServiceHeight()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var instance = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)
                );
            float multiplier;
            Vector3 soleOffset;
            try
            {
                var measured = MeasureEvaluatedCopy(instance);
                if (measured.height <= .1f)
                    throw new InvalidOperationException("Invalid evaluated mesh height.");
                multiplier = 1.7f / measured.height;
                soleOffset = instance.transform.InverseTransformVector(
                    Vector3.down * measured.minimum
                );
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                root.transform.localScale *= multiplier;
                root.transform.GetChild(0).localPosition += soleOffset;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            ConnectScenes();
        }

        public static void PreparePreviewScene()
        {
            var p = Preview(PrefabPath);
            var skin = p.root.GetComponentInChildren<SkinnedMeshRenderer>();
            var center = skin.transform.TransformPoint(new Vector3(-.12f, .66f, 0));
            new GameObject("Face controls")
                .AddComponent<FacePosePreviewPanel>()
                .Configure(p.face, p.camera, center, p.camera.transform.position - center);
            EditorSceneManager.SaveScene(
                p.root.scene,
                "Assets/Scenes/Sandbox/HotelierFacePreview.unity"
            );
        }

        private static (GameObject root, CharacterFaceController face, Camera camera) Preview(
            string path
        )
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = (GameObject)
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            foreach (var behavior in root.GetComponentsInChildren<MonoBehaviour>().Reverse())
                if (behavior is not CharacterFaceController)
                    Object.DestroyImmediate(behavior);
            root.GetComponentInChildren<Animator>().enabled = false;
            var face = root.GetComponentInChildren<CharacterFaceController>();
            if (face == null)
                face = root.AddComponent<CharacterFaceController>();
            var skin = root.GetComponentInChildren<SkinnedMeshRenderer>();
            var center = skin.transform.TransformPoint(new Vector3(-.12f, .66f, 0));
            var front = skin.transform.TransformDirection(Vector3.left);
            var camera = new GameObject("Face camera").AddComponent<Camera>();
            camera.transform.position = center + front * 3;
            camera.transform.LookAt(center);
            camera.orthographic = true;
            camera.orthographicSize = .31f * skin.transform.lossyScale.y;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.17f, .19f, .22f);
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 30;
            var light = new GameObject("Face light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.transform.rotation = Quaternion.LookRotation(-front + Vector3.down * .6f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.65f, .65f, .65f);
            return (root, face, camera);
        }

        public static void RenderPoses()
        {
            string output = Environment.GetEnvironmentVariable("HOTEL_FACE_OUTPUT");
            Directory.CreateDirectory(output);
            var preview = Preview(PrefabPath);
            var target = new RenderTexture(768, 768, 24);
            var image = new Texture2D(768, 768, TextureFormat.RGB24, false);
            preview.camera.targetTexture = target;
            foreach (CharacterFacePose pose in Enum.GetValues(typeof(CharacterFacePose)))
            {
                preview.face.SetPose(pose);
                CafeFemaleFaceBuilder.RenderBakedPose(preview.face, preview.camera);
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 768, 768), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(output, pose + ".png"), image.EncodeToPNG());
            }
            RenderTexture.active = null;
            preview.camera.targetTexture = null;
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
        }

        [MenuItem("Tools/A305/Hotel/Connect Dialogue Face")]
        public static void ConnectScenes()
        {
            if (!BuilderTools.CanBuild())
                return;
            foreach (
                string path in new[]
                {
                    "Assets/Scenes/Production/HotelScene.unity",
                    "Assets/Scenes/Sandbox/HotelInteriorSandbox.unity",
                }
            )
            {
                var scene = EditorSceneManager.OpenScene(path);
                var staff = scene
                    .GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<NpcIdentity>(true))
                    .Single(n => n.Code == "HOTEL_STAFF");
                Connect(staff);
                EditorSceneManager.SaveScene(scene, path);
            }
            BuildPreviewScene();
        }

        public static void Connect(NpcIdentity staff)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
                throw new InvalidOperationException("Author the hotelier face copy first.");
            var visual = staff.transform.Find("Hotelier");
            if (visual == null)
                throw new InvalidOperationException("Connect the existing hotelier first.");
            if (
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(visual.gameObject)
                != PrefabPath
            )
            {
                var replacement = (GameObject)
                    PrefabUtility.InstantiatePrefab(prefab, staff.transform);
                replacement.name = "Hotelier";
                replacement.transform.SetLocalPositionAndRotation(
                    visual.localPosition,
                    visual.localRotation
                );
                Object.DestroyImmediate(visual.gameObject);
                visual = replacement.transform;
            }
            BuilderTools.SetReference(
                staff.GetComponent<CharacterAnimationDriver>(),
                "animator",
                visual.GetComponentInChildren<Animator>()
            );
            var capsule = staff.GetComponent<CharacterController>();
            capsule.height = 1.7f;
            capsule.center = new Vector3(capsule.center.x, .85f, capsule.center.z);
            PrefabUtility.RecordPrefabInstancePropertyModifications(capsule);
            // Bone evaluation changes the mesh axes; measure the same posed copy used at runtime.
            var measurement = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            float eyeHeight;
            try
            {
                eyeHeight = MeasureEvaluatedCopy(measurement).eyes;
            }
            finally
            {
                Object.DestroyImmediate(measurement);
            }
            var look = staff.LookTarget.localPosition;
            look.y = eyeHeight;
            staff.LookTarget.localPosition = look;
            PrefabUtility.RecordPrefabInstancePropertyModifications(staff.LookTarget);
        }

        public static void BuildPreviewScene()
        {
            if (!File.Exists("Assets/Scenes/Sandbox/HotelierFacePreview.unity"))
                PreparePreviewScene();
        }

        // Offline verification uses the same WAV decoder, automatic binding and pose evaluator as dialogue.
        public static void RenderSpeechComparison()
        {
            string output = Environment.GetEnvironmentVariable("HOTEL_FACE_OUTPUT");
            string voice = Environment.GetEnvironmentVariable("HOTEL_FACE_WAV");
            byte[] bytes = File.ReadAllBytes(voice);
            var decoded = DialogueSpeechWav.Read(bytes);
            if (decoded.Timeline == null)
                throw new InvalidOperationException(decoded.TimingError);
            foreach (
                var entry in new[]
                {
                    ("Hotelier", PrefabPath),
                    ("Cafe", CafeDialogueBuilder.PrefabPath),
                }
            )
            {
                string folder = Path.Combine(output, entry.Item1);
                Directory.CreateDirectory(folder);
                var p = Preview(entry.Item2);
                // Preview disabled unrelated behaviours; only the speech path is needed here.
                var npc = new GameObject("Speech comparison NPC");
                p.root.transform.SetParent(npc.transform, true);
                npc.AddComponent<NpcIdentity>();
                var binding = npc.GetComponent<DialogueSpeaker>();
                if (binding == null)
                    binding = npc.AddComponent<DialogueSpeaker>();
                var player = binding.Player;
                if (player == null)
                    throw new InvalidOperationException(binding.BindingNotice);
                var link = npc.AddComponent<DialogueServerLink>();
                link.SetSpeaker(player);
                if (!link.PlayRecordedSpeech(bytes))
                    throw new InvalidOperationException("Voice decode failed.");
                var animator = p.root.GetComponentInChildren<Animator>();
                animator.enabled = true;
                animator.Rebind();
                animator.Update(0);
                CafeDialogueBuilder.FrameReviewFace(player, p.camera);
                var lipsSkin = player
                    .GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Single(r => r.name == "SpeakingLips");
                var bodySkin = player
                    .GetComponentsInChildren<SkinnedMeshRenderer>()
                    .OrderByDescending(r => r.sharedMesh.vertexCount)
                    .First();
                var framing = new Mesh();
                bodySkin.BakeMesh(framing, true);
                float top = framing.vertices.Max(v => bodySkin.transform.TransformPoint(v).y);
                lipsSkin.BakeMesh(framing, true);
                float lipsHeight = lipsSkin.transform.TransformPoint(framing.bounds.center).y;
                Object.DestroyImmediate(framing);
                p.camera.orthographic = true;
                p.camera.orthographicSize = Mathf.Max(.25f, (top - lipsHeight) * .9f);
                // Keep facial inspection lighting aligned with the evaluated Avatar's front.
                var light = Object.FindFirstObjectByType<Light>();
                light.transform.rotation =
                    p.camera.transform.rotation * Quaternion.Euler(15, -15, 0);
                light.intensity = .75f;
                RenderSettings.ambientLight = new Color(.8f, .8f, .8f);
                var target = new RenderTexture(768, 768, 24);
                var image = new Texture2D(768, 768, TextureFormat.RGB24, false);
                p.camera.targetTexture = target;
                int frames = Mathf.CeilToInt(decoded.Timeline.Duration * 30);
                for (int i = 0; i < frames; i++)
                {
                    if (i > 0)
                        animator.Update(1f / 30);
                    player.ApplyAtAudioTime(i / 30f);
                    CafeFemaleFaceBuilder.RenderBakedPose(player.Face, p.camera);
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 768, 768), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(
                        Path.Combine(folder, $"Frame{i:D5}.png"),
                        image.EncodeToPNG()
                    );
                }
                link.StopSpeech();
                RenderTexture.active = null;
                p.camera.targetTexture = null;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
                File.WriteAllText(
                    Path.Combine(folder, "verification.json"),
                    JsonUtility.ToJson(
                        new SpeechEvidence
                        {
                            frames = frames,
                            duration = decoded.Timeline.Duration,
                            missingChannels = CharacterFaceController.MissingChannels(
                                p.root.transform
                            ),
                        },
                        true
                    )
                );
            }
        }

        [Serializable]
        private sealed class SpeechEvidence
        {
            public int frames;
            public float duration;
            public string[] missingChannels;
        }

        [Serializable]
        private sealed class Surface
        {
            public Vector3[] vertices,
                normals;
            public Vector2[] uv;
            public int[] triangles;
            public string texture;
            public string[] bones;
        }

        public static void ExportSource()
        {
            string output = Environment.GetEnvironmentVariable("HOTEL_FACE_OUTPUT");
            Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(original);
            var skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (skins.Length != 1)
                throw new InvalidOperationException(
                    "Inspect the hotel's multiple meshes before authoring."
                );
            var skin = skins[0];
            var mesh = skin.sharedMesh;
            File.WriteAllText(
                Path.Combine(output, "source.json"),
                JsonUtility.ToJson(
                    new Surface
                    {
                        vertices = mesh.vertices,
                        normals = mesh.normals,
                        uv = mesh.uv,
                        triangles = mesh.triangles,
                        texture = AssetDatabase.GetAssetPath(
                            skin.sharedMaterial.GetTexture("_BaseMap")
                        ),
                        bones = skin.bones.Select(b => b.name).ToArray(),
                    }
                )
            );
            // Render the authored mesh coordinate system to locate facial landmarks.
            Object.DestroyImmediate(instance);
            var model = new GameObject("Source inspection");
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            model.AddComponent<MeshRenderer>().sharedMaterials =
                skins.Length == 1
                    ? original.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterials
                    : null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.8f, .8f, .8f);
            var light = new GameObject("Inspection light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            var camera = new GameObject("Inspection camera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = mesh.bounds.size.y * .55f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.7f, .75f, .8f);
            camera.farClipPlane = 100;
            var target = new RenderTexture(768, 768, 24);
            camera.targetTexture = target;
            var image = new Texture2D(768, 768, TextureFormat.RGB24, false);
            foreach (
                var direction in new[]
                {
                    Vector3.left,
                    Vector3.right,
                    Vector3.forward,
                    Vector3.back,
                }
            )
            {
                camera.transform.position = mesh.bounds.center + direction * 3;
                camera.transform.LookAt(mesh.bounds.center);
                light.transform.rotation = camera.transform.rotation;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 768, 768), 0, 0);
                image.Apply();
                File.WriteAllBytes(
                    Path.Combine(output, "source-" + direction.ToString() + ".png"),
                    image.EncodeToPNG()
                );
            }
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
        }
    }
}
