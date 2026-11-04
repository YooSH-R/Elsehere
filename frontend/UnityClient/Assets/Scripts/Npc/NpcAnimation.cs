namespace A305.Npc
{
    /// <summary>
    /// NPC 모델이 재생하는 동작. 값은 NPC 전용 Animator Controller(Builder가 만드는 NpcLife.controller)의 Action 정수와 같다.
    /// 동작 클립은 Quaternius Universal Animation Library(CC0)를 Humanoid로 리타겟팅한 것이다.
    /// </summary>
    public enum NpcAnimation
    {
        /// <summary>걷기·서 있기(이동 속도에 따른 기본 동작)</summary>
        None = 0,

        /// <summary>앞쪽 물건을 손으로 다루기(Interact)</summary>
        Work = 1,

        /// <summary>탁자 위 물건을 집어 들기(PickUp_Table)</summary>
        PickUp = 2,

        /// <summary>손짓하며 말하기(Idle_Talking_Loop)</summary>
        Talk = 3,

        /// <summary>앉아 쉬기(Sitting_Idle_Loop). 다리는 앉는 면에 맞춰 따로 구부린다.</summary>
        Sit = 4,
    }

    public static class NpcAnimations
    {
        /// <summary>사물을 쓰는 동안의 동작. 행동 이름은 생활 데이터(world-rules)의 action이고, 모르는 행동은 서 있는다.</summary>
        public static NpcAnimation WhileUsing(string action, bool seated)
        {
            if (seated)
            {
                return NpcAnimation.Sit;
            }

            switch (action)
            {
                case "MAKE_DRINK":
                case "SERVE_GUEST":
                    return NpcAnimation.Work;
                case "EAT":
                case "DRINK_COFFEE":
                    return NpcAnimation.PickUp;
                default:
                    return NpcAnimation.None;
            }
        }
    }
}
