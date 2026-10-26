namespace A305.Onboarding
{
    /// <summary>
    /// 닉네임 규칙(AUTH-04). 한글 완성형·영문·숫자만 쓰고, 2자 이상이며 한글은 2, 영문·숫자는 1로 세어 합계 16 이하다.
    /// 서버와 같은 기준으로 입력 중에 바로 안내하며, 최종 판단은 서버가 한다.
    /// </summary>
    public static class NicknameRule
    {
        private const int MinLength = 2;
        private const int MaxWidth = 16;
        private const int HangulWidth = 2;
        private const int AsciiWidth = 1;

        public static NicknameProblem Check(string nickname)
        {
            if (nickname == null || nickname.Length < MinLength)
            {
                return NicknameProblem.TooShort;
            }

            int width = 0;
            foreach (char letter in nickname)
            {
                if (IsHangulSyllable(letter))
                {
                    width += HangulWidth;
                }
                else if (IsAsciiLetterOrDigit(letter))
                {
                    width += AsciiWidth;
                }
                else
                {
                    return NicknameProblem.InvalidCharacter;
                }
            }

            return width <= MaxWidth ? NicknameProblem.None : NicknameProblem.TooLong;
        }

        // 완성형 한글 음절(가~힣). 자음·모음만 있는 글자(ㅋ, ㅏ)는 이 범위 밖이다.
        private static bool IsHangulSyllable(char letter)
        {
            return letter >= '가' && letter <= '힣';
        }

        private static bool IsAsciiLetterOrDigit(char letter)
        {
            return (letter >= 'a' && letter <= 'z')
                || (letter >= 'A' && letter <= 'Z')
                || (letter >= '0' && letter <= '9');
        }
    }
}
