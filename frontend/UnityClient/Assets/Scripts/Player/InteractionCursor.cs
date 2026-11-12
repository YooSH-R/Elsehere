using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace A305.Player
{
    /// <summary>
    /// 게임 브라우저의 마우스가 실제로 클릭할 수 있는 UI나 월드 대상 위에 있을 때 손 모양을, 창 변·모서리 위에 있거나
    /// 창 크기를 바꾸는 동안에는 크기 조절 모양을 표시한다.
    /// </summary>
    public sealed class InteractionCursor : MonoBehaviour
    {
        private static int worldTargetFrame = -1;

        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private EventSystem eventSystem;
        private PointerEventData pointer;
        private string showingCursor;

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

        /// <summary>UI 위 커서. 창 변·모서리는 크기 조절 모양, 누를 수 있는 UI는 손 모양, 그 밖은 기본(빈 문자열)이다.</summary>
        public static string UiCursor(GameObject hit, Vector2 position, Camera camera)
        {
            if (hit.TryGetComponent(out WindowResizeHandle handle))
                return handle.CursorName;
            return CanClickUi(hit, position, camera) ? Hand : "";
        }

        private const string Hand = "pointer";

        private void LateUpdate()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || BrowserPointer.IsPrimaryCoarse)
            {
                ShowCursor("");
                return;
            }

            // 창 크기를 바꾸는 동안에는 포인터가 변을 벗어나도 크기 조절 모양을 유지한다.
            if (WindowResizeHandle.Dragging != null)
            {
                ShowCursor(WindowResizeHandle.Dragging.CursorName);
                return;
            }

            Vector2 position = mouse.position.ReadValue();
            if (!new Rect(0, 0, Screen.width, Screen.height).Contains(position))
            {
                ShowCursor("");
                return;
            }

            string ui = UiCursorAt(position);
            ShowCursor(
                ui != "" ? ui
                : worldTargetFrame == Time.frameCount ? Hand
                : ""
            );
        }

        private void OnDisable() => ShowCursor("");

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
                ShowCursor("");
        }

        private string UiCursorAt(Vector2 position)
        {
            EventSystem current = EventSystem.current;
            if (current == null)
                return "";
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
                    return UiCursor(hit.gameObject, position, hit.module.eventCamera);
            }

            return "";
        }

        private void ShowCursor(string cursor)
        {
            if (showingCursor == cursor)
                return;

            showingCursor = cursor;
            BrowserPointer.SetCursor(cursor);
        }
    }

    /// <summary>일반 버튼 대신 글자 일부나 약관 줄을 눌러 쓰는 UI의 클릭 영역.</summary>
    public interface IClickPointerTarget
    {
        bool CanClickAt(Vector2 position, Camera camera);
    }
}
