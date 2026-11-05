using System.Linq;
using A305.Characters;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace A305.EditorTools.Characters
{
    public static partial class NpcEmotionBuilder
    {
        public const string ScenePath = "Assets/Scenes/Sandbox/NpcEmotionSandbox.unity";

        [MenuItem("Tools/A305/Characters/Build NPC Emotion Sandbox")]
        public static void BuildReview()
        {
            var names = Names.Append("LittleMung_Cafe_Female").ToArray();
            var labels = new[]
            {
                "비즈니스맨",
                "중년 남성",
                "중년 여성",
                "흑인 청년 남성",
                "할머니",
                "호텔리어 남성",
                "카페 남성",
                "할아버지",
                "호텔리어 여성",
                "경찰 여성",
                "경찰 남성",
                "카페 여성",
            };
            var entries = new NpcEmotionRoster.Entry[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var review = PrepareReview(PrefabPath(names[i]));
                entries[i] = new NpcEmotionRoster.Entry
                {
                    label = labels[i],
                    center = review.center,
                    front = review.front,
                    cameraSize = review.camera.orthographicSize,
                };
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("표정 검수 카메라").AddComponent<Camera>();
            camera.gameObject.AddComponent<AudioListener>();
            camera.orthographic = true;
            camera.nearClipPlane = .01f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.15f, .18f, .20f);
            var light = new GameObject("표정 검수 조명").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1;
            light.transform.rotation = Quaternion.LookRotation(
                -entries[0].front + Vector3.down * .5f
            );
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.65f, .65f, .65f);
            for (int i = 0; i < names.Length; i++)
            {
                var root = (GameObject)
                    PrefabUtility.InstantiatePrefab(
                        AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(names[i]))
                    );
                root.name = names[i];
                // Face-only review has no moving controller or ground. These
                // overrides belong to this sandbox instance, never the prefab.
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>())
                    if (
                        component
                        is LittleMungGroundContact
                            or LittleMungStairLocomotion
                            or LittleMungArmClearance
                    )
                        Object.DestroyImmediate(component);
                foreach (var animator in root.GetComponentsInChildren<Animator>())
                    animator.enabled = false;
                foreach (var blink in root.GetComponentsInChildren<CharacterIdleBlink>())
                    blink.enabled = false;
                entries[i].root = root;
                entries[i].player = root.GetComponentInChildren<CharacterSpeechPlayer>();
            }
            var controls = new GameObject("표정 검수 조작");
            var panel = controls.AddComponent<EmotionPreviewPanel>();
            var roster = controls.AddComponent<NpcEmotionRoster>();
            var clips = new[] { "CuriousPlayfulLaugh", "FrownConcernCalm" }
                .Select(name =>
                    AssetDatabase.LoadAssetAtPath<CharacterSpeechClip>(
                        CafeEmotionBuilder.DataFolder + "/" + name + ".asset"
                    )
                )
                .ToArray();
            roster.Configure(
                entries,
                panel,
                AssetDatabase.LoadAssetAtPath<EmotionExpressionProfile>(ProfilePath),
                clips,
                camera
            );
            roster.Select(0);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }
    }
}
