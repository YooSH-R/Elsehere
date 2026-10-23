using A305.EditorTools.Common;
using A305.EditorTools.UI;
using A305.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Player
{
    public static class TouchControlsBuilder
    {
        public const string MenuPath = "Tools/A305/Player/Build Touch Controls";
        private const string PrefabPath = "Assets/Prefabs/Player/TouchControls.prefab";
        private const string SettingsPath = "Assets/Data/Player/PlayerTouchSettings.asset";
        private const string SandboxPath = "Assets/Scenes/Sandbox/TouchControlsSandbox.unity";

        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
            {
                return;
            }

            PrepareAssets();
            BuildSandbox();
            AssetDatabase.SaveAssets();
        }

        public static void PrepareAssets()
        {
            var settings = BuilderTools.LoadOrCreateAsset<PlayerTouchSettings>(SettingsPath);
            EditorUtility.SetDirty(settings);
            GameObject prefab = BuilderTools.UpdateOrCreatePrefab(
                PrefabPath,
                "TouchControls",
                root =>
                {
                    bool isNewCanvas = root.GetComponent<Canvas>() == null;
                    UiBuilderTools.EnsureOverlayCanvas(root, -10, true);
                    if (isNewCanvas)
                    {
                        root.GetComponent<CanvasScaler>().referenceResolution = new Vector2(
                            960f,
                            600f
                        );
                    }

                    var controls = BuilderTools.GetOrAddComponent<PlayerTouchControls>(root);
                    RectTransform stick = FindOrCreateStick(root.transform);
                    var drag = BuilderTools.GetOrAddComponent<TouchDragArea>(stick.gameObject);
                    BuilderTools.SetReference(
                        controls,
                        "surfaceCanvas",
                        root.GetComponent<Canvas>()
                    );
                    BuilderTools.SetReference(controls, "stick", stick);
                    BuilderTools.SetReference(controls, "settings", settings);
                    BuilderTools.SetReference(drag, "controls", controls);
                    BuilderTools.SetReference(drag, "handle", stick.Find("Handle"));

                    RectTransform look = FindOrCreateLookArea(root.transform);
                    var lookDrag = BuilderTools.GetOrAddComponent<TouchDragArea>(look.gameObject);
                    BuilderTools.SetReference(lookDrag, "controls", controls);
                    var serializedLook = new SerializedObject(lookDrag);
                    serializedLook.FindProperty("controlsLook").boolValue = true;
                    serializedLook.ApplyModifiedPropertiesWithoutUndo();
                }
            );

            BuilderTools.LoadRequiredAsset<GameObject>(
                PlayerSandboxBuilder.PlayerPrefabPath,
                PlayerSandboxBuilder.MenuPath
            );
            BuilderTools.UpdatePrefabIfExists(
                PlayerSandboxBuilder.PlayerPrefabPath,
                player =>
                {
                    if (player.transform.Find("TouchControls") == null)
                    {
                        BuilderTools.InstantiatePrefab(
                            prefab,
                            player.transform,
                            Vector3.zero,
                            Quaternion.identity
                        );
                    }
                }
            );
        }

        private static RectTransform FindOrCreateStick(Transform parent)
        {
            Transform existing = parent.Find("Stick");
            if (existing != null)
            {
                return (RectTransform)existing;
            }

            RectTransform stick = CreateRect("Stick", parent);
            stick.anchorMin = stick.anchorMax = Vector2.zero;
            stick.anchoredPosition = new Vector2(110f, 110f);
            stick.sizeDelta = new Vector2(160f, 160f);
            var background = stick.gameObject.AddComponent<Image>();
            background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            background.color = new Color(1f, 1f, 1f, 0.25f);

            RectTransform handle = CreateRect("Handle", stick);
            handle.sizeDelta = new Vector2(72f, 72f);
            var knob = handle.gameObject.AddComponent<Image>();
            knob.sprite = background.sprite;
            knob.color = new Color(0.25f, 0.48f, 0.9f, 0.85f);
            knob.raycastTarget = false;
            return stick;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var root = new GameObject(name, typeof(RectTransform));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static RectTransform FindOrCreateLookArea(Transform parent)
        {
            Transform existing = parent.Find("LookArea");
            if (existing != null)
            {
                return (RectTransform)existing;
            }

            RectTransform look = CreateRect("LookArea", parent);
            look.anchorMin = new Vector2(0.5f, 0f);
            look.anchorMax = Vector2.one;
            look.offsetMin = look.offsetMax = Vector2.zero;
            var surface = look.gameObject.AddComponent<Image>();
            surface.color = Color.clear;
            return look;
        }

        private static void BuildSandbox()
        {
            SceneGroups groups = BuilderTools.NewSceneWithGroups();
            Material ground = BuilderTools.GetOrCreateMaterial(
                "Assets/Art/Player",
                "SandboxGround",
                new Color(0.55f, 0.62f, 0.5f)
            );
            BuilderTools.CreateShape(
                PrimitiveType.Plane,
                "Ground",
                groups.Environment,
                Vector3.zero,
                new Vector3(4f, 1f, 4f),
                ground
            );
            GameObject player = BuilderTools.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(PlayerSandboxBuilder.PlayerPrefabPath),
                groups.Characters,
                Vector3.zero,
                Quaternion.identity
            );
            GameObject camera = BuilderTools.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(PlayerSandboxBuilder.CameraPrefabPath),
                groups.Systems,
                new Vector3(0f, 2.5f, -5f),
                Quaternion.Euler(20f, 0f, 0f)
            );
            PlayerSandboxBuilder.ConnectPlayerAndCamera(player, camera);
            BuilderTools.EnsureEventSystem(groups);
            BuilderTools.SaveScene(groups.Scene, SandboxPath);
        }
    }
}
