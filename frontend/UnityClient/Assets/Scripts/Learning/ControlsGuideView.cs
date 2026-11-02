using A305.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>
    /// 오른쪽 아래 조작법 카드와 메뉴(≡)의 [조작법]. 카드의 X로 닫고 [조작법]으로 다시 연다.
    /// 휴대폰·태블릿이면 터치 조작을 보인다. 대화 중이거나 화면을 덮는 창이 열려 있으면 <see cref="LearningController"/>가 잠시 숨긴다.
    /// </summary>
    public sealed class ControlsGuideView : MonoBehaviour
    {
        [SerializeField]
        private GameObject card;

        [SerializeField]
        private TMP_Text bodyText;

        [SerializeField]
        [Tooltip("닫아도 메뉴에서 다시 볼 수 있다는 안내")]
        private TMP_Text hintText;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        [Tooltip("메뉴(≡)의 [조작법]")]
        private Button openButton;

        private bool isBlocked;

        public void SetBlocked(bool blocked)
        {
            if (isBlocked == blocked)
                return;
            isBlocked = blocked;
            Show();
        }

        private void Awake()
        {
            hintText.text = ControlsGuideText.ReopenHint;
            closeButton.onClick.AddListener(() =>
            {
                ControlsGuide.Close();
                Show();
            });
            openButton.onClick.AddListener(() =>
            {
                ControlsGuide.Reopen();
                Show();
            });
            Show();
        }

        private void Show()
        {
            bool shouldShow = ControlsGuide.ShouldShow(isBlocked);
            if (shouldShow)
                bodyText.text = ControlsGuideText.Body(PlayerTouchControls.ShouldShowControls());
            card.SetActive(shouldShow);
        }
    }
}
