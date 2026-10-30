using System;
using System.Collections.Generic;
using System.Text;

namespace A305.Learning
{
    /// <summary>[지난 대화] 창의 목록 글자. 고른 대화 주변의 몇 개만 보여 준다.</summary>
    public static class ConversationHistoryText
    {
        public const string EmptyMessage =
            "아직 나눈 대화가 없어요. 마을에서 NPC에게 말을 걸어 보세요.";

        // 서버 장소 코드의 이름. 목록에 없는 장소는 코드를 그대로 보인다.
        private static readonly IReadOnlyDictionary<string, string> PlaceNames = new Dictionary<
            string,
            string
        >
        {
            { "HOME", "집" },
            { "TOWN", "마을" },
            { "CAFE", "카페" },
            { "HOTEL", "호텔" },
            { "SHOP", "상점" },
        };

        /// <param name="selectedIndex">고른 대화. 목록 범위 안의 값이어야 한다.</param>
        /// <param name="visibleCount">한 번에 보여 줄 대화 수</param>
        /// <param name="hasMore">서버에 더 불러올 대화가 있는지</param>
        public static string Format(
            IReadOnlyList<ConversationSummary> conversations,
            int selectedIndex,
            int visibleCount,
            bool hasMore
        )
        {
            if (conversations.Count == 0)
            {
                return EmptyMessage;
            }

            int firstIndex = Math.Clamp(
                selectedIndex - visibleCount / 2,
                0,
                Math.Max(0, conversations.Count - visibleCount)
            );
            int lastIndex = Math.Min(conversations.Count, firstIndex + visibleCount);
            // 줄바꿈은 플랫폼과 상관없이 \n으로 둔다(AppendLine은 Windows에서 \r\n을 붙인다).
            var text = new StringBuilder();
            for (int i = firstIndex; i < lastIndex; i++)
            {
                string marker = i == selectedIndex ? "→ " : "    ";
                text.Append(marker).Append(Row(conversations[i])).Append('\n');
            }

            text.Append($"\n{selectedIndex + 1} / {conversations.Count}");
            if (hasMore)
            {
                text.Append(" · [다음]으로 더 불러와요");
            }

            return text.ToString();
        }

        private static string Row(ConversationSummary conversation)
        {
            string name = conversation.Npc?.Name ?? "NPC";
            string role = string.IsNullOrEmpty(conversation.Npc?.RoleLabel)
                ? ""
                : " " + conversation.Npc.RoleLabel;
            string place = PlaceNames.TryGetValue(conversation.SceneCode ?? "", out string known)
                ? known
                : conversation.SceneCode;
            string startedAt = conversation.StartedAt.ToString("M월 d일 HH:mm");
            return $"<b>{name}</b>{role} · {place} · {startedAt} · {FeedbackState(conversation)}";
        }

        private static string FeedbackState(ConversationSummary conversation)
        {
            switch (conversation.FeedbackStatus)
            {
                case "DONE":
                    return "피드백 있음";
                case "FAILED":
                    return "피드백을 만들지 못함";
                case "SKIPPED":
                    return "피드백 건너뜀";
                default:
                    return conversation.Status == "ACTIVE" ? "대화 중" : "피드백 없음";
            }
        }
    }
}
