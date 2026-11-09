using System;

namespace A305.Api
{
    /// <summary>
    /// 사용자 설문. 설문을 권할 때가 되면 <see cref="Request"/>로 알리고, 장소 HUD의 설문 카드가 받아 화면 위 가운데에서 내려온다.
    /// 설문을 권하는 곳(대화를 다 쓴 게스트의 가입 안내 창, 첫 호텔 퀘스트(서버 Quest.CHECK_IN)를 끝낸 팡파레의 [다음])은 요청만 하고,
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
    }
}
