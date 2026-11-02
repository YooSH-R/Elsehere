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
    /// 음성 처리에 동의하지 않은 채 [말하기]를 누르면 뜨는 동의 창(VoiceConsent)을 대화창 프리팹에 더한다.
    /// 없는 요소만 만들고, 이미 있는 요소의 디자인은 유지한다.
    /// </summary>
    public static class VoiceConsentDialogBuilder
    {
        private const string MenuPath = "Tools/A305/Dialogue/Apply Voice Consent Dialog";
        private const string PanelPrefabPath = "Assets/Prefabs/Dialogue/DialoguePanel.prefab";

        /// <summary>대화창 프리팹에 동의 창만 더하고 테마를 칠한다. 대화창 전체 Builder를 다시 돌리지 않아도 된다.</summary>
        [MenuItem(MenuPath)]
        public static void Apply()
        {
            BuilderTools.UpdatePrefabIfExists(
                PanelPrefabPath,
                root => SetUp(root, root.GetComponent<DialoguePanelView>())
            );
            UiThemeBuilder.ApplyVoiceConsentDialogTheme();
            AssetDatabase.SaveAssets();
        }

        internal static void SetUp(GameObject root, DialoguePanelView view)
        {
            TMP_FontAsset bold = UiBuilderTools.BoldFont();
            TMP_FontAsset regular = UiBuilderTools.RegularFont();

            Transform existing = root.transform.Find("VoiceConsent");
            RectTransform overlay;
            if (existing != null)
            {
                overlay = (RectTransform)existing;
            }
            else
            {
                overlay = new GameObject(
                    "VoiceConsent",
                    typeof(RectTransform),
                    typeof(Image)
                ).GetComponent<RectTransform>();
                overlay.SetParent(root.transform, false);
                overlay.anchorMin = Vector2.zero;
                overlay.anchorMax = Vector2.one;
                overlay.offsetMin = Vector2.zero;
                overlay.offsetMax = Vector2.zero;
                // 창이 열려 있는 동안 뒤의 대화창 버튼이 눌리지 않게 클릭을 받는다.
                overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
                overlay.gameObject.SetActive(false);
            }
            overlay.SetAsLastSibling();

            RectTransform window = UiBuilderTools.FindOrCreateAnchoredPanel(
                overlay,
                "Dialog",
                new Vector2(0.5f, 0.5f),
                600f,
                Vector2.zero
            );
            UiBuilderTools.FindOrCreateText(
                window,
                "Title",
                bold,
                30f,
                Color.white,
                "음성으로 말하려면 동의가 필요해요"
            );
            UiBuilderTools.FindOrCreateText(
                window,
                "Body",
                regular,
                22f,
                Color.white,
                "[말하기]로 녹음한 목소리를 글자로 바꾸려고 음성 인식 업체(미국 포함)로 보내요. "
                    + "녹음은 글자로 바꾸는 데만 쓰고, AI 개선 활용에 따로 동의하지 않았다면 저장하지 않아요.\n"
                    + "동의하지 않아도 글자로 대화할 수 있고, 내 정보 › 선택 동의에서 언제든 끌 수 있어요."
            );
            TMP_Text status = UiBuilderTools.FindOrCreateText(
                window,
                "Status",
                regular,
                20f,
                Color.gray
            );
            status.gameObject.SetActive(false);
            RectTransform buttons = UiBuilderTools.FindOrCreateButtonRow(window, "Buttons");
            Button document = UiBuilderTools.FindOrCreateButton(
                buttons,
                "DocumentButton",
                "문서 보기",
                bold,
                170f
            );
            Button close = UiBuilderTools.FindOrCreateButton(
                buttons,
                "CloseButton",
                "닫기",
                bold,
                140f
            );
            Button agree = UiBuilderTools.FindOrCreateButton(
                buttons,
                "AgreeButton",
                "동의하고 켜기",
                bold,
                210f
            );

            var dialogView = BuilderTools.GetOrAddComponent<VoiceConsentDialogView>(root);
            BuilderTools.SetReference(dialogView, "dialog", overlay.gameObject);
            BuilderTools.SetReference(dialogView, "statusText", status);
            BuilderTools.SetReference(dialogView, "documentButton", document);
            BuilderTools.SetReference(dialogView, "closeButton", close);
            BuilderTools.SetReference(dialogView, "agreeButton", agree);
            BuilderTools.RequireReferences(dialogView);
            BuilderTools.SetReference(view, "voiceConsentDialog", dialogView);
        }
    }
}
