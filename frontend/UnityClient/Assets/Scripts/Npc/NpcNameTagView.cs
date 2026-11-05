using System.Collections.Generic;
using A305.Dialogue;
using A305.Player;
using TMPro;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// 플레이어 가까이 있는 NPC 머리 위에 이름표를 띄운다(<see cref="NpcNameTags"/>). 이름·역할은 대본에서, 직원인지는 생활 데이터에서 읽는다.
    /// NPC마다 3D 글자판을 두지 않고 화면 한 장에서 머리 위치에 맞춰 그리며, 숨겨 둔 본보기 이름표를 복제해 돌려 쓴다.
    /// 이름표의 아래 가운데가 머리 위 자리에 오도록 본보기의 기준점을 아래 가운데에 둔다.
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

        private readonly Dictionary<string, NpcNameTag?> tagsByCode =
            new Dictionary<string, NpcNameTag?>();
        private readonly List<TagSlot> slots = new List<TagSlot>();
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
            int shown = 0;
            Camera viewer = Camera.main;
            if (player == null)
            {
                player = FindFirstObjectByType<PlayerMover>();
            }

            if (viewer != null && player != null)
            {
                foreach (NpcIdentity npc in NpcIdentity.Active)
                {
                    float meters = Vector3.Distance(
                        npc.transform.position,
                        player.transform.position
                    );
                    NpcNameTag? tag = NpcNameTags.IsShown(meters, player.IsMovementLocked)
                        ? TagOf(npc.Code)
                        : null;
                    Vector3 screen = viewer.WorldToScreenPoint(HeadTopOf(npc));
                    // 카메라 뒤에 있는 NPC는 화면에 거꾸로 찍히므로 띄우지 않는다.
                    if (tag == null || screen.z <= 0f)
                    {
                        continue;
                    }

                    SlotAt(shown++).Show(tag.Value, ToLayer(new Vector2(screen.x, screen.y)));
                }
            }

            for (int i = shown; i < slots.Count; i++)
            {
                slots[i].Hide();
            }
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

        /// <summary>복제한 이름표 하나. 글자가 바뀔 때만 다시 써서 매 프레임 글자 배치를 새로 하지 않는다.</summary>
        private sealed class TagSlot
        {
            private readonly RectTransform rect;
            private readonly TMP_Text nameLabel;
            private readonly GameObject roleChip;
            private readonly TMP_Text roleLabel;

            public TagSlot(RectTransform rect)
            {
                this.rect = rect;
                nameLabel = rect.Find("Name").GetComponent<TMP_Text>();
                roleChip = rect.Find("RoleChip").gameObject;
                roleLabel = roleChip.transform.Find("Label").GetComponent<TMP_Text>();
            }

            public void Show(NpcNameTag tag, Vector2 layerPoint)
            {
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

                rect.localPosition = layerPoint;
                if (!rect.gameObject.activeSelf)
                {
                    rect.gameObject.SetActive(true);
                }
            }

            public void Hide()
            {
                if (rect.gameObject.activeSelf)
                {
                    rect.gameObject.SetActive(false);
                }
            }
        }
    }
}
