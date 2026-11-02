namespace A305.Tutorial
{
    /// <summary>
    /// 튜토리얼 진행. 버튼·이동·호버·공간 이동·대화·피드백·미션 신호를 받아 단계를 넘긴다.
    /// 지금 단계와 관계없는 신호는 무시한다.
    /// </summary>
    public sealed class TutorialProgress
    {
        private readonly string tutorialNpcCode;
        private readonly string missionNpcCode;
        private bool hasWalked;
        private bool hasHovered;
        private bool isFinishSaved;

        /// <param name="tutorialNpcCode">길을 묻는 튜토리얼 행인의 NPC 키(ERD npcs.code)</param>
        /// <param name="missionNpcCode">미션 안내에서 대화하러 가는 NPC 키</param>
        public TutorialProgress(string tutorialNpcCode, string missionNpcCode)
        {
            this.tutorialNpcCode = tutorialNpcCode;
            this.missionNpcCode = missionNpcCode;
        }

        public TutorialStep Step { get; private set; } = TutorialStep.Welcome;

        /// <summary>이번 Play에서 튜토리얼을 끝낸 방법. 끝내지 않았거나 서버에서 이미 끝난 사용자면 null.</summary>
        public TutorialFinish? Finish { get; private set; }

        /// <summary>끝낸 기록을 아직 서버에 저장하지 않았는지.</summary>
        public bool NeedsFinishSave => Finish != null && !isFinishSaved;

        /// <summary>서버 저장을 마쳤거나 다시 보낼 필요가 없을 때 호출한다.</summary>
        public void MarkFinishSaved()
        {
            isFinishSaved = true;
        }

        /// <summary>서버에서 이미 튜토리얼을 끝낸 사용자. 안내 없이 끝난 상태로 시작하며 서버에 다시 알리지 않는다.</summary>
        public void MarkFinishedOnServer()
        {
            Step = TutorialStep.Completed;
            Finish = null;
        }

        /// <summary>안내 카드의 주 버튼으로 다음 단계로 넘어갈 수 있는지</summary>
        public bool CanContinue
        {
            get
            {
                switch (Step)
                {
                    case TutorialStep.Welcome:
                    case TutorialStep.MissionWindow:
                        return true;
                    case TutorialStep.Move:
                        return hasWalked;
                    case TutorialStep.Hover:
                        return hasHovered;
                    default:
                        return false;
                }
            }
        }

        /// <summary>미션 창은 미션 안내부터 보인다. 튜토리얼을 건너뛰어도 보인다.</summary>
        public bool IsMissionPanelVisible => Step >= TutorialStep.MissionWindow;

        /// <summary>
        /// 튜토리얼 중인지. 그동안 길을 묻는 행인(Ben)은 집 앞에서 튜토리얼 대본대로만 대화하고 생활하지 않는다(튜토리얼 모드).
        /// </summary>
        public bool IsGuiding => Step != TutorialStep.Completed;

        public void Continue()
        {
            if (!CanContinue)
            {
                return;
            }

            Step = Step switch
            {
                TutorialStep.Welcome => TutorialStep.Move,
                TutorialStep.Move => TutorialStep.Hover,
                TutorialStep.Hover => TutorialStep.MissionIntro,
                TutorialStep.MissionWindow => TutorialStep.MissionSelect,
                _ => Step,
            };
        }

        /// <summary>플레이어가 걸었음을 알린다. 걷기 안내 단계에서 걸은 것만 센다.</summary>
        public void ReportWalked()
        {
            if (Step == TutorialStep.Move)
            {
                hasWalked = true;
            }
        }

        /// <summary>사물 툴팁이 보였음을 알린다. 사물 안내 단계에서 본 것만 센다.</summary>
        public void ReportHovered()
        {
            if (Step == TutorialStep.Hover)
            {
                hasHovered = true;
            }
        }

        /// <summary>방 밖 공간에 들어왔음을 알린다. 방 안내 중이면 남은 안내를 건너뛰고 이웃 만나기로 넘어간다.</summary>
        public void LeaveHome()
        {
            if (Step <= TutorialStep.MissionIntro)
            {
                Step = TutorialStep.NeighborApproach;
            }
        }

        public void StartDialogue(string npcCode)
        {
            if (Step == TutorialStep.NeighborApproach && npcCode == tutorialNpcCode)
            {
                Step = TutorialStep.NeighborDialogue;
            }
            else if (Step == TutorialStep.MissionTalk && npcCode == missionNpcCode)
            {
                Finish = TutorialFinish.Completed;
                Step = TutorialStep.Completed;
            }
        }

        /// <summary>
        /// 길 안내를 끝까지 마치면 피드백 단계로 넘어가고, 중간에 끝내면 이웃이 다시 다가오도록 이웃 만나기로 되돌린다.
        /// </summary>
        public void EndDialogue(string npcCode, bool isGuidedCompleted)
        {
            if (Step != TutorialStep.NeighborDialogue || npcCode != tutorialNpcCode)
            {
                return;
            }

            Step = isGuidedCompleted
                ? TutorialStep.NeighborFeedback
                : TutorialStep.NeighborApproach;
        }

        public void CloseFeedback(string npcCode)
        {
            if (Step == TutorialStep.NeighborFeedback && npcCode == tutorialNpcCode)
            {
                Step = TutorialStep.MissionWindow;
            }
        }

        public void StartMission()
        {
            if (Step == TutorialStep.MissionWindow || Step == TutorialStep.MissionSelect)
            {
                Step = TutorialStep.MissionTalk;
            }
        }

        /// <summary>안내를 건너뛴다. 이미 끝난 튜토리얼은 끝낸 방법을 바꾸지 않는다.</summary>
        public void Skip()
        {
            if (Step == TutorialStep.Completed)
            {
                return;
            }

            Finish = TutorialFinish.Skipped;
            Step = TutorialStep.Completed;
        }

        /// <summary>
        /// 행인이 다가와 말을 걸어도 되는지. 길을 묻는 행인은 튜토리얼 중에는 이웃 만나기·길 안내 때만, 튜토리얼을 마치면 평소처럼
        /// 다가온다. 미션 NPC는 미션 대화 안내부터 다가온다. 다른 NPC는 튜토리얼과 관계없이 다가온다.
        /// </summary>
        public bool IsApproachAllowed(string npcCode)
        {
            if (npcCode == tutorialNpcCode)
            {
                return !IsGuiding
                    || Step == TutorialStep.NeighborApproach
                    || Step == TutorialStep.NeighborDialogue;
            }

            if (npcCode == missionNpcCode)
            {
                return Step >= TutorialStep.MissionTalk;
            }

            return true;
        }
    }
}
