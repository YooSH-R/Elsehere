#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

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
                Does.Contain("대화하기").And.Contain("[E]")
            );
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
                Assert.That(Label("DeclineButton").text, Is.EqualTo("지금은 안 할래요"));
            }
            finally
            {
                InputSystem.RemoveDevice(touchscreen);
            }
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
