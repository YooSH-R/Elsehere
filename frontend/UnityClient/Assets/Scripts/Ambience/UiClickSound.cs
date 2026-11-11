using System;
using A305.Player;
using A305.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace A305.Ambience
{
    /// <summary>실제로 누른 UI에만 짧은 소리를 낸다. 프로그램에서 onClick을 호출한 경우는 제외한다.</summary>
    [DisallowMultipleComponent]
    public sealed class UiClickSound
        : MonoBehaviour,
            IPointerUpHandler,
            IPointerClickHandler,
            ISubmitHandler
    {
        private int? releasedPointer;

        public void OnPointerUp(PointerEventData data)
        {
            releasedPointer =
                data.button == PointerEventData.InputButton.Left && CanClick()
                    ? data.pointerId
                    : null;
        }

        public void OnPointerClick(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left)
                return;
            bool canClick = releasedPointer == data.pointerId || CanClick();
            releasedPointer = null;
            if (canClick)
                Play();
        }

        public void OnSubmit(BaseEventData data)
        {
            // 먼저 실행된 제출 동작이 자기 창을 닫아도 제출음은 재생한다.
            if (CanClick(requireActive: false))
                Play();
        }

        /// <summary>누르는 조작인지. 슬라이더·스크롤바는 끌어서 쓰고, 입력칸은 글자를 쓰므로 클릭음을 내지 않는다.</summary>
        public static bool Clicks(Selectable control) =>
            !(control is Slider || control is Scrollbar || control is InputField)
            && !control.GetType().Name.Contains("InputField");

        private bool CanClick(bool requireActive = true)
        {
            var control = GetComponent<Selectable>();
            if (control == null)
                return TryGetComponent<TouchJumpButton>(out var jump) && jump.isActiveAndEnabled;
            if (
                !Clicks(control)
                || (requireActive && !control.isActiveAndEnabled)
                || !control.IsInteractable()
            )
                return false;
            // 마이크 입력에 클릭음이 섞이지 않도록 녹음 조작은 건드리지 않는다.
            foreach (string part in new[] { "Record", "Speak", "Voice", "Mic" })
                if (name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
            return true;
        }

        private void Play() =>
            UiSoundEvents.Play(
                name switch
                {
                    "MenuButton" or "OpenMenuButton" => UiSound.Menu,
                    "GoogleButton"
                    or "GuestButton"
                    or "SignupButton"
                    or "StartButton"
                    or "ConfirmButton" => UiSound.PrimaryClick,
                    _ => UiSound.Click,
                }
            );
    }
}
