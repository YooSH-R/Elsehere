namespace A305.Tutorial
{
    /// <summary>
    /// 튜토리얼 진행. 버튼·걷기·시점·단어 저장·단어장·공간 이동·대화·피드백·지도 신호를 받아 단계를 넘긴다.
    /// 지금 단계와 관계없는 신호는 무시한다.
    /// </summary>
    public sealed class TutorialProgress
    {
        private readonly string tutorialNpcCode;
        private bool hasWalked;
        private bool hasLooked;
        private bool isFinishSaved;

        /// <param name="tutorialNpcCode">길을 묻는 튜토리얼 행인의 NPC 키(ERD npcs.code)</param>
        public TutorialProgress(string tutorialNpcCode)
        {
            this.tutorialNpcCode = tutorialNpcCode;
        }

        public TutorialStep Step { get; private set; } = TutorialStep.Arrival;

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
        public bool CanContinue => false;

        /// <summary>미션(첫날 여정)을 보이고 시작할 수 있는지.</summary>
        public bool IsMissionAvailable => false;

        /// <summary>문·[홈]으로 다른 장소에 갈 수 있는지.</summary>
        public bool IsTravelAllowed => false;

        /// <summary>
        /// 튜토리얼 중인지. 그동안 길을 묻는 행인(Ben)은 집 앞에서 튜토리얼 대본대로만 대화하고 생활하지 않는다(튜토리얼 모드).
        /// </summary>
        public bool IsGuiding => Step != TutorialStep.Completed;

        public void Continue() { }

        public void ReportWalked() { }

        public void ReportLooked() { }

        public void ReportWordSaved() { }

        public void ReportVocabularyOpened() { }

        public void LeaveHome() { }

        public void StartDialogue(string npcCode) { }

        public void EndDialogue(string npcCode, bool isGuidedCompleted) { }

        public void CloseFeedback(string npcCode) { }

        public void ReportJournalOpened() { }

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

        public bool IsApproachAllowed(string npcCode) => false;
    }
}
