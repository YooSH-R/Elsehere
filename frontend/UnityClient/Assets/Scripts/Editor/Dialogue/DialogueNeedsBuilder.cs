using System.IO;
using A305.Dialogue;
using A305.EditorTools.Common;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Dialogue
{
    /// <summary>
    /// 대화 창 이름 줄의 NPC 욕구 게이지(<see cref="DialogueNeedsView"/>)를 만든다. 게이지 세 칸마다 위에 이름, 아래에 양 끝이 둥근 막대를 두고
    /// 바탕은 두지 않는다. 자리는 대화 창이 무례 경고 칸과 도구 버튼 사이 가운데로 정한다. 없는 요소만 만들고 연결을 맞춘다.
    /// </summary>
    public static class DialogueNeedsBuilder
    {
        private const string MenuPath = "Tools/A305/Dialogue/Apply Need Gauges";
        private const string PanelPrefabPath = "Assets/Prefabs/Dialogue/DialoguePanel.prefab";
        private const string RoundFolder = "Assets/Art/UI/Dialogue";
        private const string RoundPath = RoundFolder + "/NeedGaugeRound.png";

        private const int SlotCount = 3;
        private const int RoundSize = 64;
        private const int RoundBorder = 31;
        private const float BarHeight = 8;
        private const float BarBottom = 10;
        private const float LabelBottom = 19;
        private const float LabelHeight = 22;
        private const float LabelSize = 18;

        private static readonly Color TrackColor = new Color(.3f, .27f, .24f, .12f);

        /// <summary>대화 창 프리팹에 욕구 게이지만 더한다. 대화 창 전체 Builder를 다시 돌리지 않아도 된다.</summary>
        [MenuItem(MenuPath)]
        public static void Apply()
        {
            BuilderTools.UpdatePrefabIfExists(
                PanelPrefabPath,
                root => SetUp(root, root.GetComponent<DialoguePanelView>())
            );
            AssetDatabase.SaveAssets();
        }

        internal static void SetUp(GameObject root, DialoguePanelView view)
        {
            Sprite round = Round();
            Transform panel = root.transform.Find("Panel");
            TMP_Text standing = panel.Find("Standing").GetComponent<TMP_Text>();
            RectTransform gauges = Child(panel, "NeedGauges", out bool isNew);
            if (isNew)
            {
                gauges.anchorMin = gauges.anchorMax = gauges.pivot = Vector2.zero;
                gauges.sizeDelta = new Vector2(
                    SlotCount * DialogueNeedsView.SlotWidth
                        + (SlotCount - 1) * DialogueNeedsView.SlotGap,
                    48
                );
                gauges.gameObject.SetActive(false);
            }
            BuilderTools.GetOrAddComponent<LayoutElement>(gauges.gameObject).ignoreLayout = true;
            var needs = BuilderTools.GetOrAddComponent<DialogueNeedsView>(gauges.gameObject);

            var labels = new TMP_Text[SlotCount];
            var fills = new RectTransform[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                RectTransform slot = Child(gauges, "Need" + i, out bool isNewSlot);
                if (isNewSlot)
                {
                    slot.anchorMin = slot.anchorMax = slot.pivot = Vector2.zero;
                    slot.anchoredPosition = new Vector2(
                        i * (DialogueNeedsView.SlotWidth + DialogueNeedsView.SlotGap),
                        0
                    );
                    slot.sizeDelta = new Vector2(DialogueNeedsView.SlotWidth, 48);
                }
                labels[i] = Label(slot, standing.font);
                fills[i] = Bar(slot, round);
            }

            BuilderTools.SetReference(view, "needsView", needs);
            SetReferences(needs, "labels", labels);
            SetReferences(needs, "fills", fills);
        }

        private static TMP_Text Label(RectTransform slot, TMP_FontAsset font)
        {
            RectTransform rect = Child(slot, "Label", out bool isNew);
            TMP_Text label = BuilderTools.GetOrAddComponent<TextMeshProUGUI>(rect.gameObject);
            if (isNew)
            {
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                rect.anchoredPosition = new Vector2(1, LabelBottom);
                rect.sizeDelta = new Vector2(DialogueNeedsView.SlotWidth, LabelHeight);
                label.font = font;
                label.fontSize = LabelSize;
                label.alignment = TextAlignmentOptions.BottomLeft;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;
                label.text = "배고픔";
            }
            label.raycastTarget = false;
            return label;
        }

        // 양 끝이 둥근 바탕 막대와, 왼쪽 끝에서 값만큼 늘어나는 채움 막대
        private static RectTransform Bar(RectTransform slot, Sprite round)
        {
            RectTransform track = Child(slot, "Track", out bool isNew);
            if (isNew)
            {
                track.anchorMin = track.anchorMax = track.pivot = Vector2.zero;
                track.anchoredPosition = new Vector2(0, BarBottom);
                track.sizeDelta = new Vector2(DialogueNeedsView.SlotWidth, BarHeight);
                Rounded(track, round, TrackColor);
            }

            RectTransform fill = Child(track, "Fill", out bool isNewFill);
            if (isNewFill)
            {
                fill.anchorMin = fill.anchorMax = fill.pivot = Vector2.zero;
                fill.anchoredPosition = Vector2.zero;
                fill.sizeDelta = new Vector2(DialogueNeedsView.SlotWidth * .7f, BarHeight);
                Rounded(fill, round, Color.white);
            }
            return fill;
        }

        private static void Rounded(RectTransform rect, Sprite round, Color color)
        {
            var image = BuilderTools.GetOrAddComponent<Image>(rect.gameObject);
            image.sprite = round;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            // 원 그림의 가장자리(31px)를 막대 높이의 절반 반지름으로 줄인다. 캔버스 기준 100px = 1단위다.
            image.pixelsPerUnitMultiplier = RoundBorder / (BarHeight / 2f);
        }

        // 흰 원 그림. 9분할로 늘려 양 끝이 둥근 막대를 그린다. 없을 때만 만든다.
        private static Sprite Round()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(RoundPath);
            if (existing != null)
                return existing;
            BuilderTools.EnsureFolder(RoundFolder);
            var texture = new Texture2D(RoundSize, RoundSize, TextureFormat.RGBA32, false);
            var pixels = new Color32[RoundSize * RoundSize];
            float centre = (RoundSize - 1) / 2f;
            for (int y = 0; y < RoundSize; y++)
            {
                for (int x = 0; x < RoundSize; x++)
                {
                    float distance = Mathf.Sqrt(
                        (x - centre) * (x - centre) + (y - centre) * (y - centre)
                    );
                    byte alpha = (byte)(255 * Mathf.Clamp01(RoundSize / 2f - distance));
                    pixels[y * RoundSize + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(RoundPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(RoundPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(RoundPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(RoundBorder, RoundBorder, RoundBorder, RoundBorder);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(RoundPath);
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

        private static void SetReferences(Object owner, string field, Object[] values)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty list = serialized.FindProperty(field);
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
