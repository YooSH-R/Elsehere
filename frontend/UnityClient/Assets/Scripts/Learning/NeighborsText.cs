using System.Collections.Generic;

namespace A305.Learning
{
    /// <summary>[이웃] 창(마을 NPC 목록과 정보 카드) 문구</summary>
    public static class NeighborsText
    {
        public const string Title = "이웃";
        public const string AppLabel = "이웃";
        public const string Loading = "이웃을 불러오는 중이에요…";
        public const string Empty = "아직 마을 사람 목록이 비어 있어요.";
        public const string NoneOpenYet = "NPC와 대화를 끝까지 나누면 이웃으로 열려요.";
        public const string Failed =
            "지금은 이웃 목록을 받아 오지 못했어요. 잠시 후 다시 시도해 주세요.";
        public const string Retry = "다시 시도";
        public const string Back = "목록으로";
        public const string ShowOnMap = "지도에서 보기";
        public const string Locked = "잠김";

        private const string HiddenName = "???";
        private const string HowToUnlock = "끝까지 대화하면 열려요";
        private const string NotKnownYet = "아직 몰라요";

        // NPC가 말을 걸 때의 머리말(PasserbyOfferText)과 같은 크기로 역할을 작게 붙인다.
        private const string RoleSize = "80%";

        // 대화창 이름 옆 호감도(ConversationStandingDisplay)와 같은 하트
        private const string Heart = "<color=#FF6B81>♥</color>";

        /// <summary>"Max 오락실 직원"처럼 이름 옆에 역할을 작게 적는다. 열리지 않은 NPC는 이름 대신 ???이고, 역할이 없으면 이름만.</summary>
        public static string Name(Neighbor neighbor)
        {
            string name = neighbor.Unlocked ? neighbor.Name : HiddenName;
            return string.IsNullOrEmpty(neighbor.RoleLabel)
                ? name
                : $"{name} <size={RoleSize}>{neighbor.RoleLabel}</size>";
        }

        /// <summary>"모퉁이 카페 · 대화 3번 · 아는 정도 30". 열리지 않은 NPC는 장소와 어떻게 열리는지.</summary>
        public static string Detail(Neighbor neighbor)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(neighbor.SceneName))
                parts.Add(neighbor.SceneName);
            if (neighbor.Unlocked)
            {
                parts.Add($"대화 {neighbor.TalkCount ?? 0}번");
                parts.Add($"아는 정도 {neighbor.Recognition ?? 0}");
            }
            else
            {
                parts.Add(HowToUnlock);
            }
            return string.Join(" · ", parts);
        }

        public static string Affinity(Neighbor neighbor) =>
            neighbor.Unlocked ? $"{Heart} {neighbor.Affinity ?? 0}" : Locked;

        /// <summary>"3 / 36명"처럼 열린 이웃 수와 마을 전체 NPC 수</summary>
        public static string Count(int open, int total) => $"{open} / {total}명";

        /// <summary>정보 카드의 한 줄씩: 일하는 곳, 지금 위치, 직업, 좋아하는 것, 호감도·아는 정도, 대화 수·마지막 대화 날짜</summary>
        /// <param name="whereabouts">지금 무엇을 하는지(A305.Npc의 NpcWhereabouts)</param>
        public static string Facts(Neighbor neighbor, string whereabouts)
        {
            string interests =
                neighbor.Interests == null || neighbor.Interests.Count == 0
                    ? NotKnownYet
                    : string.Join(", ", neighbor.Interests);
            string talks = $"대화 {neighbor.TalkCount ?? 0}번";
            if (neighbor.LastTalkedAt.HasValue)
                talks +=
                    $" · 마지막 대화 {neighbor.LastTalkedAt.Value.Month}월 {neighbor.LastTalkedAt.Value.Day}일";
            return string.Join(
                "\n",
                $"일하는 곳 · {neighbor.SceneName}",
                $"지금 · {whereabouts}",
                $"직업 · {(string.IsNullOrEmpty(neighbor.Occupation) ? NotKnownYet : neighbor.Occupation)}",
                $"좋아하는 것 · {interests}",
                $"호감도 {Heart} {neighbor.Affinity ?? 0} · 아는 정도 {neighbor.Recognition ?? 0}",
                talks
            );
        }

        /// <summary>
        /// 정보 카드의 [지도에서 보기] 대신 보일 안내. 지도에 찍을 자리가 있으면 null이라 버튼을 보인다.
        /// 숙소에서 쉬면 만날 수 없고, 걸어 다니는 중이면 마을에 켜진 몸을 봐야 자리를 안다.
        /// </summary>
        public static string MapHint(A305.Npc.NpcSpot spot, bool playerInTown) =>
            spot switch
            {
                A305.Npc.NpcSpot.Body or A305.Npc.NpcSpot.Workplace => null,
                A305.Npc.NpcSpot.Resting => "숙소에서 쉬고 있어서 지금은 만날 수 없어요.",
                _ when playerInTown => "지금은 지도에서 찾을 수 없어요.",
                _ => "마을에 나가면 지도에서 찾을 수 있어요.",
            };

        public static string AvatarLetter(Neighbor neighbor) =>
            neighbor.Unlocked ? AvatarName(neighbor).Substring(0, 1).ToUpperInvariant() : "?";

        /// <summary>
        /// 이름으로 고르는 아이콘 색 번호. [피드백 목록](<see cref="ConversationHistoryText.AvatarColor"/>)과 같은 계산이라
        /// 같은 NPC는 두 목록에서 같은 색이다.
        /// </summary>
        public static int AvatarColor(Neighbor neighbor, int colorCount)
        {
            int sum = 0;
            foreach (char letter in AvatarName(neighbor))
                sum += letter;
            return sum / 3 % colorCount;
        }

        private static string AvatarName(Neighbor neighbor) =>
            !neighbor.Unlocked || string.IsNullOrEmpty(neighbor.Name) ? "NPC" : neighbor.Name;
    }
}
