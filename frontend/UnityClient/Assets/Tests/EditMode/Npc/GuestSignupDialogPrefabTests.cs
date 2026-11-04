using A305.Api;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Npc.Tests
{
    /// <summary>대화를 다 쓴 게스트가 [가입하고 계속 대화하기]를 누르면 띄울 가입 안내 창이 대화 제안 화면 프리팹에 연결돼 있다.</summary>
    public sealed class GuestSignupDialogPrefabTests
    {
        private const string PrefabPath = "Assets/Prefabs/Npc/PasserbyOffer.prefab";

        [Test]
        public void OfferView_ConnectsHiddenGuestSignupDialog()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var offer = new SerializedObject(prefab.GetComponent<PasserbyOfferView>());
            var dialog = (GuestSignupDialogView)
                offer.FindProperty("signupDialog").objectReferenceValue;

            Assert.That(dialog, Is.Not.Null, "제안 화면에 가입 안내 창이 연결되지 않았습니다.");
            Transform window = prefab.transform.Find("GuestSignup");
            Assert.That(window, Is.Not.Null);
            Assert.That(
                window.gameObject.activeSelf,
                Is.False,
                "가입 안내 창은 닫힌 채로 시작해야 합니다."
            );
            Assert.That(window.Find("Dialog/Buttons/SignupButton"), Is.Not.Null);
        }

        // 설문은 창 안이 아니라 화면 위 설문 카드(HUD)로 따로 띄운다. 창이 열릴 때 요청만 한다.
        [Test]
        public void ShowingGuestSignupDialog_RequestsTheSurveyCard()
        {
            GameObject offer = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)
            );
            int requests = 0;
            void CountRequest() => requests++;
            SurveyPrompt.Requested += CountRequest;
            try
            {
                offer.GetComponent<GuestSignupDialogView>().Show();

                Assert.That(requests, Is.EqualTo(1));
            }
            finally
            {
                SurveyPrompt.Requested -= CountRequest;
                Object.DestroyImmediate(offer);
            }
        }

        [Test]
        public void GuestSignupDialog_HasNoSurveyLinkInside()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            Assert.That(prefab.transform.Find("GuestSignup/Dialog/SurveyButton"), Is.Null);
        }
    }
}
