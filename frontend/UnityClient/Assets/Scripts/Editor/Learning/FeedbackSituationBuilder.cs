using A305.EditorTools.Common;
using A305.EditorTools.Theme;
using A305.EditorTools.UI;
using A305.Learning;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Learning
{
    /// <summary>
    /// 피드백 회화 표현 탭에 '이 상황에서 자주 써요' 표현 줄 묶음과 복사해 쓸 표현 줄 원본을 만든다.
    /// 표현 줄은 영어 표현·뜻, 쓰는 상황, [+ 담기]로 이루어진다. 없는 요소만 만들고 디자인은 유지한다.
    /// </summary>
    public static class FeedbackSituationBuilder
    {
        private const string MenuPath = "Tools/A305/Learning/Apply Feedback Situation Rows";
        private const string FeedbackPrefabPath = "Assets/Prefabs/Learning/FeedbackScreen.prefab";

        /// <summary>피드백 화면 프리팹에 표현 줄만 더하고 테마를 칠한다. 피드백 화면 전체 Builder를 다시 돌리지 않아도 된다.</summary>
        [MenuItem(MenuPath)]
        public static void Apply()
        {
            BuilderTools.UpdatePrefabIfExists(
                FeedbackPrefabPath,
                root => SetUp(root, root.GetComponentInChildren<FeedbackView>(true))
            );
            UiThemeBuilder.ApplyFeedbackSituationTheme();
            AssetDatabase.SaveAssets();
        }

        internal static void SetUp(GameObject root, FeedbackView view)
        {
            Transform card = root.transform.Find("Panel/PageCard/Content");
            if (card == null)
            {
                card = root.transform.Find("Panel/PageCard");
            }
            TMP_FontAsset bold = UiBuilderTools.BoldFont();
            TMP_FontAsset regular = UiBuilderTools.RegularFont();

            RectTransform rows = Child(card, "Situations", out bool isNew);
            if (isNew)
            {
                rows.anchorMin = new Vector2(0, 1);
                rows.anchorMax = Vector2.one;
                rows.pivot = new Vector2(0.5f, 1);
                rows.offsetMin = new Vector2(36, 0);
                rows.offsetMax = new Vector2(-36, 0);
                rows.anchoredPosition = new Vector2(0, -28);
                var column = rows.gameObject.AddComponent<VerticalLayoutGroup>();
                column.spacing = 10;
                column.childControlWidth = column.childControlHeight = true;
                column.childForceExpandWidth = true;
                column.childForceExpandHeight = false;
                rows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter
                    .FitMode
                    .PreferredSize;
                UiBuilderTools.FindOrCreateText(
                    rows,
                    "Header",
                    bold,
                    24,
                    Color.black,
                    "이 상황에서 자주 써요"
                );

                RectTransform template = Child(rows, "SituationRowTemplate", out _);
                template.gameObject.AddComponent<Image>().color = new Color(1f, 0.973f, 0.925f);
                var line = template.gameObject.AddComponent<HorizontalLayoutGroup>();
                line.padding = new RectOffset(22, 18, 12, 12);
                line.spacing = 16;
                line.childAlignment = TextAnchor.MiddleLeft;
                line.childControlWidth = line.childControlHeight = true;
                line.childForceExpandWidth = line.childForceExpandHeight = false;
                RectTransform texts = Child(template, "Texts", out _);
                var textColumn = texts.gameObject.AddComponent<VerticalLayoutGroup>();
                textColumn.spacing = 2;
                textColumn.childControlWidth = textColumn.childControlHeight = true;
                textColumn.childForceExpandWidth = true;
                textColumn.childForceExpandHeight = false;
                texts.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                TMP_Text phrase = UiBuilderTools.FindOrCreateText(
                    texts,
                    "Phrase",
                    regular,
                    24,
                    Color.black
                );
                phrase.textWrappingMode = TextWrappingModes.Normal;
                TMP_Text usage = UiBuilderTools.FindOrCreateText(
                    texts,
                    "Usage",
                    regular,
                    19,
                    Color.gray
                );
                usage.textWrappingMode = TextWrappingModes.Normal;
                Button save = UiBuilderTools.FindOrCreateButton(
                    template,
                    "SaveButton",
                    "담기",
                    bold,
                    120
                );
                var saveSize = BuilderTools.GetOrAddComponent<LayoutElement>(save.gameObject);
                saveSize.flexibleWidth = 0;
                template.gameObject.SetActive(false);
                rows.gameObject.SetActive(false);
            }

            BuilderTools.SetReference(view, "situationRows", rows);
            BuilderTools.SetReference(
                view,
                "situationRowTemplate",
                rows.Find("SituationRowTemplate").gameObject
            );
        }

        private static RectTransform Child(Transform parent, string name, out bool isNew)
        {
            Transform existing = parent.Find(name);
            isNew = existing == null;
            if (!isNew)
                return (RectTransform)existing;
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }
    }
}
