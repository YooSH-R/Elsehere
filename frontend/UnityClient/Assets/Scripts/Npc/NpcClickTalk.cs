using System.Collections.Generic;
using A305.ObjectHover;
using A305.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace A305.Npc
{
    /// <summary>
    /// NPC 몸이나 머리 위 이름표를 클릭·탭해 말을 건다(기능 명세 F-22). 8m 안에서 대화 시작을 확인할 NPC만 받고(<see cref="Passerby.CanTalkByClick"/>),
    /// PC에서는 커서를 올린 그 NPC 몸에 테두리를 그린다. 휴대폰·태블릿은 커서가 없어 탭으로만 말을 건다.
    /// 사물 카드와 같은 클릭 판정(<see cref="PointerClicks"/>)을 써서 시점을 돌린 끌기와 UI 위 클릭은 빼고,
    /// 이름표(<see cref="NpcNameTagView"/>)를 눌렀으면 그 이름표의 NPC를, 아니면 맨 앞에 보이는 NPC 몸을 받는다(<see cref="NpcPointerRaycast"/>).
    /// 휴대폰은 손가락마다 따로 보아 조이스틱을 잡은 채 탭해도 되고, 누르고 떼는 사이 NPC가 비켜나면 누른 순간 가리킨 NPC에게 말을 건다.
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

        // NPC 클릭을 판단하려고 포인터마다 누른 자리를 기억한다. 이름표가 아닌 UI 위에서 누른 것은 기억하지 않는다.
        private PointerClicks clicks;
        private readonly List<PointerChange> pointerChanges = new List<PointerChange>();

        // 포인터마다 누른 순간 가리킨 NPC. 없으면 null
        private readonly Dictionary<int, Passerby> pressedNpcs = new Dictionary<int, Passerby>();

        private PointerClicks Clicks =>
            clicks ??= new PointerClicks(position => IsCoveredByOtherUi(position, out _));

        // 포인터 아래 UI를 찾는 데 매 프레임 다시 쓴다. 이름표가 아닌 UI만 따로 모아 클릭을 막는지 본다.
        private PointerEventData uiPointer;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private readonly List<GameObject> otherUiHits = new List<GameObject>();

        /// <summary>커서를 올려 테두리를 그린 NPC. 없으면 null</summary>
        public Passerby HighlightedNpc { get; private set; }

        /// <summary>
        /// 포인터 위치를 클릭·탭했다. 말을 걸 수 있는 NPC면 대화 확인 창을 연다. Update에서 클릭을 판단해 호출하며,
        /// 실제 마우스 없이 확인할 때 직접 호출할 수 있다.
        /// </summary>
        public void Click(Vector2 pointerScreenPosition)
        {
            Click(pointerScreenPosition, null);
        }

        /// <summary>
        /// 포인터(마우스·펜 또는 손가락 하나)를 눌렀다. 누른 자리의 NPC를 기억해 둔다.
        /// Update에서 장치 입력을 읽어 호출하며, 실제 마우스 없이 확인할 때 직접 호출할 수 있다.
        /// </summary>
        public void Press(Vector2 pointerScreenPosition, int pointerId = PointerChanges.CursorId)
        {
            if (Clicks.Press(pointerId, pointerScreenPosition))
            {
                pressedNpcs[pointerId] = FindPointedNpc(pointerScreenPosition);
            }
            else
            {
                pressedNpcs.Remove(pointerId);
            }
        }

        /// <summary>
        /// 포인터를 뗐다. 누른 자리에서 거의 움직이지 않았으면 클릭이다. 뗀 자리에 말을 걸 NPC가 없으면 누른 순간 가리킨 NPC에게 말을 건다.
        /// Update에서 장치 입력을 읽어 호출하며, 실제 마우스 없이 확인할 때 직접 호출할 수 있다.
        /// </summary>
        public void Release(Vector2 pointerScreenPosition, int pointerId = PointerChanges.CursorId)
        {
            pressedNpcs.Remove(pointerId, out Passerby pressedNpc);
            if (Clicks.Release(pointerId, pointerScreenPosition))
            {
                Click(pointerScreenPosition, pressedNpc);
            }
        }

        // 걸으면서 누르면 떼는 사이 플레이어·카메라·NPC가 움직여 뗀 자리에서 NPC가 비켜날 수 있어, 누른 순간 가리킨 NPC도 받는다.
        private void Click(Vector2 pointerScreenPosition, Passerby pressedNpc)
        {
            Passerby target = FindTalkableAt(pointerScreenPosition);
            if (target == null && pressedNpc != null && pressedNpc.CanTalkByClick(ReachMeters))
            {
                target = pressedNpc;
            }

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
            if (target != null)
                InteractionCursor.MarkWorldTarget();
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
            clicks?.Clear();
            pressedNpcs.Clear();
            HighlightedNpc = null;
            outline?.Hide();
        }

        private void Update()
        {
            PointerChanges.Read(pointerChanges);
            foreach (PointerChange change in pointerChanges)
            {
                if (change.IsPress)
                {
                    Press(change.Position, change.PointerId);
                }
                else
                {
                    Release(change.Position, change.PointerId);
                }
            }

            Pointer device = Pointer.current;
            if (device != null)
            {
                RefreshHighlight(device.position.ReadValue());
            }
        }

        private Passerby FindTalkableAt(Vector2 screenPosition)
        {
            Passerby npc = FindPointedNpc(screenPosition);
            return npc != null && npc.CanTalkByClick(ReachMeters) ? npc : null;
        }

        // 화면 좌표가 가리키는 NPC. 이름표를 눌렀으면 그 이름표의 NPC, 아니면 맨 앞에 보이는 NPC 몸이다.
        // 이름표가 아닌 UI가 덮었으면 그 UI를 누른 것이라 없다.
        private Passerby FindPointedNpc(Vector2 screenPosition)
        {
            if (IsCoveredByOtherUi(screenPosition, out GameObject nameTag))
            {
                return null;
            }

            if (nameTag != null)
            {
                NpcIdentity tagged = nameTag.GetComponentInParent<NpcNameTagView>().NpcOf(nameTag);
                return tagged != null ? tagged.GetComponent<Passerby>() : null;
            }

            Camera view = Camera.main;
            return view != null
                ? raycast.Find(view.ScreenPointToRay(screenPosition), RayLengthMeters)
                : null;
        }

        // 화면 좌표에 HUD 버튼·메뉴·창처럼 이름표가 아닌 UI가 있어 NPC 클릭을 막는지. 사물 카드와 같은 기준(WorldClickBlocker)으로,
        // 화면을 덮는 터치 조작판은 끌기만 받으므로 막지 않는다. 이름표는 다른 화면 UI보다 아래에 그려지므로(NpcNameTagBuilder)
        // 그런 UI가 있으면 이름표도 덮인 것이다. 막지 않으면 맨 위 이름표를 함께 돌려준다(가까운 NPC 이름표가 위에 그려진다).
        // 실제 마우스 대신 좌표를 넣어 확인할 수 있도록 좌표로 UI를 찾는다.
        private bool IsCoveredByOtherUi(Vector2 screenPosition, out GameObject topNameTag)
        {
            topNameTag = null;
            EventSystem events = EventSystem.current;
            if (events == null)
            {
                return false;
            }

            uiPointer ??= new PointerEventData(events);
            uiPointer.position = screenPosition;
            uiHits.Clear();
            events.RaycastAll(uiPointer, uiHits);
            otherUiHits.Clear();
            foreach (RaycastResult hit in uiHits)
            {
                if (hit.gameObject.GetComponentInParent<NpcNameTagView>() == null)
                {
                    otherUiHits.Add(hit.gameObject);
                }
                else if (topNameTag == null)
                {
                    topNameTag = hit.gameObject;
                }
            }

            return WorldClickBlocker.IsBlockedBy(otherUiHits);
        }
    }
}
