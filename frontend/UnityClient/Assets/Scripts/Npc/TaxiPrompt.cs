namespace A305.Npc
{
    public enum TaxiPromptKind
    {
        None,
        Hail,
        Talk,
        Blocked,
        Signup,
    }

    /// <summary>
    /// 택시 곁에 보일 버튼(<see cref="TaxiRide"/>). 택시를 세우면 바로 기사와 대화하므로, 새 대화를 시작할 수 없으면
    /// [택시 타기]·[말 걸기] 대신 이유를 보인다(행인 [말 걸기]와 같은 문구). 게스트가 대화를 다 썼으면 이유와 함께 가입 버튼을 보인다.
    /// </summary>
    public sealed class TaxiPrompt
    {
        public const string HailLabel = "택시 타기";

        public static readonly TaxiPrompt None = new TaxiPrompt(TaxiPromptKind.None, null, null);

        private TaxiPrompt(TaxiPromptKind kind, string label, string remaining)
        {
            Kind = kind;
            Label = label;
            Remaining = remaining;
        }

        public TaxiPromptKind Kind { get; }

        /// <summary>버튼 문구. 막혔으면 막힌 이유, 가입을 권하면 가입 버튼 문구다.</summary>
        public string Label { get; }

        /// <summary>
        /// 버튼 위에 붙일 남은 대화 수. 가입을 권하면 대화를 다 썼다는 안내다. 남은 수를 모르거나 막혔으면 null이다.
        /// </summary>
        public string Remaining { get; }

        /// <param name="canHail">지나가는 택시를 지금 세울 수 있는지</param>
        /// <param name="canTalk">선 택시 곁이라 기사에게 말을 걸 수 있는지</param>
        /// <param name="canStartConversation">새 대화를 시작할 수 있는지(AI-01)</param>
        /// <param name="remainingConversations">남은 대화 수(AI-01). 모르면 null</param>
        /// <param name="guest">게스트인지(AI-01)</param>
        /// <param name="driverName">대화 대본의 기사 이름. 모르면 null</param>
        public static TaxiPrompt Choose(
            bool canHail,
            bool canTalk,
            bool canStartConversation,
            int? remainingConversations,
            bool guest,
            string driverName
        )
        {
            if (!canHail && !canTalk)
            {
                return None;
            }

            if (
                !canStartConversation
                && PasserbyOfferText.OffersSignup(remainingConversations, guest)
            )
            {
                return new TaxiPrompt(
                    TaxiPromptKind.Signup,
                    PasserbyOfferText.SignupLabel,
                    PasserbyOfferText.Blocked(remainingConversations, guest)
                );
            }

            if (!canStartConversation)
            {
                return new TaxiPrompt(
                    TaxiPromptKind.Blocked,
                    PasserbyOfferText.Blocked(remainingConversations, guest),
                    null
                );
            }

            string remaining = PasserbyOfferText.RemainingConversations(
                remainingConversations,
                guest
            );
            return canHail
                ? new TaxiPrompt(TaxiPromptKind.Hail, HailLabel, remaining)
                : new TaxiPrompt(
                    TaxiPromptKind.Talk,
                    PasserbyOfferText.TalkLabel(driverName),
                    remaining
                );
        }
    }
}
