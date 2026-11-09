using A305.EditorTools.Common;
using A305.EditorTools.Theme;
using A305.Npc;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Npc
{
    /// <summary>기존 대화 제안 버튼에 E·Esc 안내와 안내가 들어갈 폭을 적용한다. 다른 디자인은 유지한다.</summary>
    public static class OfferShortcutsBuilder
    {
        [MenuItem("Tools/A305/Npc/Apply Offer Shortcuts")]
        public static void Apply()
        {
            BuilderTools.ThrowIfPlaying();
            BuilderTools.UpdatePrefabIfExists("Assets/Prefabs/Npc/PasserbyOffer.prefab", SetUp);
            AssetDatabase.SaveAssets();
        }

        internal static void SetUp(GameObject root)
        {
            SetButton(
                root.transform.Find("Panel/Buttons/AcceptButton"),
                PasserbyOfferText.AcceptLabel(true),
                260
            );
            SetButton(
                root.transform.Find("Panel/Buttons/DeclineButton"),
                PasserbyOfferText.DeclineLabel(true),
                320
            );
            Transform accept = root.transform.Find("Panel/Buttons/AcceptButton");
            RectTransform key = UiThemeBuilder.StyleInteractionKeyHint(accept);
            var view = root.GetComponent<PasserbyOfferView>();
            BuilderTools.SetReference(
                view,
                "acceptLabel",
                accept.GetComponentInChildren<TMP_Text>(true)
            );
            BuilderTools.SetReference(view, "acceptKeyHint", key);
        }

        private static void SetButton(Transform button, string caption, float width)
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            label.richText = true;
            label.text = caption;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = 18;
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(button.gameObject);
            size.minWidth = Mathf.Max(size.minWidth, width);
            size.preferredWidth = Mathf.Max(size.preferredWidth, width);
        }
    }
}
