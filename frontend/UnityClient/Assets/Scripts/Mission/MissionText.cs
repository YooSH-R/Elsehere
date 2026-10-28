namespace A305.Mission
{
    /// <summary>미션 창에 보일 문구. 미션 시작·교체 알림과 미션 한 줄의 진행 상태다.</summary>
    public static class MissionText
    {
        /// <param name="previous">시작하기 전에 진행 중이던 미션. 없으면 null.</param>
        public static string StartMessage(MissionContent previous, MissionContent started)
        {
            if (previous == null || previous == started)
            {
                return $"미션 시작 · {started.Title}";
            }

            // 미션은 한 번에 하나라 새 미션을 고르면 진행 중이던 미션이 멈춘다. 모르고 바뀌지 않게 두 미션을 함께 알린다.
            return $"'{previous.Title}' 대신 '{started.Title}' 미션을 시작했어요";
        }

        /// <summary>미션 한 줄의 진행 상태. 완료 전에는 대화에 넣어 말할 키워드를 함께 보여 준다.</summary>
        public static string Status(MissionContent mission, bool isActive, bool isCompleted)
        {
            if (isCompleted)
            {
                return "미션 완료";
            }

            bool hasKeywords = mission.Keywords != null && mission.Keywords.Length > 0;
            if (!hasKeywords)
            {
                return isActive ? "진행 중 · 대화에서 해 보면 완료돼요" : "시작 전";
            }

            string words = "말할 단어: " + string.Join(", ", mission.Keywords);
            return isActive ? $"진행 중 · {words}" : $"시작 전 · {words}";
        }
    }
}
