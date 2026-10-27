using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace A305.Voice
{
    /// <summary>누르고 있는 동안만 동작하는 버튼(누르고 말하기). 누른 채 버튼 밖으로 나가도 뗀 것으로 본다.</summary>
    public class HoldButton
        : MonoBehaviour,
            IPointerDownHandler,
            IPointerUpHandler,
            IPointerExitHandler
    {
        private bool isHeld;

        public event Action Pressed;
        public event Action Released;

        public void OnPointerDown(PointerEventData eventData)
        {
            isHeld = true;
            Pressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Release();
        }

        private void Release()
        {
            if (!isHeld)
            {
                return;
            }

            isHeld = false;
            Released?.Invoke();
        }
    }
}
