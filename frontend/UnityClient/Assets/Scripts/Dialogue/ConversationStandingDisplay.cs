using System.Text;

namespace A305.Dialogue
{
    /// <summary>
    /// 대화창 NPC 이름 옆에 보일 호감도와 무례 경고 칸 한 줄. 예: "♥ 12   ○○○".
    /// 상태가 바뀐 뒤 몇 초 동안은 호감도 변화량(예: -6)과 무례 경고 문구를 함께 보인다.
    /// </summary>
    public sealed class ConversationStandingDisplay
    {
        /// <summary>변화량과 경고 문구를 보이는 시간(초)</summary>
        public const float FlashSeconds = 4f;

        private const string Heart = "<color=#FF6B81>♥</color>";
        private const string DropColor = "#E05252";
        private const string RiseColor = "#5FAF6B";

        private int shownVersion;
        private float flashUntil;

        /// <summary>
        /// 이 칸이 가장 길어지는 줄: 호감도 -100에 깎인 변화량과, 두 무례 문구 가운데 긴 "n번이면 대화가 끝나요"가 붙은 때.
        /// 이름 줄의 욕구 게이지가 이 폭 뒤에 자리를 잡아, 무례 경고 문구가 붙었다 사라져도 움직이지 않는다.
        /// </summary>
        public static string Widest(int rudeLimit)
        {
            var standing = new ConversationStanding();
            standing.Apply(
                new ConversationState
                {
                    Affinity = -91,
                    RudeCount = 0,
                    RudeLimit = rudeLimit,
                }
            );
            standing.Apply(
                new ConversationState
                {
                    Affinity = -100,
                    RudeCount = 1,
                    RudeLimit = rudeLimit,
                }
            );
            return new ConversationStandingDisplay().Text(standing, 0f);
        }

        /// <param name="now">지금 시각(초). 화면은 실제 시간을 넘긴다.</param>
        /// <returns>보일 문구. 서버가 호감도를 보내지 않았으면 null이며 화면은 숨긴다.</returns>
        public string Text(ConversationStanding standing, float now)
        {
            if (standing == null || !standing.IsKnown)
            {
                return null;
            }

            // 새 상태를 받은 때부터 잠깐 변화량과 경고 문구를 보인다.
            if (standing.Version != shownVersion)
            {
                shownVersion = standing.Version;
                flashUntil = now + FlashSeconds;
            }

            bool isFlashing = now < flashUntil;
            var text = new StringBuilder($"{Heart} {standing.Affinity}");
            if (isFlashing && standing.AffinityChange != 0)
            {
                string color = standing.AffinityChange > 0 ? RiseColor : DropColor;
                text.Append($" <color={color}>{standing.AffinityChangeText}</color>");
            }

            text.Append("   ");
            for (int index = 0; index < standing.RudeLimit; index++)
            {
                text.Append(standing.IsStrike(index) ? '●' : '○');
            }

            if (isFlashing && standing.RudeNotice != null)
            {
                text.Append($"   <size=80%>{standing.RudeNotice}</size>");
            }

            return text.ToString();
        }
    }
}
