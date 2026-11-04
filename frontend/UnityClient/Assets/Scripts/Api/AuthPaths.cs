namespace A305.Api
{
    /// <summary>API 호출이 아니라 페이지 이동(<see cref="BrowserRedirect"/>)으로 여는 인증 주소.</summary>
    public static class AuthPaths
    {
        /// <summary>
        /// AUTH-01 Google 로그인. 게스트 세션에서 열면 가입할 때 그 게스트가 회원으로 바뀐다(AUTH-02·04).
        /// </summary>
        public const string GoogleLogin = "/api/v1/auth/login/google";
    }
}
