namespace A305.Mission
{
    /// <summary>장소 연습 탭의 글자.</summary>
    public static class PracticeBoardText
    {
        /// <summary>왼쪽 장소 줄 오른쪽의 끝낸 상황 수. 예: 1 / 3</summary>
        public static string DoneCount(PracticePlace place) =>
            $"{place.DoneCount} / {place.TotalCount}";

        /// <summary>그 장소의 상황을 모두 끝냈는지. 끝낸 수를 초록으로 보인다.</summary>
        public static bool IsAllDone(PracticePlace place) =>
            place.TotalCount > 0 && place.DoneCount >= place.TotalCount;

        /// <summary>오른쪽 장소 이름 옆 직원 칩. 예: Rosa · 식당 직원</summary>
        public static string NpcChip(PracticePlace place) =>
            string.IsNullOrEmpty(place.RoleLabel)
                ? place.NpcName
                : $"{place.NpcName} · {place.RoleLabel}";
    }
}
