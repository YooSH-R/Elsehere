namespace A305.Onboarding
{
    /// <summary>닉네임 규칙에서 어긋난 점. 어긋난 점이 없으면 <see cref="None"/>이다.</summary>
    public enum NicknameProblem
    {
        None,

        /// <summary>2자보다 짧다.</summary>
        TooShort,

        /// <summary>한글 2, 영문·숫자 1로 센 합계가 16을 넘는다.</summary>
        TooLong,

        /// <summary>한글 완성형·영문·숫자가 아닌 글자가 있다.</summary>
        InvalidCharacter,
    }
}
