using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Onboarding
{
    /// <summary>듣기 문항의 보기 한 줄. 고르면 코랄 테두리 줄과 채운 번호 동그라미 그림으로 바뀐다.</summary>
    public class LevelTestChoiceRow : MonoBehaviour
    {
        // 시안의 번호색(잉크 55%)을 옅은 회색 동그라미 위에 섞은 불투명 색
        private static readonly Color NumberColor = new Color32(134, 128, 122, 255);

        [SerializeField]
        private Button button;

        [SerializeField]
        private ChoiceArt rowArt;

        [SerializeField]
        private ChoiceArt numberArt;

        [SerializeField]
        private TMP_Text numberText;

        [SerializeField]
        private TMP_Text choiceText;

        public Button Button => button;

        public void Show(string choice, bool selected)
        {
            choiceText.text = choice;
            rowArt.Show(selected);
            numberArt.Show(selected);
            numberText.color = selected ? Color.white : NumberColor;
        }
    }
}
