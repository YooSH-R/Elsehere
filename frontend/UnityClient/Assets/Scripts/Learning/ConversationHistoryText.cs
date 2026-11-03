using System;
using System.Collections.Generic;

namespace A305.Learning
{
    /// <summary>[피드백 목록] 한 줄의 글자. "NPC · 장소" 제목, 언제 나눈 대화인지, 피드백이 없으면 그 상태, 색 아이콘의 글자와 색을 정한다.</summary>
    public static class ConversationHistoryText
    {
        public const string EmptyMessage =
            "아직 나눈 대화가 없어요. 마을에서 NPC에게 말을 걸어 보세요.";

        private const string DotColor = "#A39580";
        private const string NoteColor = "#D24F2F";

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

        public static string Title(ConversationSummary conversation)
        {
            string place = PlaceNames.TryGetValue(conversation.SceneCode ?? "", out string known)
                ? known
                : conversation.SceneCode;
            return $"{Name(conversation)}<color={DotColor}> · </color>{place}";
        }

        /// <summary>오늘·어제는 시각까지("오늘 오후 2:05"), 그 전은 날짜만 보인다. 날은 보는 사람의 시간대(<paramref name="now"/>)로 가른다.</summary>
        public static string When(DateTimeOffset startedAt, DateTimeOffset now)
        {
            DateTime started = startedAt.ToOffset(now.Offset).DateTime;
            DateTime today = now.DateTime.Date;
            if (started.Date == today)
                return "오늘 " + Time(started);
            if (started.Date == today.AddDays(-1))
                return "어제 " + Time(started);
            return started.Year == today.Year
                ? $"{started.Month}월 {started.Day}일"
                : $"{started.Year}년 {started.Month}월 {started.Day}일";
        }

        /// <summary>시간 줄. 피드백이 없는 대화만 그 상태를 빨간 글자로 붙인다.</summary>
        public static string Subtitle(ConversationSummary conversation, DateTimeOffset now)
        {
            string when = When(conversation.StartedAt, now);
            string note = Note(conversation);
            return note == null ? when : $"{when} · <color={NoteColor}>{note}</color>";
        }

        public static string AvatarLetter(ConversationSummary conversation) =>
            Name(conversation).Substring(0, 1).ToUpperInvariant();

        /// <summary>
        /// NPC 이름으로 정하는 아이콘 색 번호. 같은 NPC는 언제나 같은 색이다.
        /// 글자 값 합을 3으로 나눈 몫으로 고르면 첫날 함께 만나는 Jake·Mia·Ben이 서로 다른 색이 된다.
        /// </summary>
        public static int AvatarColor(ConversationSummary conversation, int colorCount)
        {
            int sum = 0;
            foreach (char letter in Name(conversation))
                sum += letter;
            return sum / 3 % colorCount;
        }

        private static string Name(ConversationSummary conversation) =>
            string.IsNullOrEmpty(conversation.Npc?.Name) ? "NPC" : conversation.Npc.Name;

        private static string Time(DateTime time)
        {
            int hour = time.Hour % 12 == 0 ? 12 : time.Hour % 12;
            return $"{(time.Hour < 12 ? "오전" : "오후")} {hour}:{time.Minute:00}";
        }

        private static string Note(ConversationSummary conversation)
        {
            switch (conversation.FeedbackStatus)
            {
                case "DONE":
                    return null;
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
