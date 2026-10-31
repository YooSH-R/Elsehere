using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace A305.Onboarding.Tests
{
    /// <summary>
    /// 가입 화면이 선택 동의 바로 아래에서, 음성 처리에 동의해야 [꾹 눌러 말하기]를 쓸 수 있고 나중에 어디서 바꾸는지 알려 주는지 확인한다.
    /// </summary>
    public class SignupVoiceConsentNoteTests
    {
        private const string EntryScreensPath = "Assets/Prefabs/Onboarding/EntryScreens.prefab";

        [Test]
        public void SignupPanel_ExplainsVoiceConsentRightBelowTheConsents()
        {
            Transform panel = AssetDatabase
                .LoadAssetAtPath<GameObject>(EntryScreensPath)
                .transform.Find("SignupPanel");
            Transform note = panel.Find("VoiceConsentNote");

            Assert.That(note, Is.Not.Null, "가입 화면에 음성 동의 안내가 없습니다.");
            Assert.That(
                note.GetSiblingIndex(),
                Is.EqualTo(panel.Find("Consents").GetSiblingIndex() + 1)
            );
            Assert.That(
                note.GetComponent<TMP_Text>().text,
                Does.Contain("[꾹 눌러 말하기]").And.Contain("내 정보")
            );
        }
    }
}
