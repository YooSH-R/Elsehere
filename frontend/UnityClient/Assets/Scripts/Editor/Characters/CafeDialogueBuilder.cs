using System;
using System.Linq;
using A305.Characters;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.UI;
using A305.Npc;
using A305.Player;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Characters
{
    public static class CafeDialogueBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Npc/CafeFemaleDialogue.prefab";
        public const string SandboxPath = "Assets/Scenes/Sandbox/CafeDialogueSpeechSandbox.unity";
        private const string OriginalPath = "Assets/Prefabs/Npc/Barista.prefab";
        private const string ProductionPath = "Assets/Scenes/Production/CafeScene.unity";

        [MenuItem("Tools/A305/Cafe/Connect Dialogue Face")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            CreatePrefab();
            foreach (
                string path in new[]
                {
                    ProductionPath,
                    "Assets/Scenes/Sandbox/CafeInteriorSandbox.unity",
                }
            )
            {
                Scene scene = EditorSceneManager.OpenScene(path);
                Connect(Staff(scene));
                EditorSceneManager.SaveScene(scene, path);
            }
            BuildOfflineReview();
            AssetDatabase.SaveAssets();
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
        }

        private static void CreatePrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                return;
            var wrapper = new GameObject("CafeFemaleDialogue");
            try
            {
                var original = (GameObject)
                    PrefabUtility.InstantiatePrefab(
                        AssetDatabase.LoadAssetAtPath<GameObject>(OriginalPath)
                    );
                original.transform.SetParent(wrapper.transform, false);
                original.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                original.transform.localScale = Vector3.one;
                var authored = AssetDatabase.LoadAssetAtPath<GameObject>(
                    CafeFemaleFaceBuilder.PrefabPath
                );
                var authoredBody = authored
                    .GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Single(s => s.sharedMesh.name == "CafeFemaleFace");
                var body = original
                    .GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Single(s => s.sharedMesh.vertexCount == authoredBody.sharedMesh.vertexCount);
                if (
                    body.bones.Length != authoredBody.bones.Length
                    || !body
                        .bones.Select(b => b.name)
                        .SequenceEqual(authoredBody.bones.Select(b => b.name))
                )
                    throw new InvalidOperationException(
                        "Authored face does not match the cafe skeleton."
                    );
                body.sharedMesh = authoredBody.sharedMesh;
                body.sharedMaterials = authoredBody.sharedMaterials;
                PrefabUtility.RecordPrefabInstancePropertyModifications(body);
                foreach (
                    var part in authored
                        .GetComponentsInChildren<SkinnedMeshRenderer>()
                        .Where(s => s != authoredBody)
                )
                {
                    if (part.transform.parent != authoredBody.transform)
                        throw new InvalidOperationException("Unexpected authored face hierarchy.");
                    var copy = Object.Instantiate(part.gameObject, body.transform, false);
                    copy.name = part.name;
                    var skin = copy.GetComponent<SkinnedMeshRenderer>();
                    skin.bones = body.bones;
                    skin.rootBone = body.rootBone;
                }
                var face = original.AddComponent<CharacterFaceController>();
                var source = original.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0;
                var player = original.AddComponent<CharacterSpeechPlayer>();
                player.Configure(face, source, null);
                player.ConfigureExpressions(
                    AssetDatabase.LoadAssetAtPath<SpeechExpressionProfile>(
                        "Assets/Data/Characters/FriendlySpeechExpression.asset"
                    )
                );
                PrefabUtility.SaveAsPrefabAsset(wrapper, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(wrapper);
            }
        }

        public static void Connect(NpcIdentity staff)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
                return;
            Transform visual = staff.transform.Find("Barista");
            if (visual == null)
                throw new InvalidOperationException("Connect the existing barista first.");
            if (visual.GetComponentInChildren<CharacterSpeechPlayer>() == null)
            {
                Vector3 position = visual.localPosition,
                    scale = visual.localScale;
                Quaternion rotation = visual.localRotation;
                var replacement = (GameObject)
                    PrefabUtility.InstantiatePrefab(prefab, staff.transform);
                replacement.name = "Barista";
                replacement.transform.SetLocalPositionAndRotation(position, rotation);
                replacement.transform.localScale = scale;
                Object.DestroyImmediate(visual.gameObject);
                visual = replacement.transform;
            }
            var speaker = BuilderTools.GetOrAddComponent<DialogueSpeaker>(staff.gameObject);
            BuilderTools.SetReference(
                speaker,
                "player",
                visual.GetComponentInChildren<CharacterSpeechPlayer>()
            );
            BuilderTools.SetReference(
                staff.GetComponent<CharacterAnimationDriver>(),
                "animator",
                visual.GetComponentInChildren<Animator>()
            );
        }

        private static NpcIdentity Staff(Scene scene) =>
            scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<NpcIdentity>(true))
                .Single(npc => npc.Code == "CAFE_STAFF");

        public static void RenderReview()
        {
            string output = Environment.GetEnvironmentVariable("CAFE_DIALOGUE_REVIEW_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException("Set CAFE_DIALOGUE_REVIEW_OUTPUT.");
            System.IO.Directory.CreateDirectory(output);
            var scene = EditorSceneManager.OpenScene(SandboxPath);
            var speaker = Staff(scene).GetComponent<DialogueSpeaker>().Player;
            var link = BuilderTools.FindSingleInScene<DialogueServerLink>(scene);
            link.SetSpeaker(speaker);
            var voice = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Audio/Characters/CafeDialogue/CafeThanksSpeech.bytes"
            );
            link.PlayRecordedSpeech(voice.bytes);
            speaker.GetComponent<AudioSource>().mute = true;
            var animator = speaker.GetComponentInChildren<Animator>();
            animator.Rebind();
            animator.Update(.2f);
            Camera camera = BuilderTools
                .FindSingleInScene<PlayerCameraRig>(scene)
                .GetComponent<Camera>();
            FrameReviewFace(speaker, camera);
            var target = new RenderTexture(1280, 900, 24);
            var image = new Texture2D(1280, 900, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                int index = 0;
                foreach (float time in new[] { .14f, 1.33f, 2.8f })
                {
                    speaker.ApplyAtAudioTime(time);
                    CafeFemaleFaceBuilder.RenderBakedPose(
                        speaker.GetComponent<CharacterFaceController>(),
                        camera
                    );
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 1280, 900), 0, 0);
                    image.Apply();
                    System.IO.File.WriteAllBytes(
                        System.IO.Path.Combine(output, "CafeNpcSpeech" + index++ + ".png"),
                        image.EncodeToPNG()
                    );
                }
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
                link.StopSpeech();
            }
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
        }

        // Render a real worker WAV through the same player used by cafe dialogue.
        // No scene, prefab, source model or imported audio is saved by this command.
        public static void RenderSpeechVideo()
        {
            string output = Environment.GetEnvironmentVariable("CAFE_DIALOGUE_REVIEW_OUTPUT");
            string wavPath = Environment.GetEnvironmentVariable("CAFE_DIALOGUE_REVIEW_WAV");
            if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(wavPath))
                throw new InvalidOperationException(
                    "Set the review output directory and worker WAV path."
                );
            byte[] wav = System.IO.File.ReadAllBytes(wavPath);
            var decoded = DialogueSpeechWav.Read(wav);
            if (decoded.Timeline == null)
                throw new InvalidOperationException(
                    "Review voice has no verified phoneme timing: " + decoded.TimingError
                );
            System.IO.Directory.CreateDirectory(output);
            var scene = EditorSceneManager.OpenScene(SandboxPath);
            foreach (
                var controls in scene
                    .GetRootGameObjects()
                    .Where(go => go.name == "OfflineCafeSpeech")
            )
                controls.SetActive(false);
            var speaker = Staff(scene).GetComponent<DialogueSpeaker>().Player;
            var link = BuilderTools.FindSingleInScene<DialogueServerLink>(scene);
            link.SetSpeaker(speaker);
            link.PlayRecordedSpeech(wav);
            speaker.GetComponent<AudioSource>().mute = true;
            var animator = speaker.GetComponentInChildren<Animator>();
            animator.Rebind();
            animator.Update(.2f);
            var camera = BuilderTools
                .FindSingleInScene<PlayerCameraRig>(scene)
                .GetComponent<Camera>();
            FrameReviewFace(speaker, camera);
            const int width = 960,
                height = 676,
                fps = 30;
            int frames = Mathf.CeilToInt(decoded.Timeline.Duration * fps);
            var target = new RenderTexture(width, height, 24);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                for (int frame = 0; frame < frames; frame++)
                {
                    if (frame > 0)
                        animator.Update(1f / fps);
                    speaker.ApplyAtAudioTime(frame / (float)fps);
                    CafeFemaleFaceBuilder.RenderBakedPose(
                        speaker.GetComponent<CharacterFaceController>(),
                        camera
                    );
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    image.Apply();
                    System.IO.File.WriteAllBytes(
                        System.IO.Path.Combine(output, $"Frame{frame:D5}.png"),
                        image.EncodeToPNG()
                    );
                }
                Debug.Log(
                    $"Cafe speech review: {frames} frames at {fps} fps, verified audio {decoded.Timeline.Duration:F3}s."
                );
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
                link.StopSpeech();
            }
        }

        private static void FrameReviewFace(CharacterSpeechPlayer speaker, Camera camera)
        {
            var mouth = speaker
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .Single(s => s.name == "SpeakingLips");
            var baked = new Mesh();
            try
            {
                mouth.BakeMesh(baked);
                Vector3 lips = mouth.transform.TransformPoint(baked.bounds.center);
                var head = speaker
                    .GetComponentInChildren<Animator>()
                    .GetBoneTransform(HumanBodyBones.Head);
                Vector3 front = Vector3.ProjectOnPlane(lips - head.position, Vector3.up).normalized;
                if (front.sqrMagnitude < .5f)
                    throw new InvalidOperationException(
                        "Cannot determine the speaking face direction."
                    );
                Vector3 center = lips + Vector3.up * .25f;
                camera.transform.position = center + front * 2.8f;
                camera.transform.LookAt(center);
                camera.fieldOfView = 45;
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
        }

        private static void BuildOfflineReview()
        {
            var scene = EditorSceneManager.OpenScene(ProductionPath);
            var staff = Staff(scene);
            var player = staff.GetComponent<DialogueSpeaker>().Player;
            var link = BuilderTools.FindSingleInScene<DialogueServerLink>(scene);
            var settings = new SerializedObject(link);
            settings.FindProperty("useServer").boolValue = false;
            settings.ApplyModifiedPropertiesWithoutUndo();
            foreach (var controller in BuilderTools.FindAllInScene<DialogueController>(scene))
                controller.enabled = false;
            var rig = BuilderTools.FindSingleInScene<PlayerCameraRig>(scene);
            rig.enabled = false;
            BuilderTools.FindSingleInScene<PlayerMover>(scene).enabled = false;
            var body = player
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .Single(s => s.sharedMesh.name == "CafeFemaleFace");
            var animator = player.GetComponentInChildren<Animator>();
            animator.Rebind();
            animator.Update(.2f);
            FrameReviewFace(player, rig.GetComponent<Camera>());
            var controls = new GameObject("OfflineCafeSpeech", typeof(RectTransform));
            var preview = controls.AddComponent<CafeDialogueSpeechPreview>();
            BuilderTools.SetReference(preview, "link", link);
            BuilderTools.SetReference(preview, "speaker", player);
            BuilderTools.SetReference(
                preview,
                "coffeeChoice",
                AssetDatabase.LoadAssetAtPath<TextAsset>(
                    "Assets/Audio/Characters/CafeDialogue/CoffeeChoiceSpeech.bytes"
                )
            );
            BuilderTools.SetReference(
                preview,
                "cafeThanks",
                AssetDatabase.LoadAssetAtPath<TextAsset>(
                    "Assets/Audio/Characters/CafeDialogue/CafeThanksSpeech.bytes"
                )
            );
            UiBuilderTools.EnsureOverlayCanvas(controls, 100, true);
            var panel = UiBuilderTools.FindOrCreateBottomPanel(
                controls.transform,
                "Speech",
                920,
                18
            );
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                UiFontBuilder.RegularFontAssetPath
            );
            var choice = UiBuilderTools.FindOrCreateButton(
                panel,
                "Coffee",
                "Would you like coffee or tea?",
                font
            );
            UnityEventTools.AddPersistentListener(choice.onClick, preview.PlayCoffeeChoice);
            var thanks = UiBuilderTools.FindOrCreateButton(
                panel,
                "Thanks",
                "Thank you for visiting. Your coffee will be ready in five minutes.",
                font
            );
            UnityEventTools.AddPersistentListener(thanks.onClick, preview.PlayThanks);
            var stop = UiBuilderTools.FindOrCreateButton(
                panel,
                "Stop",
                "멈춤 · 서버 호출 없는 음성 검수",
                font
            );
            UnityEventTools.AddPersistentListener(stop.onClick, preview.Stop);
            EditorSceneManager.SaveScene(scene, SandboxPath);
        }
    }
}
