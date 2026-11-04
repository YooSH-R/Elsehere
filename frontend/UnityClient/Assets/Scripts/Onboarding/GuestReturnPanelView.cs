using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Onboarding
{
    /// <summary>
    /// 게임에 들어가기 전에 한 번 멈춰 알리는 입구 화면.
    /// 게스트로 다시 접속했으면 [Google로 가입하고 이어 하기]·[게스트로 계속하기]를 고르게 하고(<see cref="EntryScreen.GuestReturn"/>),
    /// 게스트 세션에서 이미 가입한 계정으로 들어왔으면 게스트 기록을 합치지 않았다고 알린다(<see cref="EntryScreen.ExistingAccount"/>).
    /// </summary>
    public class GuestReturnPanelView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text titleText;

        [SerializeField]
        private TMP_Text bodyText;

        [SerializeField]
        [Tooltip("Google 로그인이 실패하거나 취소돼 돌아왔을 때의 안내")]
        private TMP_Text noticeText;

        [SerializeField]
        private Button signupButton;

        [SerializeField]
        private Button continueButton;

        public event Action SignupRequested;
        public event Action ContinueRequested;

        // 가입 버튼을 숨기면 [계속하기]를 그 자리로 올리고 창을 그만큼 줄여 빈 칸을 남기지 않는다.
        private Vector2 continueBelowSignup;
        private Vector2 continueInSignupPlace;
        private Vector2 sizeWithSignup;
        private bool isLayoutRead;

        private void Awake()
        {
            signupButton.onClick.AddListener(() => SignupRequested?.Invoke());
            continueButton.onClick.AddListener(() => ContinueRequested?.Invoke());
        }

        /// <param name="notice">Google 로그인이 실패해 돌아왔으면 그 안내. 없으면 빈 문자열.</param>
        public void ShowGuestReturn(string notice)
        {
            Show(
                GuestReturnText.GuestTitle,
                GuestReturnText.GuestBody,
                showsSignup: true,
                GuestReturnText.ContinueAsGuest,
                notice
            );
        }

        public void ShowExistingAccount()
        {
            Show(
                GuestReturnText.ExistingAccountTitle,
                GuestReturnText.ExistingAccountBody,
                showsSignup: false,
                GuestReturnText.Continue,
                ""
            );
        }

        private void Show(
            string title,
            string body,
            bool showsSignup,
            string continueLabel,
            string notice
        )
        {
            titleText.text = title;
            bodyText.text = body;
            signupButton.gameObject.SetActive(showsSignup);
            continueButton.GetComponentInChildren<TMP_Text>().text = continueLabel;
            noticeText.text = notice;
            noticeText.gameObject.SetActive(!string.IsNullOrEmpty(notice));
            PlaceContinue(showsSignup);
        }

        private void PlaceContinue(bool showsSignup)
        {
            var panel = (RectTransform)transform;
            var signup = (RectTransform)signupButton.transform;
            var continueRect = (RectTransform)continueButton.transform;
            if (!isLayoutRead)
            {
                continueBelowSignup = continueRect.anchoredPosition;
                continueInSignupPlace = signup.anchoredPosition;
                sizeWithSignup = panel.sizeDelta;
                isLayoutRead = true;
            }

            float signupRow = continueInSignupPlace.y - continueBelowSignup.y;
            continueRect.anchoredPosition = showsSignup
                ? continueBelowSignup
                : continueInSignupPlace;
            panel.sizeDelta = showsSignup
                ? sizeWithSignup
                : sizeWithSignup - new Vector2(0, signupRow);
        }
    }
}
