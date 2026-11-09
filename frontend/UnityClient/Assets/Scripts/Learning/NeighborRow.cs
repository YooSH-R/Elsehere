using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>
    /// [이웃]의 NPC 한 줄. 색 아이콘 위 이름 첫 글자, 이름과 작은 역할, "장소 · 대화 N번 · 아는 정도 N", 오른쪽 호감도를 그린다.
    /// 열린 이웃만 누를 수 있고(정보 카드), 잠긴 NPC는 흐리게 그린다.
    /// </summary>
    public class NeighborRow : MonoBehaviour
    {
        // 잠긴 NPC 줄의 불투명도
        private const float LockedAlpha = .55f;

        [SerializeField]
        private Button button;

        [SerializeField]
        private CanvasGroup group;

        [SerializeField]
        private Image avatar;

        [SerializeField]
        private TMP_Text letter;

        [SerializeField]
        private TMP_Text nameText;

        [SerializeField]
        private TMP_Text detailText;

        [SerializeField]
        private TMP_Text affinityText;

        /// <summary>줄을 누르면 정보 카드를 연다. <see cref="NeighborsView"/>가 연결한다.</summary>
        public Button Button => button;

        public void Show(Neighbor neighbor, Sprite avatarSprite)
        {
            gameObject.SetActive(true);
            avatar.sprite = avatarSprite;
            letter.text = NeighborsText.AvatarLetter(neighbor);
            nameText.text = NeighborsText.Name(neighbor);
            detailText.text = NeighborsText.Detail(neighbor);
            affinityText.text = NeighborsText.Affinity(neighbor);
            button.interactable = neighbor.Unlocked;
            group.alpha = neighbor.Unlocked ? 1f : LockedAlpha;
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
