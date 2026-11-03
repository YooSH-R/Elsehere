using UnityEngine;
using UnityEngine.UI;

namespace A305.Onboarding
{
    /// <summary>시안을 렌더링한 고르기 카드. 고르면 코랄 테두리·체크가 있는 그림으로, 아니면 기본 그림으로 바꾼다.</summary>
    public class ChoiceArt : MonoBehaviour
    {
        [SerializeField]
        private Image face;

        [SerializeField]
        private Sprite selectedSprite;

        [SerializeField]
        private Sprite unselectedSprite;

        /// <summary>Builder와 테스트에서 그림을 연결한다.</summary>
        public void Set(Image cardFace, Sprite selected, Sprite unselected)
        {
            face = cardFace;
            selectedSprite = selected;
            unselectedSprite = unselected;
        }

        public void Show(bool selected) =>
            face.sprite = selected ? selectedSprite : unselectedSprite;
    }
}
