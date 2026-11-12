using System;

namespace A305.Api
{
    /// <summary>
    /// 사용자 설문. 설문을 권할 때가 되면 <see cref="Request"/>로 알리고, 장소 HUD의 설문 카드가 받아 화면 위 가운데에서 내려온다.
    /// 튜토리얼을 제외한 첫 일반 대화 피드백을 닫으면 기기에서 한 번 안내한다.
    /// 문구·주소·모양은 설문 카드 한 곳에서 바꾼다.
    /// </summary>
    public static class SurveyPrompt
    {
        // 팀 사용자 설문(Google 설문지)의 응답 페이지. 설문을 바꾸면 이 값만 바꾼다.
        public const string Url =
            "https://docs.google.com/forms/d/e/1FAIpQLSf_CIGUBIOuuadvDprLZVpgM9foxp3r1EaMqCQ-eS3Xul7plg/viewform";

        /// <summary>설문을 권할 때가 됐다. 설문 카드가 받는다.</summary>
        public static event Action Requested;

        public static void Request() => Requested?.Invoke();

        /// <summary>안내가 접힌 뒤 남는 메인 화면의 설문 진입점.</summary>
        public static bool EntryVisible { get; private set; }

        public static void RevealEntry() => EntryVisible = true;

        public static void RestoreEntry() =>
            EntryVisible = UnityEngine.PlayerPrefs.GetInt(InvitationKey, 0) != 0;

        // v1은 튜토리얼에서도 소비돼 일반 대화 안내 여부를 구별할 수 없다.
        public const string InvitationKey = "elsehere.survey.first-real-feedback.invited.v2";

        public static bool RequestAfterFeedback(bool isRegularConversation = true)
        {
            if (!isRegularConversation || UnityEngine.PlayerPrefs.GetInt(InvitationKey, 0) != 0)
                return false;
            UnityEngine.PlayerPrefs.SetInt(InvitationKey, 1);
            UnityEngine.PlayerPrefs.Save();
            Request();
            return true;
        }
    }
}
