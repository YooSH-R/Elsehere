using System;
using System.Collections.Generic;
using A305.Dialogue;
using A305.Player;
using TMPro;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// 플레이어 가까이 있는 NPC 머리 위에 이름표를 띄운다(<see cref="NpcNameTags"/>). 이름·역할은 대본에서, 직원인지는 생활 데이터에서, 지금 근무 중인지는 생활(NpcRoutine)에서 읽는다.
    /// NPC마다 3D 글자판을 두지 않고 화면 한 장에서 머리 위치에 맞춰 그리며, 숨겨 둔 본보기 이름표를 복제해 돌려 쓴다.
    /// 이름표의 아래 가운데가 머리 위 자리에 오도록 본보기의 기준점을 아래 가운데에 둔다.
    /// 이름표를 누르면 그 NPC를 누른 것으로 보도록(<see cref="NpcClickTalk"/>) 띄운 이름표마다 어느 NPC 것인지 기억한다.
    /// </summary>
    public sealed class NpcNameTagView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("NPC 이름과 역할 이름을 읽을 대본. 대화 화면과 같은 것을 쓴다.")]
        private DialogueScriptSource names;

        [SerializeField]
        [Tooltip("직원(STAFF)과 행인을 가를 생활 데이터(Data/Npc/NpcLife.json)")]
        private TextAsset lifeData;

        [SerializeField]
        [Tooltip("숨겨 둔 본보기 이름표. 자식 Name(이름)과 RoleChip/Label(직원 역할)을 쓴다.")]
        private RectTransform tagTemplate;

        [SerializeField, Min(0)]
        [Tooltip(
            "시선 위치(눈 높이)에서 이만큼 위에 이름표를 띄운다. 머리 꼭대기를 가리지 않을 만큼 둔다."
        )]
        private float headClearanceMeters = .7f;

        // 카메라에서 먼 이름표부터 놓는다. 화면 좌표의 z가 카메라에서의 거리다.
        private static readonly Comparison<ShownTag> FartherFirst = (one, other) =>
            other.ScreenPoint.z.CompareTo(one.ScreenPoint.z);

        private readonly Dictionary<string, NpcNameTag?> tagsByCode =
            new Dictionary<string, NpcNameTag?>();
        private readonly List<TagSlot> slots = new List<TagSlot>();
        private readonly List<ShownTag> shownTags = new List<ShownTag>();
        private NpcLifeData life;
        private PlayerMover player;
        private Canvas layerCanvas;

        private void Awake()
        {
            life = lifeData != null ? NpcLifeData.Parse(lifeData.text) : null;
            layerCanvas = GetComponentInParent<Canvas>();
        }

        private void LateUpdate()
        {
            CollectShownTags();
            // 복제한 이름표는 뒤에 만든 것일수록 위에 그려진다. 먼 NPC 이름표부터 놓아, 겹치면 가까운 NPC 이름표가
            // 위에 보이고 클릭도 받게 한다.
            shownTags.Sort(FartherFirst);
            for (int i = 0; i < shownTags.Count; i++)
            {
                ShownTag shown = shownTags[i];
                SlotAt(i).Show(shown.Npc, shown.Tag, ToLayer(shown.ScreenPoint));
            }

            for (int i = shownTags.Count; i < slots.Count; i++)
            {
                slots[i].Hide();
            }
        }

        /// <summary>화면에 띄운 이름표(또는 그 안의 글자·칩)가 가리키는 NPC. 띄운 이름표가 아니면 null</summary>
        public NpcIdentity NpcOf(GameObject tagPart)
        {
            foreach (TagSlot slot in slots)
            {
                if (slot.Npc != null && tagPart.transform.IsChildOf(slot.Rect))
                {
                    return slot.Npc;
                }
            }

            return null;
        }

        // 플레이어 가까이 있고 카메라 앞에 있는 NPC의 이름표와 머리 위 화면 좌표를 모은다.
        private void CollectShownTags()
        {
            shownTags.Clear();
            Camera viewer = Camera.main;
            if (player == null)
            {
                player = FindFirstObjectByType<PlayerMover>();
            }

            if (viewer == null || player == null)
            {
                return;
            }

            foreach (NpcIdentity npc in NpcIdentity.Active)
            {
                float meters = Vector3.Distance(npc.transform.position, player.transform.position);
                NpcNameTag? tag = NpcNameTags.IsShown(meters, player.IsMovementLocked)
                    ? TagOf(npc.Code)
                    : null;
                Vector3 screen = viewer.WorldToScreenPoint(HeadTopOf(npc));
                // 카메라 뒤에 있는 NPC는 화면에 거꾸로 찍히므로 띄우지 않는다.
                if (tag == null || screen.z <= 0f)
                {
                    continue;
                }

                shownTags.Add(
                    new ShownTag(npc, NpcNameTags.ForDuty(tag.Value, IsOffDuty(npc)), screen)
                );
            }
        }

        // 생활(NpcRoutine)이 없는 NPC는 맡은 근무가 없어 쉬는 중이 아니다.
        private static bool IsOffDuty(NpcIdentity npc)
        {
            return npc.TryGetComponent(out NpcRoutine routine) && routine.IsOffDuty;
        }

        // 화면 좌표를 이름표 층 안의 자리로 바꾼다. 화면을 덮는 캔버스든 카메라 캔버스든 같은 자리에 놓인다.
        private Vector2 ToLayer(Vector2 screenPoint)
        {
            Canvas root = layerCanvas.rootCanvas;
            Camera uiCamera =
                root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)transform,
                screenPoint,
                uiCamera,
                out Vector2 local
            );
            return local;
        }

        private Vector3 HeadTopOf(NpcIdentity npc)
        {
            Vector3 eyes =
                npc.LookTarget != null
                    ? npc.LookTarget.position
                    : npc.transform.position + Vector3.up * 1.6f;
            return eyes + Vector3.up * headClearanceMeters;
        }

        private NpcNameTag? TagOf(string npcCode)
        {
            if (!tagsByCode.TryGetValue(npcCode, out NpcNameTag? tag))
            {
                DialogueScript script = null;
                bool known = names != null && names.TryGetScript(npcCode, out script);
                tag = NpcNameTags.Describe(
                    known ? script.NpcName : null,
                    known ? script.RoleLabel : null,
                    life?.FindNpc(npcCode)?.Role
                );
                tagsByCode.Add(npcCode, tag);
            }

            return tag;
        }

        private TagSlot SlotAt(int index)
        {
            while (slots.Count <= index)
            {
                var copy = Instantiate(tagTemplate, tagTemplate.parent);
                copy.name = "Tag";
                slots.Add(new TagSlot(copy));
            }

            return slots[index];
        }

        /// <summary>이번 프레임에 띄울 이름표 하나</summary>
        private readonly struct ShownTag
        {
            public ShownTag(NpcIdentity npc, NpcNameTag tag, Vector3 screenPoint)
            {
                Npc = npc;
                Tag = tag;
                ScreenPoint = screenPoint;
            }

            public NpcIdentity Npc { get; }

            public NpcNameTag Tag { get; }

            /// <summary>머리 위 자리의 화면 좌표. z는 카메라에서의 거리</summary>
            public Vector3 ScreenPoint { get; }
        }

        /// <summary>복제한 이름표 하나. 글자가 바뀔 때만 다시 써서 매 프레임 글자 배치를 새로 하지 않는다.</summary>
        private sealed class TagSlot
        {
            private readonly TMP_Text nameLabel;
            private readonly GameObject roleChip;
            private readonly TMP_Text roleLabel;

            public TagSlot(RectTransform rect)
            {
                Rect = rect;
                nameLabel = rect.Find("Name").GetComponent<TMP_Text>();
                roleChip = rect.Find("RoleChip").gameObject;
                roleLabel = roleChip.transform.Find("Label").GetComponent<TMP_Text>();
            }

            public RectTransform Rect { get; }

            /// <summary>이 이름표를 띄운 NPC. 숨겼으면 null</summary>
            public NpcIdentity Npc { get; private set; }

            public void Show(NpcIdentity npc, NpcNameTag tag, Vector2 layerPoint)
            {
                Npc = npc;
                if (nameLabel.text != tag.Name)
                {
                    nameLabel.text = tag.Name;
                }

                bool isStaff = tag.StaffRole != null;
                if (roleChip.activeSelf != isStaff)
                {
                    roleChip.SetActive(isStaff);
                }

                if (isStaff && roleLabel.text != tag.StaffRole)
                {
                    roleLabel.text = tag.StaffRole;
                }

                Rect.localPosition = layerPoint;
                if (!Rect.gameObject.activeSelf)
                {
                    Rect.gameObject.SetActive(true);
                }
            }

            public void Hide()
            {
                Npc = null;
                if (Rect.gameObject.activeSelf)
                {
                    Rect.gameObject.SetActive(false);
                }
            }
        }
    }
}
