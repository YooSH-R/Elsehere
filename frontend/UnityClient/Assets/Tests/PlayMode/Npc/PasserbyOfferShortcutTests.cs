#if UNITY_EDITOR
using System.Collections;
using A305.Theme;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace A305.Npc.PlayModeTests
{
    public class PasserbyOfferShortcutTests
    {
        private GameObject instance;
        private PasserbyOfferView view;
        private Keyboard keyboard;
        private InputSettings.BackgroundBehavior previousBackground;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
        private int accepted;
        private int declined;

        [SetUp]
        public void SetUp()
        {
            previousBackground = InputSystem.settings.backgroundBehavior;
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings
                .EditorInputBehaviorInPlayMode
                .AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            instance = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Npc/PasserbyOffer.prefab")
            );
            view = instance.GetComponent<PasserbyOfferView>();
            accepted = declined = 0;
        }

        [TearDown]
        public void TearDown()
        {
            instance.SetActive(false);
            Object.Destroy(instance);
            InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
        }

        [UnityTest]
        public IEnumerator E_AcceptsTheVisibleOfferOnce()
        {
            Show(canDecline: true);
            yield return Press(Key.E);
            Assert.That(accepted, Is.EqualTo(1));
            Assert.That(declined, Is.Zero);
            Assert.That(view.IsShowing, Is.False);
        }

        [UnityTest]
        public IEnumerator Escape_DeclinesTheVisibleOptionalOfferOnce()
        {
            Show(canDecline: true);
            yield return Press(Key.Escape);
            Assert.That(declined, Is.EqualTo(1));
            Assert.That(accepted, Is.Zero);
            Assert.That(view.IsShowing, Is.False);
        }

        [UnityTest]
        public IEnumerator Escape_DoesNotDeclineAMandatoryOffer()
        {
            Show(canDecline: false);
            yield return Press(Key.Escape);
            Assert.That(declined, Is.Zero);
            Assert.That(view.IsShowing, Is.True);
        }

        [UnityTest]
        public IEnumerator DesktopOfferButtons_ShowTheirKeyboardShortcuts()
        {
            Show(canDecline: true);
            yield return null;
            Assert.That(
                Label("AcceptButton").GetParsedText(),
                Does.Contain("대화하기").And.Contain("E").And.Not.Contain("[E]")
            );
            Assert.That(AcceptKeyHint().activeInHierarchy, Is.True);
            Assert.That(
                Label("DeclineButton").GetParsedText(),
                Does.Contain("지금은 안 할래요").And.Contain("[Esc]")
            );
        }

        [Test]
        public void TouchOfferButtons_HideKeyboardShortcuts()
        {
            Touchscreen touchscreen = InputSystem.AddDevice<Touchscreen>();
            try
            {
                Show(canDecline: true);
                Assert.That(Label("AcceptButton").text, Is.EqualTo("대화하기"));
                Assert.That(AcceptKeyHint().activeSelf, Is.False);
                Assert.That(Label("DeclineButton").text, Is.EqualTo("지금은 안 할래요"));
            }
            finally
            {
                InputSystem.RemoveDevice(touchscreen);
            }
        }

        [UnityTest]
        public IEnumerator EventAcknowledgement_HidesKeyHint_AndNextOfferRestoresIt()
        {
            view.ShowEvent(new object(), "Mia", "잠시만 기다려 주세요.");
            yield return null;
            Assert.That(Label("AcceptButton").GetParsedText(), Is.EqualTo("알겠어요"));
            Assert.That(AcceptKeyHint().activeSelf, Is.False);
            Assert.That(
                instance
                    .transform.Find("Panel/Buttons/AcceptButton/Icon")
                    .GetComponent<Image>()
                    .enabled,
                Is.False
            );

            Show(canDecline: true);
            yield return null;
            Assert.That(AcceptKeyHint().activeInHierarchy, Is.True);
            Assert.That(
                instance
                    .transform.Find("Panel/Buttons/AcceptButton/Icon")
                    .GetComponent<Image>()
                    .enabled,
                Is.True
            );
        }

        private GameObject AcceptKeyHint()
        {
            Transform key = instance.transform.Find("Panel/Buttons/AcceptButton/KeyHint");
            Assert.That(key, Is.Not.Null, "대화하기 버튼에 테두리 있는 E 키 안내가 필요합니다.");
            return key.gameObject;
        }

        [UnityTest]
        public IEnumerator SeatingPrompt_HidesSpeechIcon_AndStillAcceptsE()
        {
            int interactions = 0;
            view.ShowTalkPrompt(
                new object(),
                "잠깐 앉기",
                () => interactions++,
                showTalkIcon: false
            );
            yield return null;
            Transform button = instance.transform.Find("TalkPrompt/Buttons/TalkButton");
            Image icon = button.Find("Icon").GetComponent<Image>();
            Assert.That(icon.enabled, Is.False);
            Assert.That(button.Find("KeyHint").gameObject.activeInHierarchy, Is.True);

            button.GetComponent<ThemedElement>().ApplyCurrent();
            yield return null;
            Assert.That(icon.enabled, Is.False, "테마를 갱신해도 말풍선이 다시 켜지면 안 됩니다.");
            yield return Press(Key.E);
            Assert.That(interactions, Is.EqualTo(1));

            view.ShowTalkPrompt(new object(), "말 걸기", () => { });
            yield return null;
            Assert.That(
                icon.enabled,
                Is.True,
                "다른 대화 안내에는 말풍선이 다시 표시되어야 합니다."
            );
        }

        [UnityTest]
        public IEnumerator StandingSignupAndBlockedPrompts_HideSpeechIcon()
        {
            Image icon = instance
                .transform.Find("TalkPrompt/Buttons/TalkButton/Icon")
                .GetComponent<Image>();
            object requester = new object();
            view.ShowMealWaitPrompt(requester, "음식 준비 중 · 5초", () => { });
            yield return null;
            Assert.That(icon.enabled, Is.False, "일어서기는 대화 동작이 아닙니다.");

            view.ShowSignupPrompt(
                requester,
                PasserbyOfferText.SignupLabel,
                "게스트 대화를 모두 썼어요"
            );
            yield return null;
            Assert.That(icon.enabled, Is.False, "가입 안내 열기는 대화 동작이 아닙니다.");

            view.ShowBlockedTalkPrompt(requester, "지금은 대화할 수 없어요");
            yield return null;
            Assert.That(icon.enabled, Is.False);
        }

        private void Show(bool canDecline)
        {
            object requester = new object();
            view.Show(
                requester,
                "Mia(카페 직원)의 대화 제안",
                "Hi there!",
                canDecline,
                null,
                () =>
                {
                    accepted++;
                    view.Hide(requester);
                },
                () =>
                {
                    declined++;
                    view.Hide(requester);
                }
            );
        }

        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            yield return null;
        }

        private TMP_Text Label(string name) =>
            instance.transform.Find("Panel/Buttons/" + name).GetComponentInChildren<TMP_Text>(true);
    }
}
#endif
