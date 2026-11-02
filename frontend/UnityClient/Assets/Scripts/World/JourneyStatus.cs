using System;
using System.Collections.Generic;
using UnityEngine;

namespace A305.World
{
    /// <summary>오늘의 여정 진행 막대 한 칸의 상태</summary>
    public enum JourneyMark
    {
        Upcoming,
        Current,
        Done,
    }

    /// <summary>
    /// 미션 화면과 폰 메뉴가 함께 보는 오늘의 여정. 미션 화면이 단계 칩·할 일·진행·예산을 알리면 폰 메뉴가 같은 내용을 보이고,
    /// 폰의 [마을 지도] 앱은 미션 화면에 큰 마을 지도를 열어 달라고 알린다. 두 화면은 서로의 모듈을 모르므로 이 상태로 만난다.
    /// </summary>
    public sealed class JourneyStatus
    {
        public static JourneyStatus Shared { get; } = new JourneyStatus();

        public bool HasJourney { get; private set; }

        /// <summary>지금 단계 / 전체(예: "1 / 7"). 여정을 시작하기 전이면 null이다.</summary>
        public string StepChip { get; private set; }

        public string Direction { get; private set; }

        public IReadOnlyList<JourneyMark> Marks { get; private set; } = Array.Empty<JourneyMark>();

        public int Balance { get; private set; }

        public event Action Changed;

        /// <summary>폰의 [마을 지도] 앱을 눌렀다.</summary>
        public event Action MapRequested;

        /// <summary>진행 막대 칸 색. 끝낸 단계는 초록, 지금 단계는 코랄, 남은 단계는 옅은 갈색이다.</summary>
        public static Color MarkColor(JourneyMark mark) =>
            mark == JourneyMark.Done ? new Color32(76, 185, 107, 255)
            : mark == JourneyMark.Current ? new Color32(255, 119, 89, 255)
            : new Color32(125, 110, 91, 70);

        public void Show(
            string stepChip,
            string direction,
            IReadOnlyList<JourneyMark> marks,
            int balance
        )
        {
            HasJourney = true;
            StepChip = stepChip;
            Direction = direction;
            Marks = marks;
            Balance = balance;
            Changed?.Invoke();
        }

        public void Clear()
        {
            HasJourney = false;
            StepChip = null;
            Direction = null;
            Marks = Array.Empty<JourneyMark>();
            Balance = 0;
            Changed?.Invoke();
        }

        public void RequestMap() => MapRequested?.Invoke();
    }
}
