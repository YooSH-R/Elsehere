using A305.Api;
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
    /// 대화를 다 쓴 게스트가 [가입하고 계속 대화하기]를 누르면 뜨는 가입 안내 창(GuestSignup)을 대화 제안 화면 프리팹에 더한다.
    /// 없는 요소만 만들고, 이미 있는 요소의 디자인은 유지한다.
    /// </summary>
    public static class GuestSignupDialogBuilder
    {
        private const string MenuPath = "Tools/A305/Npc/Apply Guest Signup Dialog";
        private const string OfferPrefabPath = "Assets/Prefabs/Npc/PasserbyOffer.prefab";
        private const string ApiSettingsPath = "Assets/Data/Api/ApiSettings.asset";

        /// <summary>제안 화면 프리팹에 가입 안내 창만 더하고 테마를 칠한다. 행인 전체 Builder를 다시 돌리지 않아도 된다.</summary>
        [MenuItem(MenuPath)]
        public static void Apply()
        {
            BuilderTools.UpdatePrefabIfExists(
                OfferPrefabPath,
                root => SetUp(root, root.GetComponent<PasserbyOfferView>())
            );
            UiThemeBuilder.ApplyGuestSignupDialogTheme();
            AssetDatabase.SaveAssets();
        }

        internal static void SetUp(GameObject root, PasserbyOfferView view)
        {
            TMP_FontAsset bold = UiBuilderTools.BoldFont();
            TMP_FontAsset regular = UiBuilderTools.RegularFont();

            Transform existing = root.transform.Find("GuestSignup");
            RectTransform overlay;
            if (existing != null)
            {
                overlay = (RectTransform)existing;
            }
            else
            {
                overlay = new GameObject(
                    "GuestSignup",
                    typeof(RectTransform),
                    typeof(Image)
                ).GetComponent<RectTransform>();
                overlay.SetParent(root.transform, false);
                overlay.anchorMin = Vector2.zero;
                overlay.anchorMax = Vector2.one;
                overlay.offsetMin = Vector2.zero;
                overlay.offsetMax = Vector2.zero;
                // 창이 열려 있는 동안 뒤의 버튼이 눌리지 않게 클릭을 받는다.
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
                "게스트 대화를 모두 썼어요"
            );
            UiBuilderTools.FindOrCreateText(
                window,
                "Body",
                regular,
                22f,
                Color.white,
                "Google로 가입하면 대화를 계속할 수 있어요.\n"
                    + "지금까지 나눈 대화와 저장한 단어, NPC와의 사이는 가입한 계정에 그대로 이어져요."
            );
            RectTransform buttons = UiBuilderTools.FindOrCreateButtonRow(window, "Buttons");
            Button close = UiBuilderTools.FindOrCreateButton(
                buttons,
                "CloseButton",
                "닫기",
                bold,
                140f
            );
            Button signup = UiBuilderTools.FindOrCreateButton(
                buttons,
                "SignupButton",
                "Google로 가입하기",
                bold,
                240f
            );

            var dialogView = BuilderTools.GetOrAddComponent<GuestSignupDialogView>(root);
            BuilderTools.SetReference(
                dialogView,
                "apiSettings",
                BuilderTools.LoadRequiredAsset<ApiSettings>(ApiSettingsPath, MenuPath)
            );
            BuilderTools.SetReference(dialogView, "dialog", overlay.gameObject);
            BuilderTools.SetReference(dialogView, "signupButton", signup);
            BuilderTools.SetReference(dialogView, "closeButton", close);
            BuilderTools.RequireReferences(dialogView);
            BuilderTools.SetReference(view, "signupDialog", dialogView);
        }
    }
}
