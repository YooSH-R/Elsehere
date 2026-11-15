using System.Collections.Generic;

namespace A305.Dialogue
{
    /// <summary>게이지 색 단계. 욕구가 낮을수록 따뜻한 경고색이다.</summary>
    public enum NpcNeedTone
    {
        Fine,
        Low,
        Urgent,
    }

    /// <summary>대화 창 이름 줄에 그리는 욕구 게이지 하나</summary>
    public readonly struct NpcNeedGauge
    {
        public NpcNeedGauge(string label, float fill, NpcNeedTone tone)
        {
            Label = label;
            Fill = fill;
            Tone = tone;
        }

        public string Label { get; }

        /// <summary>막대를 채운 비율(0~1). 가득할수록 채워진 상태다.</summary>
        public float Fill { get; }

        public NpcNeedTone Tone { get; }
    }

    /// <summary>
    /// NPC 욕구 가운데 대화 창에 보일 게이지를 고른다. 낮을 때 NPC가 무엇을 바라는지 분명하고 부탁으로 이어지는
    /// 배고픔·피로·외로움만 보인다. 재미·업무는 생활 동작(책 읽기, 음료 만들기 등)을 고르는 데만 쓴다(docs/npc-life-design.md 6절).
    /// </summary>
    public static class NpcNeedGauges
    {
        /// <summary>이 값 아래면 조금 부족한 색(옅은 모래색)이다.</summary>
        public const float LowBelow = 70;

        /// <summary>이 값 아래면 급한 색(차분한 산호색)이다.</summary>
        public const float UrgentBelow = 40;

        private static readonly (string Need, string Label)[] Shown =
        {
            ("HUNGER", "배고픔"),
            ("ENERGY", "피로"),
            ("SOCIAL", "외로움"),
        };

        /// <param name="needs">대화를 시작한 순간의 욕구. 생활하지 않는 NPC면 null</param>
        public static IReadOnlyList<NpcNeedGauge> From(NpcNeeds needs)
        {
            var gauges = new List<NpcNeedGauge>();
            if (needs == null)
            {
                return gauges;
            }

            foreach ((string need, string label) in Shown)
            {
                if (needs.Values.TryGetValue(need, out float value))
                {
                    gauges.Add(new NpcNeedGauge(label, value / 100f, ToneOf(value)));
                }
            }

            return gauges;
        }

        private static NpcNeedTone ToneOf(float value)
        {
            if (value < UrgentBelow)
            {
                return NpcNeedTone.Urgent;
            }

            return value < LowBelow ? NpcNeedTone.Low : NpcNeedTone.Fine;
        }
    }
}
