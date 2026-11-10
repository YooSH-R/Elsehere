using System;

namespace A305.Api
{
    /// <summary>
    /// 사용자 설문. 설문을 권할 때가 되면 <see cref="Request"/>로 알리고, 장소 HUD의 설문 카드가 받아 화면 위 가운데에서 내려온다.
    /// 첫 대화 피드백을 닫으면 기기에서 한 번 안내한다. 뒤늦은 가입·퀘스트 안내는 중복 요청하지 않는다.
    /// 문구·주소·모양은 설문 카드 한 곳에서 바꾼다.
    /// </summary>
    public static class SurveyPrompt
    {
        // 팀 사용자 설문(Google 설문지)의 응답 페이지. 설문을 바꾸면 이 값만 바꾼다.
        public const string Url =
            "https://docs.google.com/forms/d/e/1FAIpQLSctxlb9ka3jrbFp24OjHd-HxR0U6Vh_s6sTdPR3kvRDyeDrVA/viewform";

        /// <summary>설문을 권할 때가 됐다. 설문 카드가 받는다.</summary>
        public static event Action Requested;

        public static void Request() => Requested?.Invoke();

        public const string InvitationKey = "elsehere.survey.first-feedback.invited.v1";

        public static bool RequestAfterFeedback()
        {
            if (UnityEngine.PlayerPrefs.GetInt(InvitationKey, 0) != 0)
                return false;
            UnityEngine.PlayerPrefs.SetInt(InvitationKey, 1);
            UnityEngine.PlayerPrefs.Save();
            Request();
            return true;
        }

        // 예전 자동 안내 시점이 늦게 와도 첫 피드백에서 이미 안내했다면 반복하지 않는다.
        public static void RequestUnlessInvited()
        {
            if (UnityEngine.PlayerPrefs.GetInt(InvitationKey, 0) == 0)
                Request();
        }
    }
}
