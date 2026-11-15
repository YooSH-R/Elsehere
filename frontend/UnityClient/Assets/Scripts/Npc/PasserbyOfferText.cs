namespace A305.Npc
{
    /// <summary>대화 제안 화면에 보일 문구.</summary>
    public static class PasserbyOfferText
    {
        public const string HurryGreeting =
            "Sorry, I'm running late. Let's talk later!\n늦어서 서둘러야 해요. 다음에 이야기해요!";

        public static string HurryHint(string npcName) => $"{npcName} · 서두르는 중";

        /// <summary>대화 수락 버튼. 키보드를 쓰는 화면에만 E 안내를 붙인다.</summary>
        public static string AcceptLabel(bool showKeyHint) =>
            InteractionLabel("대화하기", showKeyHint);

        internal static string InteractionLabel(string caption, bool showKeyHint) =>
            showKeyHint ? $"{caption}<space=20><size=85%><color=#FFFFFF>E</color></size>" : caption;

        /// <summary>대화 거절 버튼. 키보드를 쓰는 화면에만 Esc 안내를 붙인다.</summary>
        public static string DeclineLabel(bool showKeyHint) =>
            showKeyHint ? "지금은 안 할래요 <size=75%>[Esc]</size>" : "지금은 안 할래요";

        /// <summary>
        /// 누가 말을 걸었는지 알리는 머리말. 행인과 직원이 같은 제안 화면을 쓰므로 "행인이"로 고정하지 않는다.
        /// 이름과 역할이 있으면 함께 보이고, 이름을 받지 못했으면 역할로 알린다.
        /// </summary>
        /// <param name="npcName">대화 대본의 NPC 이름. 모르면 null.</param>
        /// <param name="roleLabel">대화 대본의 NPC 역할. 모르면 null.</param>
        public static string Hint(string npcName, string roleLabel = null)
        {
            if (!string.IsNullOrEmpty(npcName) && !string.IsNullOrEmpty(roleLabel))
            {
                return $"{npcName}({roleLabel})의 대화 제안";
            }

            string speaker = string.IsNullOrEmpty(roleLabel) ? npcName : roleLabel;
            return string.IsNullOrEmpty(speaker)
                ? "누군가 말을 걸었어요"
                : $"{speaker}{SubjectParticle(speaker)} 말을 걸었어요";
        }

        /// <summary>준비해 둔 것이 다 되어 NPC가 제자리에서 부를 때의 머리말. 예: "Mia이(가) 불러요"</summary>
        /// <param name="npcName">서버가 알려 준 NPC 이름. 모르면 null.</param>
        public static string CallHint(string npcName)
        {
            return string.IsNullOrEmpty(npcName)
                ? "누군가 불러요"
                : $"{npcName}{SubjectParticle(npcName)} 불러요";
        }

        /// <summary>부를 때 건네는 말. 음료·식사·빨래 등 무엇이든 같은 문장이다. 예: "기다리던 아이스 라테가 준비됐어요"</summary>
        /// <param name="itemKo">준비한 것의 한국어 이름. 모르면 null.</param>
        /// <summary>NPC가 부탁하려고 말을 걸 때의 알림. 예: Jake가 부탁할 게 있대요</summary>
        public static string FavorHint(string npcName)
        {
            return string.IsNullOrEmpty(npcName)
                ? "누군가 부탁할 게 있대요"
                : $"{npcName}{SubjectParticle(npcName)} 부탁할 게 있대요";
        }

        /// <summary>부탁하려고 말을 걸 때의 첫마디. 무엇을 부탁할지는 대화를 시작하면 NPC가 말한다.</summary>
        public const string FavorLine = "Excuse me! Could I ask you a small favor?";

        public static string CallLine(string itemKo)
        {
            string item = string.IsNullOrEmpty(itemKo) ? "것" : itemKo;
            return $"기다리던 {item}{SubjectParticle(item)} 준비됐어요";
        }

        /// <summary>한글로 끝나면 받침에 맞춰 이·가를 고르고, 영어 이름처럼 알 수 없으면 "이(가)"를 쓴다.</summary>
        public static string SubjectParticle(string word)
        {
            char last = word[word.Length - 1];
            if (last < '가' || last > '힣')
            {
                return "이(가)";
            }

            bool hasFinalConsonant = (last - '가') % 28 != 0;
            return hasFinalConsonant ? "이" : "가";
        }

        /// <summary>플레이어가 먼저 말을 거는 버튼 문구. 이름 뒤 조사가 받침에 따라 바뀌지 않도록 '에게'를 쓴다.</summary>
        /// <param name="npcName">대화 대본의 NPC 이름. 모르면 null.</param>
        public static string TalkLabel(string npcName)
        {
            return string.IsNullOrEmpty(npcName) ? "말 걸기" : $"{npcName}에게 말 걸기";
        }

        /// <summary>대화 버튼 위에 붙일 게스트의 남은 대화 수. 가입 사용자처럼 횟수 제한이 없으면 null이다.</summary>
        public static string RemainingConversations(int? remaining)
        {
            return remaining == null ? null : $"게스트 남은 대화 {remaining}번";
        }

        /// <summary>새 대화를 시작할 수 없을 때 [말 걸기] 자리에 보일 이유.</summary>
        /// <param name="remaining">게스트의 남은 대화 수. 가입 사용자는 null.</param>
        public static string Blocked(int? remaining)
        {
            return remaining == 0 ? "게스트 대화를 모두 썼어요" : "지금은 대화할 수 없어요";
        }

        /// <summary>대화를 다 쓴 게스트에게 [말 걸기]·택시 버튼 자리에 보일 버튼. 누르면 가입 안내 창을 연다.</summary>
        public const string SignupLabel = "가입하고 계속 대화하기";

        /// <summary>
        /// 새 대화를 시작할 수 없을 때 막힌 이유 대신 가입을 권할지. 게스트가 대화 횟수를 다 쓴 경우만이며, 가입하면 기록을 이어 계속 대화할 수 있다.
        /// </summary>
        /// <param name="remaining">게스트의 남은 대화 수. 가입 사용자는 null.</param>
        public static bool OffersSignup(int? remaining)
        {
            return remaining == 0;
        }
    }
}
