using System.Linq;
using A305.Api;
using A305.EditorTools.Common;
using A305.EditorTools.UI;
using A305.ObjectHover;
using A305.Player;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.ObjectHover
{
    /// <summary>
    /// 사물 호버 툴팁의 설정·확인용 단어·툴팁 프리팹을 준비하고, 대체 공간 프리팹의 가구에 사물 키를 붙인다.
    /// 공간 씬에 붙이는 일은 <see cref="ConnectToScene"/>을 통해 공간 기능 연결 도구가 한다.
    /// 이미 있는 데이터 에셋은 그대로 두고, 툴팁 프리팹은 기능 구성만 맞춰 디자인을 유지한다.
    /// </summary>
    public static class ObjectHoverBuilder
    {
        public const string MenuPath = "Tools/A305/ObjectHover/Build";

        private const string SettingsPath = "Assets/Data/ObjectHover/ObjectHoverSettings.asset";
        private const string TooltipPrefabPath = "Assets/Prefabs/ObjectHover/WordTooltip.prefab";
        private const string ApiSettingsPath = "Assets/Data/Api/ApiSettings.asset";
        private const string HomeRoomPath = "Assets/Prefabs/Home/HomeRoom.prefab";
        private const string CafeInteriorPath = "Assets/Prefabs/Cafe/CafeInterior.prefab";

        // 대체 공간 프리팹의 사물 이름과 사물 키(ERD world_objects.unity_key)
        private static readonly (
            string PrefabPath,
            string ObjectName,
            string UnityKey
        )[] PlaceholderKeys =
        {
            (HomeRoomPath, "Tv", "tv"),
            (HomeRoomPath, "Fridge", "refrigerator"),
            (HomeRoomPath, "AirConditioner", "air_conditioner"),
            (HomeRoomPath, "Sofa", "sofa"),
            (HomeRoomPath, "Bookshelf", "bookshelf"),
            (HomeRoomPath, "Bed", "bed"),
            (CafeInteriorPath, "Counter", "counter"),
            (CafeInteriorPath, "SignMenu", "menu"),
        };

        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
            {
                return;
            }

            PrepareAssets();
            AddWordKeysToSpacePrefabs();
            AssetDatabase.SaveAssets();
            Debug.Log($"사물 호버 툴팁을 준비했습니다: {TooltipPrefabPath}");
        }

        /// <summary>설정·확인용 단어·툴팁 프리팹을 준비한다.</summary>
        public static void PrepareAssets()
        {
            BuilderTools.LoadOrCreateAsset<ObjectHoverSettings>(SettingsPath);
            ObjectWordRegistration.PrepareSource();
            BuilderTools.UpdateOrCreatePrefab(TooltipPrefabPath, "WordTooltip", SetUpTooltip);
        }

        /// <summary>
        /// 대체 공간 프리팹의 가구에 사물 키를 붙이고, 가리킬 수 있도록 콜라이더가 없으면 붙인다.
        /// 프리팹이나 가구가 없으면 건너뛴다. 디자인 모델로 바꾼 사물은 키를 직접 유지한다.
        /// </summary>
        public static void AddWordKeysToSpacePrefabs()
        {
            foreach (var prefab in PlaceholderKeys.GroupBy(entry => entry.PrefabPath))
            {
                BuilderTools.UpdatePrefabIfExists(
                    prefab.Key,
                    root =>
                    {
                        foreach (var (_, objectName, unityKey) in prefab)
                        {
                            AddWordKey(root, objectName, unityKey);
                        }
                    }
                );
            }
        }

        /// <summary>
        /// 씬에 사물 호버 처리와 툴팁이 없으면 붙이고 플레이어·카메라와 연결한다. 이미 있으면 연결만 맞춘다.
        /// </summary>
        public static void ConnectToScene(
            SceneGroups groups,
            PlayerMover player,
            PlayerCameraRig cameraRig
        )
        {
            var tooltipPrefab = BuilderTools.LoadRequiredAsset<GameObject>(
                TooltipPrefabPath,
                MenuPath
            );

            var tooltip = BuilderTools.FindSingleInScene<WordTooltipView>(groups.Scene);
            if (tooltip == null)
            {
                tooltip = BuilderTools
                    .InstantiatePrefab(tooltipPrefab, groups.UI, Vector3.zero, Quaternion.identity)
                    .GetComponent<WordTooltipView>();
            }

            var hover = BuilderTools.FindSingleInScene<ObjectHoverController>(groups.Scene);
            if (hover == null)
            {
                hover = BuilderTools
                    .CreateEmpty("ObjectHover", groups.Systems, Vector3.zero, Quaternion.identity)
                    .gameObject.AddComponent<ObjectHoverController>();
            }

            BuilderTools.SetReference(
                hover,
                "settings",
                BuilderTools.LoadRequiredAsset<ObjectHoverSettings>(SettingsPath, MenuPath)
            );
            BuilderTools.SetReference(
                hover,
                "wordSource",
                BuilderTools.LoadRequiredAsset<CatalogObjectWordSource>(
                    ObjectWordRegistration.SourceAssetPath,
                    MenuPath
                )
            );
            BuilderTools.SetReference(hover, "viewCamera", cameraRig.GetComponent<Camera>());
            BuilderTools.SetReference(hover, "player", player);
            BuilderTools.SetReference(hover, "tooltip", tooltip);
            BuilderTools.SetReference(
                hover,
                "pointAction",
                BuilderTools.ActionReference("UI", "Point")
            );
            BuilderTools.RequireReferences(hover);
        }

        private static void AddWordKey(GameObject root, string objectName, string unityKey)
        {
            Transform target = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == objectName);
            if (target == null || target.TryGetComponent(out HoverableObject _))
            {
                return;
            }

            BuilderTools.SetText(
                target.gameObject.AddComponent<HoverableObject>(),
                "unityKey",
                unityKey
            );

            // 간판처럼 콜라이더가 없는 모양은 가리킬 수 있도록 모양에 맞춘 상자 콜라이더를 붙인다.
            if (target.GetComponentInChildren<Collider>() == null)
            {
                MeshFilter shape = target.GetComponentInChildren<MeshFilter>();
                if (shape != null)
                {
                    shape.gameObject.AddComponent<BoxCollider>();
                }
            }
        }

        /// <summary>
        /// 툴팁 프리팹의 기능 구성을 맞춘다. 화면 오버레이 캔버스 아래 패널에 단어·뜻·예문을 세로로 쌓는다.
        /// 없는 요소만 만들고, 이미 있는 색·크기·글꼴 같은 디자인은 유지한다.
        /// </summary>
        private static void SetUpTooltip(GameObject root)
        {
            // 고정한 카드의 버튼을 누를 수 있게 클릭을 받는다. 호버 중에는 패널이 클릭을 막지 않는다(WordTooltipView).
            UiBuilderTools.EnsureOverlayCanvas(root, sortingOrder: 100, receivesClicks: true);

            var view = BuilderTools.GetOrAddComponent<WordTooltipView>(root);

            Transform panel = root.transform.Find("Panel");
            if (panel == null)
            {
                panel = CreatePanel(root.transform);
            }

            TMP_FontAsset bold = UiBuilderTools.BoldFont();
            TMP_FontAsset regular = UiBuilderTools.RegularFont();

            BuilderTools.SetReference(view, "canvasRect", root.GetComponent<RectTransform>());
            BuilderTools.SetReference(view, "panel", panel);
            BuilderTools.SetReference(view, "panelGroup", panel.GetComponent<CanvasGroup>());
            BuilderTools.SetReference(
                view,
                "wordLabel",
                UiBuilderTools.FindOrCreateText(panel, "Word", bold, 34f, Color.white)
            );
            BuilderTools.SetReference(
                view,
                "meaningLabel",
                UiBuilderTools.FindOrCreateText(
                    panel,
                    "Meaning",
                    regular,
                    28f,
                    new Color(1f, 0.88f, 0.55f)
                )
            );
            BuilderTools.SetReference(
                view,
                "exampleLabel",
                UiBuilderTools.FindOrCreateText(
                    panel,
                    "Example",
                    regular,
                    22f,
                    new Color(0.8f, 0.82f, 0.86f)
                )
            );
            BuilderTools.SetReference(
                view,
                "pinHintLabel",
                UiBuilderTools.FindOrCreateText(
                    panel,
                    "PinHint",
                    regular,
                    18f,
                    new Color(0.62f, 0.65f, 0.72f),
                    "클릭하면 카드가 고정돼요"
                )
            );
            // 고정한 카드의 안내와 버튼 줄. 패널 너비(440)에 맞춰 단어 버튼을 두 줄로 나눈다.
            TMP_Text status = UiBuilderTools.FindOrCreateText(
                panel,
                "Status",
                regular,
                18f,
                new Color(1f, 0.88f, 0.55f)
            );
            BuilderTools.SetReference(view, "statusLabel", status);

            RectTransform wordActions = UiBuilderTools.FindOrCreateButtonRow(panel, "WordActions");
            BuilderTools.SetReference(view, "wordActionRow", wordActions.gameObject);
            Button save = FindOrCreateCardButton(wordActions, "SaveButton", "단어장에 저장", 170f);
            BuilderTools.SetReference(view, "saveButton", save);
            BuilderTools.SetReference(
                view,
                "saveLabel",
                save.GetComponentInChildren<TMP_Text>(true)
            );
            BuilderTools.SetReference(
                view,
                "pronunciationButton",
                FindOrCreateCardButton(wordActions, "PronunciationButton", "발음 듣기", 130f)
            );

            RectTransform actions = UiBuilderTools.FindOrCreateButtonRow(panel, "Actions");
            BuilderTools.SetReference(view, "actionRow", actions.gameObject);
            Button hide = FindOrCreateCardButton(
                actions,
                "HideButton",
                "알아요 · 다시 안 띄우기",
                250f
            );
            BuilderTools.SetReference(view, "hideButton", hide);
            BuilderTools.SetReference(
                view,
                "hideLabel",
                hide.GetComponentInChildren<TMP_Text>(true)
            );
            Button close = UiBuilderTools.FindOrCreateButton(
                actions,
                "CloseButton",
                "닫기",
                UiBuilderTools.BoldFont(),
                preferredWidth: 120f
            );
            BuilderTools.SetReference(view, "closeButton", close);
            // [닫기]는 버튼 줄 오른쪽 끝, 안내와 단어 버튼은 카드 내용 아래에 둔다.
            close.transform.SetAsLastSibling();
            status.transform.SetAsLastSibling();
            wordActions.SetAsLastSibling();
            actions.SetAsLastSibling();
            status.gameObject.SetActive(false);
            wordActions.gameObject.SetActive(false);
            actions.gameObject.SetActive(false);

            // 서버 주소와 발음 재생. 카드 프리팹에 두어 장소 씬마다 배치하지 않는다.
            var serverLink = BuilderTools.GetOrAddComponent<ObjectWordServerLink>(root);
            BuilderTools.SetReference(
                serverLink,
                "apiSettings",
                BuilderTools.LoadRequiredAsset<ApiSettings>(
                    ApiSettingsPath,
                    A305.EditorTools.Onboarding.OnboardingBuilder.MenuPath
                )
            );
            var pronunciationSource = BuilderTools.GetOrAddComponent<AudioSource>(root);
            pronunciationSource.playOnAwake = false;
            BuilderTools.SetReference(serverLink, "pronunciationSource", pronunciationSource);
            // 고정한 카드의 빈 곳을 눌러도 뒤의 사물이 클릭되지 않게 배경이 클릭을 받는다.
            panel.GetComponent<Image>().raycastTarget = true;
            BuilderTools.RequireReferences(view);
        }

        // 카드 글자 크기(18~34)에 맞춰 새로 만드는 버튼의 글자를 기본 버튼보다 작게 한다. 이미 있는 버튼의 디자인은 유지한다.
        private static Button FindOrCreateCardButton(
            Transform row,
            string buttonName,
            string label,
            float preferredWidth
        )
        {
            bool isNew = row.Find(buttonName) == null;
            Button button = UiBuilderTools.FindOrCreateButton(
                row,
                buttonName,
                label,
                UiBuilderTools.BoldFont(),
                preferredWidth
            );
            if (isNew)
            {
                button.GetComponentInChildren<TMP_Text>(true).fontSize = 20f;
            }

            return button;
        }

        private static Transform CreatePanel(Transform root)
        {
            var panel = new GameObject("Panel", typeof(RectTransform));
            var rect = panel.GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(440f, 0f);

            var background = panel.AddComponent<Image>();
            background.color = new Color(0.08f, 0.09f, 0.12f, 0.88f);

            var group = panel.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 14, 14);
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // 너비는 고정하고, 글이 길면 줄을 바꿔 높이를 늘린다.
            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rect;
        }
    }
}
