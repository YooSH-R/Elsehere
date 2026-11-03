namespace A305.Learning
{
    /// <summary>[피드백 목록]의 쪽 나누기. 한 쪽에 대화 4개를 보이고, 받은 대화의 마지막 쪽에서는 서버에 더 있을 때만 다음으로 넘긴다.</summary>
    public static class ConversationHistoryPages
    {
        public const int PageSize = 4;

        /// <summary>받은 대화를 담는 쪽 수. 대화가 없어도 빈 쪽 하나로 센다.</summary>
        public static int PageCount(int count) =>
            System.Math.Max(1, (count + PageSize - 1) / PageSize);

        public static string Label(int page, int count) => $"{page + 1} / {PageCount(count)}";

        public static bool CanGoNext(int page, int count, bool hasMore) =>
            count > 0 && (page < PageCount(count) - 1 || hasMore);

        /// <summary>다음 쪽을 보려면 서버에서 대화를 더 받아야 하는지</summary>
        public static bool NeedsMore(int page, int count, bool hasMore) =>
            hasMore && page >= PageCount(count) - 1;
    }
}
