using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace A305.Player
{
    /// <summary>게임 브라우저의 마우스가 실제로 클릭할 수 있는 UI나 월드 대상 위에 있을 때 손 모양을 표시한다.</summary>
    public sealed class InteractionCursor : MonoBehaviour
    {
        private static int worldTargetFrame = -1;

        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private EventSystem eventSystem;
        private PointerEventData pointer;
        private bool? showingHand;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartForBrowser()
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer)
                return;

            var cursor = new GameObject(nameof(InteractionCursor));
            DontDestroyOnLoad(cursor);
            cursor.AddComponent<InteractionCursor>();
        }

        // 클릭을 판정하는 모듈이 현재 프레임에만 표시를 요청한다. 대상이 사라지거나 씬을 옮기면 자동으로 만료된다.
        public static void MarkWorldTarget() => worldTargetFrame = Time.frameCount;

        public static bool CanClickUi(GameObject hit, Vector2 position, Camera camera)
        {
            GameObject target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);
            if (target == null)
                return false;

            if (target.TryGetComponent(out Selectable control))
                return control.IsInteractable() && (control is Button or Toggle or Dropdown);

            return target.TryGetComponent(out IClickPointerTarget custom)
                && custom.CanClickAt(position, camera);
        }

        private void LateUpdate()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || BrowserPointer.IsPrimaryCoarse)
            {
                ShowHand(false);
                return;
            }

            Vector2 position = mouse.position.ReadValue();
            bool inGame = new Rect(0, 0, Screen.width, Screen.height).Contains(position);
            ShowHand(inGame && (worldTargetFrame == Time.frameCount || HasUiTarget(position)));
        }

        private void OnDisable() => ShowHand(false);

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
                ShowHand(false);
        }

        private bool HasUiTarget(Vector2 position)
        {
            EventSystem current = EventSystem.current;
            if (current == null)
                return false;
            if (eventSystem != current)
            {
                eventSystem = current;
                pointer = new PointerEventData(current);
            }

            pointer.position = position;
            current.RaycastAll(pointer, uiHits);
            foreach (RaycastResult hit in uiHits)
            {
                if (
                    hit.module is GraphicRaycaster
                    && hit.gameObject.GetComponentInParent<TouchDragArea>() == null
                )
                    return CanClickUi(hit.gameObject, position, hit.module.eventCamera);
            }

            return false;
        }

        private void ShowHand(bool clickable)
        {
            if (showingHand == clickable)
                return;

            showingHand = clickable;
            BrowserPointer.SetClickCursor(clickable);
        }
    }

    /// <summary>일반 버튼 대신 글자 일부나 약관 줄을 눌러 쓰는 UI의 클릭 영역.</summary>
    public interface IClickPointerTarget
    {
        bool CanClickAt(Vector2 position, Camera camera);
    }
}
