using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Player.Tests
{
    public class InteractionCursorTests
    {
        private GameObject root;

        [SetUp]
        public void CreateUi()
        {
            root = new GameObject("Panel", typeof(RectTransform), typeof(CanvasGroup));
        }

        [TearDown]
        public void DestroyUi() => Object.DestroyImmediate(root);

        [Test]
        public void InteractiveButtonUsesHandEvenWhenThePointerHitsItsLabel()
        {
            Button button = AddControl<Button>();
            var label = new GameObject("Label", typeof(RectTransform));
            label.transform.SetParent(button.transform);

            Assert.That(InteractionCursor.CanClickUi(label, Vector2.zero, null), Is.True);
        }

        [Test]
        public void EnabledToggleUsesHand()
        {
            Toggle toggle = AddControl<Toggle>();

            Assert.That(
                InteractionCursor.CanClickUi(toggle.gameObject, Vector2.zero, null),
                Is.True
            );
        }

        [Test]
        public void DisabledControlAndNonInteractivePanelUseDefaultCursor()
        {
            Button button = AddControl<Button>();
            button.interactable = false;
            Assert.That(
                InteractionCursor.CanClickUi(button.gameObject, Vector2.zero, null),
                Is.False
            );

            button.interactable = true;
            root.GetComponent<CanvasGroup>().interactable = false;
            Assert.That(
                InteractionCursor.CanClickUi(button.gameObject, Vector2.zero, null),
                Is.False
            );
            Assert.That(InteractionCursor.CanClickUi(root, Vector2.zero, null), Is.False);
        }

        [Test]
        public void TextInputAndDragControlsUseDefaultCursor()
        {
            InputField input = AddControl<InputField>();
            Slider slider = AddControl<Slider>();

            Assert.That(
                InteractionCursor.CanClickUi(input.gameObject, Vector2.zero, null),
                Is.False
            );
            Assert.That(
                InteractionCursor.CanClickUi(slider.gameObject, Vector2.zero, null),
                Is.False
            );
        }

        // 창 변·모서리 위에서는 크기 조절 모양, 버튼 위에서는 손 모양, 그 밖에는 기본 커서다.
        [Test]
        public void UiCursor_ShowsResizeOnWindowEdgesAndHandOnButtons()
        {
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(800, 600);
            WindowFrame.Attach(root.GetComponent<RectTransform>());
            Button button = AddControl<Button>();

            Assert.That(
                InteractionCursor.UiCursor(
                    root.transform.Find("ResizeTopRight").gameObject,
                    Vector2.zero,
                    null
                ),
                Is.EqualTo("nesw-resize")
            );
            Assert.That(
                InteractionCursor.UiCursor(button.gameObject, Vector2.zero, null),
                Is.EqualTo("pointer")
            );
            Assert.That(InteractionCursor.UiCursor(root, Vector2.zero, null), Is.EqualTo(""));
        }

        private T AddControl<T>()
            where T : Selectable
        {
            var control = new GameObject(typeof(T).Name, typeof(RectTransform));
            control.transform.SetParent(root.transform);
            return control.AddComponent<T>();
        }
    }
}
