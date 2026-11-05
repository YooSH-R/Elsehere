using System.Collections.Generic;
using A305.ObjectHover;
using A305.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace A305.Npc
{
    /// <summary>
    /// NPC 몸을 클릭·탭해 말을 건다(기능 명세 F-22). 8m 안에서 말을 걸 수 있는 NPC만 받고(<see cref="Passerby.CanTalkByClick"/>),
    /// PC에서는 커서를 올린 그 NPC에 테두리를 그린다. 휴대폰·태블릿은 커서가 없어 탭으로만 말을 건다.
    /// 사물 카드와 같은 클릭 판정(<see cref="PointerClick"/>)을 써서 시점을 돌린 끌기와 UI 위 클릭은 빼고,
    /// 맨 앞에 보이는 것이 NPC 몸일 때만 받는다(<see cref="NpcPointerRaycast"/>).
    /// 대화 제안 화면 프리팹에 붙어, NPC가 있는 장소마다 하나씩 놓인다.
    /// </summary>
    public sealed class NpcClickTalk : MonoBehaviour
    {
        /// <summary>클릭으로 말을 걸 수 있는 NPC까지의 수평 거리. 사물 카드를 띄우는 거리와 같다.</summary>
        public const float ReachMeters = 8f;

        // 3인칭 카메라는 플레이어 뒤 몇 m에 있으므로 클릭 거리보다 길게 쏜다.
        private const float RayLengthMeters = 30f;

        [SerializeField]
        [Tooltip("커서를 올린 NPC에 덧그릴 테두리 재질(A305/NpcHoverOutline)")]
        private Material outlineMaterial;

        private readonly NpcPointerRaycast raycast = new NpcPointerRaycast();
        private RendererOutline outline;

        // NPC 클릭을 판단하려고 누른 자리를 기억한다. UI 위에서 눌렀으면 비운다.
        private Vector2? pressedAt;

        // 포인터 아래 UI를 찾는 데 매 프레임 다시 쓴다.
        private PointerEventData uiPointer;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private readonly List<GameObject> uiHitObjects = new List<GameObject>();

        /// <summary>커서를 올려 테두리를 그린 NPC. 없으면 null</summary>
        public Passerby HighlightedNpc { get; private set; }

        /// <summary>
        /// 포인터 위치를 클릭·탭했다. 말을 걸 수 있는 NPC면 대화를 시작한다. Update에서 클릭을 판단해 호출하며,
        /// 실제 마우스 없이 확인할 때 직접 호출할 수 있다.
        /// </summary>
        public void Click(Vector2 pointerScreenPosition)
        {
            Passerby target = FindTalkableAt(pointerScreenPosition);
            if (target != null)
            {
                target.TalkByClick(ReachMeters);
            }
        }

        /// <summary>포인터 아래 말을 걸 수 있는 NPC에 테두리를 그린다. 매 프레임 Update에서 호출하며, 직접 호출해 확인할 수 있다.</summary>
        public void RefreshHighlight(Vector2 pointerScreenPosition)
        {
            Passerby target = PlayerTouchControls.ShouldShowControls()
                ? null
                : FindTalkableAt(pointerScreenPosition);
            if (target == HighlightedNpc)
            {
                return;
            }

            HighlightedNpc = target;
            if (target == null)
            {
                outline.Hide();
            }
            else
            {
                outline.Show(target.gameObject);
            }
        }

        private void Awake()
        {
            if (outlineMaterial == null)
            {
                Debug.LogError("NpcClickTalk의 outlineMaterial이 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            outline = new RendererOutline(outlineMaterial);
        }

        private void OnDisable()
        {
            pressedAt = null;
            HighlightedNpc = null;
            outline?.Hide();
        }

        private void Update()
        {
            Pointer device = Pointer.current;
            if (device == null)
            {
                return;
            }

            Vector2 pointer = device.position.ReadValue();
            DetectClick(device, pointer);
            RefreshHighlight(pointer);
        }

        // 누른 자리에서 거의 움직이지 않고 떼면 클릭으로 본다. 시점을 돌린 끌기와 UI를 누른 것은 빼고, 휴대폰 탭도 같게 처리한다.
        private void DetectClick(Pointer device, Vector2 pointer)
        {
            if (device.press.wasPressedThisFrame)
            {
                pressedAt = IsUiAt(pointer) ? null : pointer;
            }
            else if (device.press.wasReleasedThisFrame && pressedAt.HasValue)
            {
                bool isClick = PointerClick.IsClick(pressedAt.Value, pointer);
                pressedAt = null;
                if (isClick)
                {
                    Click(pointer);
                }
            }
        }

        private Passerby FindTalkableAt(Vector2 screenPosition)
        {
            Camera view = Camera.main;
            if (view == null || IsUiAt(screenPosition))
            {
                return null;
            }

            Passerby npc = raycast.Find(view.ScreenPointToRay(screenPosition), RayLengthMeters);
            return npc != null && npc.CanTalkByClick(ReachMeters) ? npc : null;
        }

        // 화면 좌표에 HUD 버튼·메뉴·창 같은 UI가 있어 NPC 클릭을 막는지. 사물 카드와 같은 기준(WorldClickBlocker)으로,
        // 화면을 덮는 터치 조작판은 끌기만 받으므로 막지 않는다. 실제 마우스 대신 좌표를 넣어 확인할 수 있도록 좌표로 UI를 찾는다.
        private bool IsUiAt(Vector2 screenPosition)
        {
            EventSystem events = EventSystem.current;
            if (events == null)
            {
                return false;
            }

            uiPointer ??= new PointerEventData(events);
            uiPointer.position = screenPosition;
            uiHits.Clear();
            events.RaycastAll(uiPointer, uiHits);
            uiHitObjects.Clear();
            foreach (RaycastResult hit in uiHits)
            {
                uiHitObjects.Add(hit.gameObject);
            }

            return WorldClickBlocker.IsBlockedBy(uiHitObjects);
        }
    }
}
