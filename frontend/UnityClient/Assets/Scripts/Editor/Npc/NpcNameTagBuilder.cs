using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.Theme;
using A305.EditorTools.UI;
using A305.Npc;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Npc
{
    /// <summary>
    /// NPC 머리 위 이름표 층(NameTags)을 대화 제안 화면 프리팹에 더한다. 행인·직원이 있는 장면에는 이 프리팹이 있으므로
    /// 장면을 따로 고치지 않아도 이름표가 나온다. 단어 카드(90)·HUD(95)보다 아래에 그리고 클릭은 받지 않는다.
    /// 없는 요소만 만들고, 이미 있는 이름표의 디자인은 유지한다.
    /// </summary>
    public static class NpcNameTagBuilder
    {
        private const string MenuPath = "Tools/A305/Npc/Apply Name Tags";
        private const string OfferPrefabPath = "Assets/Prefabs/Npc/PasserbyOffer.prefab";
        private const string NamesPath = "Assets/Data/Dialogue/MockDialogueScripts.asset";
        private const string LifeDataPath = "Assets/Data/Npc/NpcLife.json";
        private const string LayerName = "NameTags";
        private const int SortingOrder = 80;

        // 이름표 칩의 직원 색. 흰 유리 칩 위에서 멀리서도 직원이 보이게 진한 파랑에 흰 굵은 글자를 쓴다.
        private static readonly Color StaffChipColor = new Color(0.13f, 0.47f, 0.78f);
        private static readonly Color StaffChipInk = Color.white;

        /// <summary>제안 화면 프리팹에 이름표 층만 더하고 테마를 칠한다. 행인 전체 Builder를 다시 돌리지 않아도 된다.</summary>
        [MenuItem(MenuPath)]
        public static void Apply()
        {
            BuilderTools.ThrowIfPlaying();
            BuilderTools.UpdatePrefabIfExists(OfferPrefabPath, SetUp);
            UiThemeBuilder.ApplyNpcNameTagTheme();
            AssetDatabase.SaveAssets();
        }

        internal static void SetUp(GameObject offerRoot)
        {
            RectTransform layer = FindOrCreateLayer(offerRoot.transform);
            var view = BuilderTools.GetOrAddComponent<NpcNameTagView>(layer.gameObject);
            RectTransform template = FindOrCreateTemplate(layer);

            BuilderTools.SetReference(
                view,
                "names",
                BuilderTools.LoadRequiredAsset<DialogueScriptSource>(NamesPath, MenuPath)
            );
            BuilderTools.SetReference(
                view,
                "lifeData",
                BuilderTools.LoadRequiredAsset<TextAsset>(LifeDataPath, MenuPath)
            );
            BuilderTools.SetReference(view, "tagTemplate", template);
            BuilderTools.RequireReferences(view);
        }

        private static RectTransform FindOrCreateLayer(Transform offerRoot)
        {
            var layer = (RectTransform)offerRoot.Find(LayerName);
            if (layer == null)
            {
                layer = new GameObject(
                    LayerName,
                    typeof(RectTransform)
                ).GetComponent<RectTransform>();
                layer.SetParent(offerRoot, false);
                layer.SetAsFirstSibling();
                layer.anchorMin = Vector2.zero;
                layer.anchorMax = Vector2.one;
                layer.offsetMin = Vector2.zero;
                layer.offsetMax = Vector2.zero;
            }

            // 제안 화면(110)과 같은 프리팹에 있지만 단어 카드·HUD 아래에 그리도록 따로 순서를 정한다.
            var canvas = BuilderTools.GetOrAddComponent<Canvas>(layer.gameObject);
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;
            return layer;
        }

        // 기준점을 아래 가운데에 둬 머리 위 자리에서 위로 자라게 한다. 글자 길이에 맞춰 폭이 정해진다.
        private static RectTransform FindOrCreateTemplate(RectTransform layer)
        {
            Transform existing = layer.Find("TagTemplate");
            if (existing != null)
            {
                return (RectTransform)existing;
            }

            var template = new GameObject(
                "TagTemplate",
                typeof(RectTransform),
                typeof(Image),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter)
            ).GetComponent<RectTransform>();
            template.SetParent(layer, false);
            template.anchorMin = template.anchorMax = Vector2.zero;
            template.pivot = new Vector2(.5f, 0f);
            template.GetComponent<Image>().raycastTarget = false;
            var column = template.GetComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(16, 16, 6, 8);
            column.spacing = 2f;
            column.childAlignment = TextAnchor.MiddleCenter;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = false;
            column.childForceExpandHeight = false;
            var fitter = template.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateRoleChip(template);
            TMP_Text name = UiBuilderTools.FindOrCreateText(
                template,
                "Name",
                UiBuilderTools.BoldFont(),
                24f,
                Color.black,
                "Name"
            );
            name.alignment = TextAlignmentOptions.Center;
            name.raycastTarget = false;
            template.gameObject.SetActive(false);
            return template;
        }

        // 직원만 켜는 역할 칩(예: 경찰관). 이름 위에 둔다.
        private static void CreateRoleChip(RectTransform template)
        {
            var chip = new GameObject(
                "RoleChip",
                typeof(RectTransform),
                typeof(Image),
                typeof(HorizontalLayoutGroup)
            ).GetComponent<RectTransform>();
            chip.SetParent(template, false);
            // 테마의 칩 그림은 유리 재질로 칠해야 보이는 바탕이라, 칩은 Unity 기본 둥근 사각형에 옅은 파랑을 깐다.
            var background = chip.GetComponent<Image>();
            background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(
                "UI/Skin/UISprite.psd"
            );
            background.type = Image.Type.Sliced;
            background.color = StaffChipColor;
            background.raycastTarget = false;
            var row = chip.GetComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(10, 10, 1, 2);
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            TMP_Text label = UiBuilderTools.FindOrCreateText(
                chip,
                "Label",
                UiBuilderTools.BoldFont(),
                18f,
                StaffChipInk,
                "직원"
            );
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
        }
    }
}
