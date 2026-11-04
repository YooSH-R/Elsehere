using System;

namespace A305.Api
{
    /// <summary>
    /// 사용자 설문. 설문을 권할 때가 되면 <see cref="Request"/>로 알리고, 장소 HUD의 설문 카드가 받아 화면 위 가운데에서 내려온다.
    /// 설문을 권하는 곳(대화를 다 쓴 게스트의 가입 안내 창, 첫 호텔 퀘스트(서버 Quest.CHECK_IN)를 끝낸 회원의 완료 창)은 요청만 하고,
    /// 문구·주소·모양은 설문 카드 한 곳에서 바꾼다.
    /// </summary>
    public static class SurveyPrompt
    {
        // 자리표시 주소다. 설문 주소를 받으면 이 값만 바꾼다. 운영 배포 전에 실제 주소로 바꿔야 한다.
        public const string Url = "https://example.com/elsehere-survey";

        /// <summary>설문을 권할 때가 됐다. 설문 카드가 받는다.</summary>
        public static event Action Requested;

        public static void Request() => Requested?.Invoke();
    }
}
