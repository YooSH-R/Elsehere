using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Onboarding.Tests
{
    /// <summary>게스트로 다시 접속했을 때 게임 전에 [가입]·[게스트로 계속하기]를 고르는 화면이 입구 프리팹에 연결돼 있다.</summary>
    public sealed class GuestReturnPanelPrefabTests
    {
        private const string PrefabPath = "Assets/Prefabs/Onboarding/EntryScreens.prefab";

        private GameObject prefab;

        [SetUp]
        public void Load()
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        [Test]
        public void EntryScreens_ConnectsGuestReturnPanelWithSignupAndContinueButtons()
        {
            var screen = new SerializedObject(prefab.GetComponent<OnboardingScreen>());
            var panel = (GuestReturnPanelView)
                screen.FindProperty("guestReturnPanel").objectReferenceValue;

            Assert.That(panel, Is.Not.Null, "입구 화면에 게임 전 멈춤 화면이 연결되지 않았습니다.");
            Assert.That(panel.transform.Find("SignupButton"), Is.Not.Null);
            Assert.That(panel.transform.Find("ContinueButton"), Is.Not.Null);
        }

        [Test]
        public void SignupPanel_ConnectsSubtitleForGuestGuidance()
        {
            var signup = new SerializedObject(
                prefab.transform.Find("SignupPanel").GetComponent<SignupPanelView>()
            );

            Assert.That(
                signup.FindProperty("subtitleText").objectReferenceValue,
                Is.Not.Null,
                "게스트 가입 안내로 바꿀 제목 아래 문구가 연결되지 않았습니다."
            );
        }
    }
}
