using System;
using System.Collections.Generic;

namespace A305.Npc
{
    /// <summary>
    /// NPC 한 명의 생활. 게임 시각에 따라 일과 활동이 바뀌고, 욕구가 줄며, 가장 급한 욕구를 채울 사물 행동을 고른다.
    /// 규칙은 AI 하루 시뮬레이션(ai/tools/npc_simulate.py)과 같다. 사물까지 걷는 일은 장면이 맡고, 도착하면 <see cref="BeginUsing"/>으로
    /// 이용 시간을 센 뒤 욕구를 채운다.
    /// 퇴근(OFF)에도 사라지지 않고 자기 장소에서 자유 시간처럼 지낸다. 근무가 다시 시작되면 시뮬레이션이 다시 나타난 NPC를 시작값에서
    /// 세듯 욕구도 시작값으로 돌아간다.
    /// </summary>
    public sealed class NpcLife
    {
        public const string Off = "OFF";
        private const string Free = "FREE";
        private const float FullNeed = 100f;

        private readonly NpcLifeData data;
        private readonly NpcLifeEntry npc;
        private readonly Dictionary<string, float> needs = new Dictionary<string, float>();
        private double lastMinutes;
        private double usingUntil;

        /// <param name="nowMinutes">1일차 0시부터 센 게임 시각(분)</param>
        public NpcLife(NpcLifeData data, NpcLifeEntry npc, double nowMinutes)
        {
            this.data = data;
            this.npc = npc;
            lastMinutes = nowMinutes;
            Activity = npc.ActivityAt(nowMinutes);
            StartNeedsOver();
        }

        /// <summary>지금 일과 활동(WORK·BREAK·FREE·OFF)</summary>
        public string Activity { get; private set; }

        /// <summary>하려고 고른 행동. 사물로 걸어가는 중이거나 이용 중이다. 할 일이 없으면 null</summary>
        public NpcActionRule Doing { get; private set; }

        /// <summary>사물에 도착해 이용 중인지</summary>
        public bool IsUsing { get; private set; }

        public float Need(string need)
        {
            return needs[need];
        }

        /// <summary>
        /// 게임 시각을 nowMinutes까지 흘린다. 욕구가 줄고, 이용 중인 행동이 끝나면 욕구를 채운다.
        /// 서버 시각에 맞추느라 시각이 거꾸로 가면 따라잡을 때까지 그 사이를 세지 않는다.
        /// </summary>
        public void Advance(double nowMinutes)
        {
            while (lastMinutes < nowMinutes)
            {
                string activity = npc.ActivityAt(lastMinutes);
                double segmentEnd = Math.Min(nowMinutes, npc.NextChangeAfter(lastMinutes));
                Drop(segmentEnd - lastMinutes);
                lastMinutes = segmentEnd;
                if (activity == Off && npc.ActivityAt(lastMinutes) != Off)
                {
                    StartNeedsOver();
                }
            }

            Activity = npc.ActivityAt(lastMinutes);
            if (IsUsing && lastMinutes >= usingUntil)
            {
                Fill(Doing.Fills);
                GiveUp();
            }
        }

        /// <summary>
        /// 할 일이 없으면 지금 활동에서 채울 수 있는 욕구 중 가장 급한 것을 채울 행동을 고른다. 점수는 Σ 채움 값 × 급함이고, 급함은 욕구가
        /// 낮을수록 가파르게 커진다((100 − 값)/100의 제곱). 점수가 같으면 먼저 적힌 행동, 기준보다 낮으면 아무것도 하지 않는다.
        /// </summary>
        /// <param name="isPlaced">사물 키가 이 장면에 있고 갈 수 있는지</param>
        public NpcActionRule ChooseNext(Predicate<string> isPlaced)
        {
            if (Doing != null)
            {
                return Doing;
            }

            List<string> canFill = CanFill(Activity);
            NpcActionRule best = null;
            double bestScore = 0;
            foreach (NpcActionRule action in data.Actions)
            {
                if (!CanDo(action, isPlaced))
                {
                    continue;
                }

                double score = Score(action, canFill);
                if (score > bestScore)
                {
                    best = action;
                    bestScore = score;
                }
            }

            Doing = bestScore >= data.MinScore ? best : null;
            return Doing;
        }

        /// <summary>고른 사물에 도착했다. 이제부터 행동 시간을 센다.</summary>
        public void BeginUsing(double nowMinutes)
        {
            if (Doing == null)
            {
                return;
            }

            IsUsing = true;
            usingUntil = nowMinutes + Doing.Minutes;
        }

        /// <summary>고른 행동을 그만둔다(말을 걸러 가거나 사물에 갈 수 없을 때). 욕구는 채우지 않는다.</summary>
        public void GiveUp()
        {
            Doing = null;
            IsUsing = false;
        }

        private bool CanDo(NpcActionRule action, Predicate<string> isPlaced)
        {
            return action.Scene == npc.Scene
                && (string.IsNullOrEmpty(action.OnlyRole) || action.OnlyRole == npc.Role)
                && isPlaced(action.ObjectKey);
        }

        private double Score(NpcActionRule action, List<string> canFill)
        {
            double score = 0;
            foreach (KeyValuePair<string, float> fill in action.Fills)
            {
                if (canFill.Contains(fill.Key))
                {
                    double urgency = (FullNeed - needs[fill.Key]) / FullNeed;
                    score += fill.Value * urgency * urgency;
                }
            }

            return score;
        }

        // 퇴근 중에는 자유 시간처럼 지낸다. 공통 규칙은 퇴근을 마을에 없는 시간으로 보고 채울 수 있는 욕구를 따로 두지 않는다.
        private List<string> CanFill(string activity)
        {
            string rule = activity == Off ? Free : activity;
            return data.Activities.TryGetValue(rule, out NpcActivityRule activityRule)
                ? activityRule.CanFill
                : new List<string>();
        }

        private void Drop(double minutes)
        {
            foreach (KeyValuePair<string, NpcNeedRule> need in data.Needs)
            {
                float rate = npc.NeedRates.TryGetValue(need.Key, out float npcRate) ? npcRate : 1f;
                float drop = (float)(need.Value.DropPerHour * rate * minutes / 60);
                needs[need.Key] = Math.Max(0f, needs[need.Key] - drop);
            }
        }

        private void Fill(Dictionary<string, float> fills)
        {
            foreach (KeyValuePair<string, float> fill in fills)
            {
                needs[fill.Key] = Math.Min(FullNeed, needs[fill.Key] + fill.Value);
            }
        }

        private void StartNeedsOver()
        {
            foreach (KeyValuePair<string, NpcNeedRule> need in data.Needs)
            {
                needs[need.Key] = need.Value.Start;
            }
        }
    }
}
