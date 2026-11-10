using System.Linq;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.Theme;
using A305.Learning;
using A305.Theme;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Learning
{
    /// <summary>기존 피드백의 창·글꼴·유리 카드를 그대로 쓰는 복습장.</summary>
    public static class ReviewWorkbookBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Learning/ReviewWorkbook.prefab";
        private const string HudPath = "Assets/Prefabs/Learning/LearningHud.prefab";

        [MenuItem("Tools/A305/Learning/Align Review Header Icon")]
        public static void AlignHeaderIcon()
        {
            BuilderTools.ThrowIfPlaying();
            BuilderTools.UpdateOrCreatePrefab(PrefabPath, "ReviewWorkbook", CenterBookIcon);
            AssetDatabase.SaveAssets();
        }

        private static void CenterBookIcon(GameObject root)
        {
            var book = (RectTransform)root.transform.Find("Panel/AppIcon/Book");
            if (book == null)
                throw new System.InvalidOperationException(
                    "복습장 머리줄의 책 아이콘이 필요합니다."
                );
            book.anchorMin = book.anchorMax = book.pivot = new Vector2(.5f, .5f);
            // 배경 PNG의 아래쪽 투명 여백을 제외한 실제 코랄 배지의 중심.
            book.anchoredPosition = new Vector2(0, 10);
            book.sizeDelta = new Vector2(60, 60);
            book.GetComponent<Image>().preserveAspect = true;
        }

        [MenuItem("Tools/A305/Learning/Apply Review Reference Progress")]
        public static void ApplyReferenceProgress()
        {
            BuilderTools.ThrowIfPlaying();
            BuilderTools.UpdateOrCreatePrefab(
                "Assets/Prefabs/Dialogue/DialoguePanel.prefab",
                "DialoguePanel",
                AttachReferenceProgress
            );
            AssetDatabase.SaveAssets();
        }

        private static void AttachReferenceProgress(GameObject root)
        {
            var fields = new SerializedObject(root.GetComponent<DialoguePanelView>());
            TMP_Text source = Read<TMP_Text>(fields, "suggestText");
            var badges = source.GetComponent<ReviewReferenceBadges>();
            if (badges == null)
                badges = source.gameObject.AddComponent<ReviewReferenceBadges>();
            badges.Configure(
                source,
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Art/UI/Onboarding/ConsentCheckOn.png"
                ),
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Art/UI/Onboarding/ConsentCheckOff.png"
                )
            );
            Set(fields, "reviewBadges", badges);
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Tools/A305/Learning/Build Review Workbook")]
        public static void Build()
        {
            BuilderTools.ThrowIfPlaying();
            GameObject original = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Learning/FeedbackScreen.prefab"
            );
            GameObject root = Object.Instantiate(original);
            root.name = "ReviewWorkbook";
            SetUp(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            BuilderTools.UpdateOrCreatePrefab(HudPath, "LearningHud", AttachToHud);
            BuilderTools.UpdateOrCreatePrefab(
                "Assets/Prefabs/Learning/FeedbackScreen.prefab",
                "FeedbackScreen",
                AttachSaveButton
            );
            BuilderTools.UpdateOrCreatePrefab(
                "Assets/Prefabs/Dialogue/DialoguePanel.prefab",
                "DialoguePanel",
                AttachReferenceFold
            );
            AssetDatabase.SaveAssets();
        }

        private static void SetUp(GameObject root)
        {
            var feedback = root.GetComponent<FeedbackView>();
            feedback.ConfigureScroll();
            var fields = new SerializedObject(feedback);
            GameObject panel = Read<GameObject>(fields, "panel");
            TMP_Text font = Read<TMP_Text>(fields, "bodyText");
            TMP_Text heading = Read<TMP_Text>(fields, "titleText");
            Button buttonTemplate = (Button)
                fields
                    .FindProperty("sectionButtons")
                    .GetArrayElementAtIndex(0)
                    .objectReferenceValue;
            Button close = Read<Button>(fields, "topCloseButton");
            Button[] tabs = Enumerable
                .Range(0, 4)
                .Select(i =>
                    (Button)
                        fields
                            .FindProperty("sectionButtons")
                            .GetArrayElementAtIndex(i)
                            .objectReferenceValue
                )
                .ToArray();
            Object.DestroyImmediate(feedback);
            foreach (Transform child in panel.transform.Cast<Transform>().ToArray())
            {
                bool keep = new[]
                {
                    "Title",
                    "Summary",
                    "AppIcon",
                    "TopCloseButton",
                    "Sections",
                    "PageCard",
                }.Contains(child.name);
                if (keep)
                    child.gameObject.SetActive(true);
                else
                    Object.DestroyImmediate(child.gameObject);
            }
            heading.text = "복습장";
            Image appIcon = panel.transform.Find("AppIcon").GetComponent<Image>();
            appIcon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/UI/Feedback/HistoryAvatarCoral.png"
            );
            var bookIcon = Rect("Book", appIcon.transform).gameObject.AddComponent<Image>();
            bookIcon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/UI/Theme/Icons/Book.png"
            );
            bookIcon.preserveAspect = true;
            bookIcon.rectTransform.anchorMin =
                bookIcon.rectTransform.anchorMax =
                bookIcon.rectTransform.pivot =
                    new Vector2(.5f, .5f);
            CenterBookIcon(root);
            bookIcon.rectTransform.sizeDelta = new Vector2(60, 60);
            heading.rectTransform.sizeDelta = new Vector2(220, heading.rectTransform.sizeDelta.y);
            var subtitle = panel.transform.Find("Summary").GetComponent<TMP_Text>();
            subtitle.text = "장소별로 다시 말해 봐요";
            subtitle.rectTransform.anchoredPosition = new Vector2(
                heading.rectTransform.anchoredPosition.x + 180,
                subtitle.rectTransform.anchoredPosition.y
            );
            subtitle.gameObject.SetActive(false); // 좁은 창에서도 제목과 필터가 겹치지 않는다.
            string[] labels = { "전체", "카페", "호텔", "마을" };
            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i].GetComponentInChildren<TMP_Text>(true).text = labels[i];
                var size = tabs[i].GetComponent<LayoutElement>();
                size.preferredWidth = 92;
                size.minWidth = 92;
            }
            ScrollRect scroll = panel.transform.Find("PageCard").GetComponent<ScrollRect>();
            foreach (Transform child in scroll.content)
                child.gameObject.SetActive(false);
            var row = Rect("ReviewRowTemplate", scroll.content)
                .gameObject.AddComponent<ReviewWorkbookRow>();
            Image background = row.gameObject.AddComponent<Image>();
            row.gameObject.AddComponent<Button>().targetGraphic = background;
            background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/UI/Feedback/FeedbackTile.png"
            );
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = 2.3f;
            var rowFields = new SerializedObject(row);
            Set(rowFields, "title", Text(row.transform, heading, "Title", 29));
            Set(rowFields, "situation", Text(row.transform, font, "Situation", 23));
            Set(rowFields, "expressions", Text(row.transform, heading, "Expressions", 26));
            var progress = Text(row.transform, font, "Progress", 18);
            progress.alignment = TextAlignmentOptions.MidlineRight;
            progress.color = new Color32(125, 110, 91, 255);
            Set(rowFields, "progress", progress);
            Set(
                rowFields,
                "practice",
                Action(row.transform, buttonTemplate, "PracticeButton", "연습하기", UiTone.Primary)
            );
            var remove = Object.Instantiate(close, row.transform);
            remove.name = "RemoveButton";
            remove.transform.Find("Icon").GetComponent<Image>().sprite =
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Theme/Icons/Delete.png");
            Set(rowFields, "remove", remove);
            rowFields.ApplyModifiedPropertiesWithoutUndo();
            row.gameObject.SetActive(false);
            var status = Text(scroll.content, font, "ReviewStatus", 24);
            var more = Action(
                scroll.content,
                buttonTemplate,
                "MoreButton",
                "더 보기",
                UiTone.Neutral
            );
            var view = root.AddComponent<ReviewWorkbookView>();
            var viewFields = new SerializedObject(view);
            Set(viewFields, "panel", panel);
            Set(viewFields, "scroll", scroll);
            Set(viewFields, "rowTemplate", row);
            Set(viewFields, "status", status);
            Set(viewFields, "more", more);
            Set(viewFields, "close", close);
            var filters = viewFields.FindProperty("filters");
            filters.arraySize = tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
                filters.GetArrayElementAtIndex(i).objectReferenceValue = tabs[i];
            viewFields.ApplyModifiedPropertiesWithoutUndo();
            foreach (Transform child in scroll.content.Cast<Transform>().ToArray())
                if (child != row.transform && child != status.transform && child != more.transform)
                    Object.DestroyImmediate(child.gameObject);
            panel.SetActive(false);
        }

        private static void AttachToHud(GameObject hud)
        {
            var old = hud.transform.Find("ReviewWorkbook");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            GameObject screen = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),
                    hud.transform
                );
            screen.name = "ReviewWorkbook";
            var screenRect = (RectTransform)screen.transform;
            screenRect.localScale = Vector3.one;
            screenRect.anchorMin = Vector2.zero;
            screenRect.anchorMax = Vector2.one;
            screenRect.pivot = new Vector2(.5f, .5f);
            screenRect.offsetMin = screenRect.offsetMax = Vector2.zero;
            // 원본은 독립 창으로도 검수한다. HUD 안에서는 바깥 캔버스의 배율을 따른다.
            Object.DestroyImmediate(screen.GetComponent<CanvasScaler>());
            Transform phone = hud.transform.Find("Panel/Menu");
            Button template = phone.Find("HistoryButton").GetComponent<Button>();
            Button entry = Action(phone, template, "ReviewButton", "복습장", UiTone.Primary);
            ReviewWorkbookRow.Pin(
                (RectTransform)entry.transform,
                98 * 1.2f,
                655 * 1.2f,
                234 * 1.2f,
                54 * 1.2f
            );
            var fields = new SerializedObject(hud.GetComponent<LearningHudView>());
            Set(fields, "reviewButton", entry);
            Set(fields, "reviewView", screen.GetComponent<ReviewWorkbookView>());
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AttachReferenceFold(GameObject dialogueRoot)
        {
            AttachReferenceProgress(dialogueRoot);
            var view = dialogueRoot.GetComponent<DialoguePanelView>();
            var fields = new SerializedObject(view);
            Transform card = Read<TMP_Text>(fields, "suggestText").transform.parent;
            Transform old = card.Find("ReviewFoldButton");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var buttonObject = new GameObject(
                "ReviewFoldButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement)
            );
            buttonObject.transform.SetParent(card, false);
            buttonObject.GetComponent<LayoutElement>().ignoreLayout = true;
            Image face = buttonObject.GetComponent<Image>();
            face.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/UI/Theme/CloseButtonFace.png"
            );
            face.type = Image.Type.Sliced;
            var rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-20, -20);
            rect.sizeDelta = new Vector2(42, 42);
            var arrowObject = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
            arrowObject.transform.SetParent(rect, false);
            Image arrow = arrowObject.GetComponent<Image>();
            arrow.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/UI/Theme/Icons/Next.png"
            );
            arrow.color = new Color32(47, 37, 27, 255);
            arrow.raycastTarget = false;
            arrow.rectTransform.sizeDelta = new Vector2(19, 19);
            arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
            Set(fields, "reviewFoldButton", buttonObject.GetComponent<Button>());
            fields.ApplyModifiedPropertiesWithoutUndo();
            buttonObject.SetActive(false);
        }

        private static void AttachSaveButton(GameObject feedbackRoot)
        {
            var view = feedbackRoot.GetComponent<FeedbackView>();
            var fields = new SerializedObject(view);
            var panel = Read<GameObject>(fields, "panel").transform;
            Transform old = panel.Find("ReviewSaveButton");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var button = Action(
                panel,
                Read<Button>(fields, "vocabularyButton"),
                "ReviewSaveButton",
                "복습장에 담기",
                UiTone.Neutral
            );
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(44, 34);
            rect.sizeDelta = new Vector2(240, 60);
            Set(fields, "reviewButton", button);
            fields.ApplyModifiedPropertiesWithoutUndo();
            button.gameObject.SetActive(false);
        }

        private static Button Action(
            Transform parent,
            Button template,
            string name,
            string label,
            UiTone tone
        )
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);
            Button button = Object.Instantiate(template, parent);
            button.name = name;
            button.gameObject.SetActive(true);
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = colors.highlightedColor = colors.selectedColor = Color.white;
            button.colors = colors;
            FeedbackScreenBuilder.Clear(button.transform, keepLabel: true);
            UiThemeBuilder.StyleReviewButton(parent, name, tone);
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text == null)
            {
                var labelObject = new GameObject("Label", typeof(RectTransform));
                labelObject.transform.SetParent(button.transform, false);
                text = labelObject.AddComponent<TextMeshProUGUI>();
            }
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Art/UI/Fonts/Jua-Regular SDF.asset"
            );
            button.GetComponent<ThemedElement>().ApplyCurrent();
            text.text = label;
            text.fontSize = 22;
            text.fontStyle = FontStyles.Normal;
            text.fontWeight = FontWeight.Regular;
            text.characterSpacing = text.wordSpacing = text.lineSpacing = text.paragraphSpacing = 0;
            text.enableAutoSizing = false;
            text.alignment = TextAlignmentOptions.Midline;
            text.margin = Vector4.zero;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(18, 2);
            text.rectTransform.offsetMax = new Vector2(-18, -2);
            // 입체 버튼 아래층까지 포함해 눈에 보이는 중앙에 맞춘다(1600px 캡처 기준 약 3px 아래).
            button
                .gameObject.AddComponent<ReviewButtonLabel>()
                .SetVerticalOffset(name == "PracticeButton" ? -4 : 0);
            return button;
        }

        private static TMP_Text Text(Transform parent, TMP_Text template, string name, float size)
        {
            var text = Object.Instantiate(template, parent);
            text.name = name;
            text.gameObject.SetActive(true);
            text.text = "";
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.margin = Vector4.zero;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static T Read<T>(SerializedObject fields, string name)
            where T : Object => (T)fields.FindProperty(name).objectReferenceValue;

        private static void Set(SerializedObject fields, string name, Object value) =>
            fields.FindProperty(name).objectReferenceValue = value;
    }
}
