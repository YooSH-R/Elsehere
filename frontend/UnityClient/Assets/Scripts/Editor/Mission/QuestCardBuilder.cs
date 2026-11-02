using A305.EditorTools.Common;
using A305.EditorTools.UI;
using A305.Mission;
using A305.Theme;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Mission
{
    /// <summary>
    /// 마을 HUD를 확정 시안대로 만든다. 왼쪽 위 퀘스트 카드에 단계 칩·장소·할 일·상황·진행 막대·목표 체크리스트를 두고,
    /// 카드를 누르면 여행 수첩이 열리게 한다(예전 지도 버튼은 지운다). 오른쪽 위 메뉴 버튼 왼쪽에는 게임 시계 칩과 코인 칩을 둔다.
    /// </summary>
    public static class QuestCardBuilder
    {
        private const string MenuPath = "Tools/A305/Mission/Apply Quest Card";
        private const string PrefabPath = "Assets/Prefabs/Mission/FirstDayMission.prefab";
        private const string CatalogPath = "Assets/Data/Theme/UiThemeCatalog.asset";
        private const float Margin = 24;
        private const float CardWidth = 520;

        // 상태 칩 자리. 오른쪽 위 메뉴 버튼(78×78)은 메뉴 창 여백 안쪽(오른쪽에서 52, 위에서 48)에 있어,
        // 칩은 버튼 왼쪽 12px에 두고 버튼과 세로 가운데를 맞춘다.
        private const float ChipHeight = 64;
        private static readonly Vector2 StatusPosition = new Vector2(
            -(52 + 78 + 12),
            -(48 + (78 - ChipHeight) / 2)
        );

        private static readonly Color CoinColor = new Color32(255, 194, 61, 255);
        private static readonly Color CoinInk = new Color32(122, 79, 0, 255);

        [MenuItem(MenuPath)]
        public static void ApplyToPrefab()
        {
            BuilderTools.UpdatePrefabIfExists(PrefabPath, Apply);
            AssetDatabase.SaveAssets();
        }

        public static void Apply(GameObject root)
        {
            var view = root.GetComponent<FirstDayMissionView>();
            Transform content = root.transform.Find("Content");
            Transform mapButton = content.Find("MapButton");
            if (mapButton != null)
                Object.DestroyImmediate(mapButton.gameObject);
            QuestCard(view, (RectTransform)content.Find("CurrentGoal"));
            StatusChips(view, content);
            // 수첩은 카드 자리에 펼친다. 펼친 동안 카드는 숨는다.
            var journal = (RectTransform)content.Find("Journal");
            journal.anchoredPosition = new Vector2(Margin, -Margin);
        }

        private static void QuestCard(FirstDayMissionView view, RectTransform card)
        {
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0, 1);
            card.anchoredPosition = new Vector2(Margin, -Margin);
            card.sizeDelta = new Vector2(CardWidth, card.sizeDelta.y);
            Theme(card, UiThemeRole.Panel);
            var column = card.GetComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(34, 34, 28, 30);
            column.spacing = 10;
            column.childAlignment = TextAnchor.UpperLeft;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            BuilderTools.GetOrAddComponent<ContentSizeFitter>(card.gameObject).verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            // 카드를 누르면 여행 수첩을 연다.
            var open = BuilderTools.GetOrAddComponent<Button>(card.gameObject);
            open.targetGraphic = card.GetComponent<Image>();
            BuilderTools.SetReference(view, "journalButton", open);

            RectTransform header = Row(card, "Header", 12);
            RectTransform chip = Child(header, "StepChip");
            var chipImage = BuilderTools.GetOrAddComponent<Image>(chip.gameObject);
            chipImage.raycastTarget = false;
            // 알약 그림의 둥근 끝이 36px 높이 안에 들어가도록 9칸 나누기 모서리를 줄여 그린다.
            chipImage.pixelsPerUnitMultiplier = 2.4f;
            Size(chip, 84, 36);
            TMP_Text chipLabel = Text(chip, "Label", 20, "1 / 7");
            Fill((RectTransform)chipLabel.transform);
            chipLabel.alignment = TextAlignmentOptions.Center;
            Theme(chip, UiThemeRole.Badge);
            TMP_Text place = Text(header, "Place", 20, "호텔 도착");
            place.textWrappingMode = TextWrappingModes.NoWrap;
            place.overflowMode = TextOverflowModes.Ellipsis;
            Theme(place.transform, UiThemeRole.Muted);
            BuilderTools.GetOrAddComponent<LayoutElement>(place.gameObject).flexibleWidth = 1;
            TMP_Text hint = Text(header, "Hint", 18, "수첩 ›");
            hint.alignment = TextAlignmentOptions.MidlineRight;
            Theme(hint.transform, UiThemeRole.Caption);

            var direction = card.Find("Direction").GetComponent<TMP_Text>();
            direction.fontSize = 28;
            direction.textWrappingMode = TextWrappingModes.Normal;
            Theme(direction.transform, UiThemeRole.Title);
            var situation = card.Find("Situation").GetComponent<TMP_Text>();
            situation.fontSize = 19;
            situation.textWrappingMode = TextWrappingModes.Normal;
            Theme(situation.transform, UiThemeRole.Muted);

            RectTransform progress = Row(card, "Progress", 6);
            var bar = progress.GetComponent<HorizontalLayoutGroup>();
            bar.childForceExpandWidth = true;
            BuilderTools.GetOrAddComponent<LayoutElement>(progress.gameObject).minHeight = 8;
            // 시안의 일곱 단계를 미리 보이게 칸을 둔다. 실제 칸 수는 화면이 단계 수에 맞춘다.
            for (int i = progress.childCount; i < 7; i++)
            {
                RectTransform mark = Child(progress, "Mark");
                var image = BuilderTools.GetOrAddComponent<Image>(mark.gameObject);
                image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(
                    "UI/Skin/UISprite.psd"
                );
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 2.5f;
                image.raycastTarget = false;
                image.color =
                    i == 0 ? FirstDayMissionView.CurrentMark : FirstDayMissionView.UpcomingMark;
                var size = BuilderTools.GetOrAddComponent<LayoutElement>(mark.gameObject);
                size.minHeight = size.preferredHeight = 8;
                size.flexibleWidth = 1;
            }

            TMP_Text checklist = Text(card, "Checklist", 21, "");
            checklist.lineSpacing = 18;
            checklist.textWrappingMode = TextWrappingModes.Normal;
            Theme(checklist.transform, UiThemeRole.Body);

            string[] order = { "Header", "Direction", "Situation", "Progress", "Checklist" };
            for (int i = 0; i < order.Length; i++)
                card.Find(order[i]).SetSiblingIndex(i);
            BuilderTools.SetReference(view, "stepChip", chipLabel);
            BuilderTools.SetReference(view, "place", place);
            BuilderTools.SetReference(view, "progress", progress);
            BuilderTools.SetReference(view, "checklist", checklist);
        }

        // 오른쪽 위 메뉴 버튼 왼쪽의 게임 시계 칩과 코인 칩
        private static void StatusChips(FirstDayMissionView view, Transform content)
        {
            RectTransform status = Row(content, "Status", 12);
            status.anchorMin = status.anchorMax = status.pivot = Vector2.one;
            status.anchoredPosition = StatusPosition;
            status.sizeDelta = new Vector2(0, ChipHeight);
            status.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleRight;
            BuilderTools.GetOrAddComponent<ContentSizeFitter>(status.gameObject).horizontalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            RectTransform clockChip = Chip(status, "Clock", new RectOffset(28, 28, 0, 0));
            TMP_Text clock = Text(clockChip, "Label", 24, "1일차 · 오전 8:00");
            clock.textWrappingMode = TextWrappingModes.NoWrap;
            Theme(clock.transform, UiThemeRole.Title);

            RectTransform coinChip = Chip(status, "Coins", new RectOffset(14, 26, 0, 0));
            RectTransform coin = Child(coinChip, "Coin");
            var coinImage = BuilderTools.GetOrAddComponent<Image>(coin.gameObject);
            coinImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            coinImage.color = CoinColor;
            coinImage.raycastTarget = false;
            Size(coin, 42, 42);
            TMP_Text mark = Text(coin, "Mark", 22, "C");
            Fill((RectTransform)mark.transform);
            mark.alignment = TextAlignmentOptions.Center;
            mark.font = DisplayFont();
            mark.color = CoinInk;
            TMP_Text amount = Text(coinChip, "Amount", 34, "0");
            amount.textWrappingMode = TextWrappingModes.NoWrap;
            Theme(amount.transform, UiThemeRole.Title);
            TMP_Text unit = Text(coinChip, "Unit", 18, "코인");
            Theme(unit.transform, UiThemeRole.Muted);
            BuilderTools.SetReference(view, "clock", clock);
            BuilderTools.SetReference(view, "coins", amount);
        }

        private static RectTransform Chip(Transform parent, string name, RectOffset padding)
        {
            RectTransform chip = Row(parent, name, 10);
            chip.GetComponent<HorizontalLayoutGroup>().padding = padding;
            var image = BuilderTools.GetOrAddComponent<Image>(chip.gameObject);
            image.raycastTarget = false;
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(chip.gameObject);
            size.minHeight = size.preferredHeight = ChipHeight;
            Theme(chip, UiThemeRole.Chip);
            return chip;
        }

        private static RectTransform Row(Transform parent, string name, float spacing)
        {
            RectTransform row = Child(parent, name);
            var layout = BuilderTools.GetOrAddComponent<HorizontalLayoutGroup>(row.gameObject);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            return row;
        }

        private static RectTransform Child(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null && name != "Mark")
                return (RectTransform)existing;
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static TMP_Text Text(Transform parent, string name, float size, string value)
        {
            TMP_Text text = UiBuilderTools.FindOrCreateText(
                parent,
                name,
                UiBuilderTools.RegularFont(),
                size,
                Color.black,
                value
            );
            text.fontSize = size;
            text.enableAutoSizing = false;
            return text;
        }

        private static void Size(RectTransform rect, float width, float height)
        {
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(rect.gameObject);
            size.minWidth = size.preferredWidth = width;
            size.minHeight = size.preferredHeight = height;
        }

        private static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static TMP_FontAsset DisplayFont() =>
            AssetDatabase.LoadAssetAtPath<UiThemeCatalog>(CatalogPath).Themes[0].DisplayFont;

        private static void Theme(Transform target, UiThemeRole role)
        {
            var element = BuilderTools.GetOrAddComponent<ThemedElement>(target.gameObject);
            var fields = new SerializedObject(element);
            fields.FindProperty("catalog").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<UiThemeCatalog>(CatalogPath);
            fields.FindProperty("role").enumValueIndex = (int)role;
            fields.ApplyModifiedPropertiesWithoutUndo();
            element.ApplyCurrent();
        }
    }
}
