using System;
using A305.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace A305.Ambience
{
    /// <summary>실제로 누른 UI에만 짧은 소리를 낸다. 프로그램에서 onClick을 호출한 경우는 제외한다.</summary>
    [DisallowMultipleComponent]
    public sealed class UiClickSound : MonoBehaviour, IPointerClickHandler, ISubmitHandler
    {
        public void OnPointerClick(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left)
                Play();
        }

        public void OnSubmit(BaseEventData data) => Play();

        /// <summary>누르는 조작인지. 슬라이더·스크롤바는 끌어서 쓰고, 입력칸은 글자를 쓰므로 클릭음을 내지 않는다.</summary>
        public static bool Clicks(Selectable control) =>
            !(control is Slider || control is Scrollbar || control is InputField)
            && !control.GetType().Name.Contains("InputField");

        private void Play()
        {
            var control = GetComponent<Selectable>();
            if (
                control == null
                || !Clicks(control)
                || !control.isActiveAndEnabled
                || !control.IsInteractable()
            )
                return;
            // 마이크 입력에 클릭음이 섞이지 않도록 녹음 조작은 건드리지 않는다.
            foreach (string part in new[] { "Record", "Speak", "Voice", "Mic" })
                if (name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)
                    return;
            UiSoundEvents.Play(
                name == "MenuButton" || name == "OpenMenuButton" ? UiSound.Menu : UiSound.Click
            );
        }
    }
}
