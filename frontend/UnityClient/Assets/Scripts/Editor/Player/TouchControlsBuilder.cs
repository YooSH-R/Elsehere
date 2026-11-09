using A305.EditorTools.Common;
using A305.EditorTools.UI;
using A305.Player;
using TMPro;
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

                    // 끌기 영역보다 뒤에 두어 버튼을 누르면 시점 돌리기가 아니라 버튼이 받는다.
                    Button viewToggle = FindOrCreateViewToggle(root.transform);
                    viewToggle.transform.SetAsLastSibling();
                    BuilderTools.SetReference(controls, "viewToggle", viewToggle);
                    BuilderTools.SetReference(
                        controls,
                        "toFirstPersonLabel",
                        viewToggle.transform.Find("ToFirstPerson").gameObject
                    );
                    BuilderTools.SetReference(
                        controls,
                        "toThirdPersonLabel",
                        viewToggle.transform.Find("ToThirdPerson").gameObject
                    );

                    // 조이스틱을 끝까지 밀어 달리는 동안 손잡이 색을 바꾼다.
                    BuilderTools.SetReference(
                        controls,
                        "stickHandle",
                        stick.Find("Handle").GetComponent<Image>()
                    );
                    RectTransform jump = FindOrCreateJumpButton(root.transform);
                    jump.SetAsLastSibling();
                    BuilderTools.SetReference(
                        BuilderTools.GetOrAddComponent<TouchJumpButton>(jump.gameObject),
                        "controls",
                        controls
                    );
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

        /// <summary>
        /// 오른쪽 가장자리의 둥근 [1인칭]·[3인칭] 버튼. 오른손 엄지가 닿는 높이이면서, 휴대폰에서 오른쪽 가운데로 옮겨 오는 미니맵과 오른쪽 아래 조작법 카드 사이다.
        /// 조이스틱 손잡이와 같은 파란색 바탕에 흰 글자로, 하늘·벽 어느 쪽 위에서도 읽힌다.
        /// </summary>
        private static Button FindOrCreateViewToggle(Transform parent)
        {
            Transform existing = parent.Find("ViewToggle");
            RectTransform toggle =
                existing != null ? (RectTransform)existing : CreateRect("ViewToggle", parent);
            // 자리·크기는 다시 실행할 때도 이 값으로 맞춘다.
            toggle.anchorMin = toggle.anchorMax = new Vector2(1f, 0.375f);
            toggle.anchoredPosition = new Vector2(-72f, 0f);
            toggle.sizeDelta = new Vector2(80f, 80f);
            var background = BuilderTools.GetOrAddComponent<Image>(toggle.gameObject);
            background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            background.color = new Color(0.25f, 0.48f, 0.9f, 0.85f);
            var button = BuilderTools.GetOrAddComponent<Button>(toggle.gameObject);
            button.targetGraphic = background;

            TMP_FontAsset font = UiBuilderTools.BoldFont();
            ViewLabel(toggle, "ToFirstPerson", "1인칭", font).SetActive(true);
            ViewLabel(toggle, "ToThirdPerson", "3인칭", font).SetActive(false);
            return button;
        }

        /// <summary>
        /// 조이스틱 오른쪽 아래의 작은 둥근 [점프] 버튼. 조이스틱(가로 30~190)과 띄워 두어 이동하다 잘못 누르지 않게 하고,
        /// [1인칭] 버튼과 같은 파란 바탕에 흰 글자다. 누르는 순간 점프하도록 Button 대신 TouchJumpButton이 받는다.
        /// </summary>
        private static RectTransform FindOrCreateJumpButton(Transform parent)
        {
            Transform existing = parent.Find("JumpButton");
            RectTransform jump =
                existing != null ? (RectTransform)existing : CreateRect("JumpButton", parent);
            // 자리·크기는 다시 실행할 때도 이 값으로 맞춘다.
            jump.anchorMin = jump.anchorMax = Vector2.zero;
            jump.anchoredPosition = new Vector2(240f, 64f);
            jump.sizeDelta = new Vector2(64f, 64f);
            var background = BuilderTools.GetOrAddComponent<Image>(jump.gameObject);
            background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            background.color = new Color(0.25f, 0.48f, 0.9f, 0.85f);
            ViewLabel(jump, "Label", "점프", UiBuilderTools.BoldFont()).SetActive(true);
            return jump;
        }

        private static GameObject ViewLabel(
            RectTransform toggle,
            string name,
            string text,
            TMP_FontAsset font
        )
        {
            TMP_Text label = UiBuilderTools.FindOrCreateText(
                toggle,
                name,
                font,
                24f,
                Color.white,
                text
            );
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            var rect = (RectTransform)label.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return label.gameObject;
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
