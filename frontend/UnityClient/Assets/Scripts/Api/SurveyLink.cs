namespace A305.Api
{
    /// <summary>
    /// 사용자 설문 주소. 대화를 다 쓴 게스트의 가입 안내 창이 [설문 하러 가기]로 새 탭에서 연다.
    /// 첫 호텔 퀘스트(서버 Quest.CHECK_IN)를 끝낸 회원에게도 같은 주소를 쓴다.
    /// </summary>
    public static class SurveyLink
    {
        // 자리표시 주소다. 설문 주소를 받으면 이 값만 바꾼다. 운영 배포 전에 실제 주소로 바꿔야 한다.
        public const string Url = "https://example.com/elsehere-survey";
    }
}
