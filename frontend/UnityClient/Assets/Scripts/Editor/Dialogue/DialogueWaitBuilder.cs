using System.IO;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.Theme;
using A305.EditorTools.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Dialogue
{
    /// <summary>
    /// 대화창의 로딩 두 가지를 만든다. NPC 첫 대사·대답을 기다리는 동안 대사 칸을 덮는 로딩(Wait)과,
    /// 대화를 끝내고 피드백을 기다리는 동안 화면 전체를 덮는 가운데 로딩 창(FeedbackWait)이다.
    /// 없는 요소만 만들고, 이미 있는 요소의 디자인은 유지한다.
    /// </summary>
    public static class DialogueWaitBuilder
    {
        private const string MenuPath = "Tools/A305/Dialogue/Apply Waiting Views";
        private const string PanelPrefabPath = "Assets/Prefabs/Dialogue/DialoguePanel.prefab";
        private const string RingFolder = "Assets/Art/UI/Dialogue";
        private const string RingPath = RingFolder + "/LoadingRing.png";

        // [말하기] 버튼과 같은 코랄
        private static readonly Color RingColor = new Color32(249, 130, 86, 255);

        /// <summary>대화창 프리팹에 로딩만 더하고 테마를 칠한다. 대화창 전체 Builder를 다시 돌리지 않아도 된다.</summary>
        [MenuItem(MenuPath)]
        public static void Apply()
        {
            BuilderTools.UpdatePrefabIfExists(
                PanelPrefabPath,
                root => SetUp(root, root.GetComponent<DialoguePanelView>())
            );
            UiThemeBuilder.ApplyDialogueWaitingTheme();
            AssetDatabase.SaveAssets();
        }

        internal static void SetUp(GameObject root, DialoguePanelView view)
        {
            Sprite ring = LoadingRing();
            TMP_FontAsset bold = UiBuilderTools.BoldFont();
            TMP_FontAsset regular = UiBuilderTools.RegularFont();

            Transform history = root.transform.Find("Panel/Conversation/History");
            RectTransform wait = Child(history, "Wait", out bool isNewWait);
            if (isNewWait)
            {
                // 카드 그림의 둥근 모서리 틈으로 대사가 비치지 않게 대사 칸보다 조금 크게 덮는다.
                Stretch(wait);
                wait.offsetMin = new Vector2(-16, -12);
                wait.offsetMax = new Vector2(16, 12);
                wait.gameObject.AddComponent<Image>().color = new Color(1f, 0.973f, 0.925f, 0.97f);
                BuilderTools.GetOrAddComponent<LayoutElement>(wait.gameObject).ignoreLayout = true;
                RectTransform row = Child(wait, "Row", out _);
                Stretch(row);
                var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.spacing = 18;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;
                Spinner(row, ring, 48);
                TMP_Text label = UiBuilderTools.FindOrCreateText(
                    row,
                    "Label",
                    bold,
                    28,
                    Color.black,
                    "대답을 기다리고 있어요"
                );
                label.textWrappingMode = TextWrappingModes.NoWrap;
                wait.gameObject.SetActive(false);
            }
            wait.SetAsLastSibling();

            RectTransform window = Child(root.transform, "FeedbackWait", out bool isNewWindow);
            if (isNewWindow)
            {
                Stretch(window);
                // 피드백이 올 때까지 뒤 화면을 누르지 못하게 막는다.
                window.gameObject.AddComponent<Image>().color = new Color(
                    0.1f,
                    0.08f,
                    0.07f,
                    0.55f
                );
                RectTransform card = Child(window, "Card", out _);
                card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
                card.sizeDelta = new Vector2(600, 310);
                card.anchoredPosition = Vector2.zero;
                card.gameObject.AddComponent<Image>().color = new Color(1f, 0.973f, 0.925f);
                var column = card.gameObject.AddComponent<VerticalLayoutGroup>();
                column.padding = new RectOffset(48, 48, 48, 40);
                column.spacing = 18;
                column.childAlignment = TextAnchor.UpperCenter;
                column.childControlWidth = column.childControlHeight = true;
                column.childForceExpandWidth = column.childForceExpandHeight = false;
                Spinner(card, ring, 84);
                TMP_Text title = UiBuilderTools.FindOrCreateText(
                    card,
                    "Title",
                    bold,
                    34,
                    Color.black,
                    "피드백을 만들고 있어요"
                );
                title.alignment = TextAlignmentOptions.Center;
                TMP_Text body = UiBuilderTools.FindOrCreateText(
                    card,
                    "Body",
                    regular,
                    22,
                    Color.gray,
                    "방금 나눈 대화를 살펴보고 있어요.\n창을 닫지 말고 잠시만 기다려 주세요."
                );
                body.alignment = TextAlignmentOptions.Center;
                window.gameObject.SetActive(false);
            }
            window.SetAsLastSibling();

            BuilderTools.SetReference(view, "waitPanel", wait.gameObject);
            BuilderTools.SetReference(
                view,
                "waitText",
                wait.Find("Row/Label").GetComponent<TMP_Text>()
            );
            BuilderTools.SetReference(view, "feedbackWait", window.gameObject);
        }

        private static void Spinner(Transform parent, Sprite ring, float size)
        {
            RectTransform spinner = Child(parent, "Spinner", out _);
            var image = BuilderTools.GetOrAddComponent<Image>(spinner.gameObject);
            image.sprite = ring;
            image.color = RingColor;
            image.raycastTarget = false;
            var element = BuilderTools.GetOrAddComponent<LayoutElement>(spinner.gameObject);
            element.preferredWidth = element.minWidth = size;
            element.preferredHeight = element.minHeight = size;
            BuilderTools.GetOrAddComponent<LoadingSpinner>(spinner.gameObject);
        }

        /// <summary>끝으로 갈수록 옅어지는 고리 그림. 그림 파일이 있으면 그대로 쓴다.</summary>
        private static Sprite LoadingRing()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(RingPath);
            if (existing != null)
                return existing;
            BuilderTools.EnsureFolder(RingFolder);
            const int Size = 128;
            const float Outer = 60f;
            const float Thickness = 13f;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color32[Size * Size];
            float centre = (Size - 1) / 2f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = x - centre;
                    float dy = y - centre;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = Mathf.Min(distance - (Outer - Thickness), Outer - distance);
                    float coverage = Mathf.Clamp01(edge + 0.5f);
                    // 위쪽(12시)에서 시계 방향으로 300도까지 칠하고, 끝으로 갈수록 옅게 한다.
                    float angle = Mathf.Repeat(Mathf.Atan2(dx, dy) * Mathf.Rad2Deg, 360f);
                    float fade = angle <= 300f ? 1f - angle / 300f * 0.85f : 0f;
                    byte alpha = (byte)(255 * coverage * fade);
                    pixels[y * Size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(RingPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(RingPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(RingPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(RingPath);
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

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
