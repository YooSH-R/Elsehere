using System.Collections.Generic;

namespace A305.Tutorial
{
    /// <summary>
    /// 튜토리얼 진행. 버튼·조작(걷기·달리기·시점)·단어 저장·단어장·공간 이동·대화·피드백·지도 신호를 받아 단계를 넘긴다.
    /// 걷기(걷기·달리기·점프)·둘러보기는 안내한 조작을 모두 해야 넘어간다. 지금 단계와 관계없는 신호는 무시한다.
    /// </summary>
    public sealed class TutorialProgress
    {
        private static readonly TutorialAction[] KeyboardWalk =
        {
            TutorialAction.Walk,
            TutorialAction.Run,
            TutorialAction.Jump,
        };
        private static readonly TutorialAction[] TouchWalk = { TutorialAction.Walk };
        private static readonly TutorialAction[] KeyboardLook =
        {
            TutorialAction.Rotate,
            TutorialAction.Zoom,
            TutorialAction.ToggleView,
        };
        private static readonly TutorialAction[] TouchLook = { TutorialAction.Rotate };
        private static readonly TutorialAction[] NoActions = new TutorialAction[0];

        private readonly string tutorialNpcCode;
        private readonly HashSet<TutorialAction> doneActions = new HashSet<TutorialAction>();
        private bool isFinishSaved;

        public bool NeedsBenDialogueSave { get; private set; }

        public void MarkBenDialogueSaved() => NeedsBenDialogueSave = false;

        /// <param name="tutorialNpcCode">길을 묻는 튜토리얼 행인의 NPC 키(ERD npcs.code)</param>
        public TutorialProgress(string tutorialNpcCode)
        {
            this.tutorialNpcCode = tutorialNpcCode;
        }

        public TutorialStep Step { get; private set; } = TutorialStep.Arrival;

        /// <summary>터치 조작판이 보이는 기기인지. 터치 기기에는 달리기·점프·휠·V가 없어 해야 할 조작이 줄어든다.</summary>
        public bool UsesTouch { get; set; }

        /// <summary>그 단계에서 해야 할 조작. 안내 문구의 " · "로 나뉜 할 일과 순서가 같다. 조작 단계가 아니면 비어 있다.</summary>
        public static IReadOnlyList<TutorialAction> RequiredActions(TutorialStep step, bool isTouch)
        {
            switch (step)
            {
                case TutorialStep.Walk:
                    return isTouch ? TouchWalk : KeyboardWalk;
                case TutorialStep.Look:
                    return isTouch ? TouchLook : KeyboardLook;
                default:
                    return NoActions;
            }
        }

        /// <summary>조작을 했음을 알린다. 지금 단계에서 해야 할 조작만 센다.</summary>
        public void Report(TutorialAction action)
        {
            if (Contains(RequiredActions(Step, UsesTouch), action))
            {
                doneActions.Add(action);
            }
        }

        /// <summary>지금 단계에서 그 조작을 했는지</summary>
        public bool IsDone(TutorialAction action) => doneActions.Contains(action);

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
            NeedsBenDialogueSave = false;
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
                    case TutorialStep.Arrival:
                        return true;
                    case TutorialStep.Walk:
                    case TutorialStep.Look:
                        return AreAllDone(RequiredActions(Step, UsesTouch));
                    default:
                        return false;
                }
            }
        }

        /// <summary>미션(첫날 여정)을 보이고 시작할 수 있는지. 오늘의 목표 안내부터 보이고, 튜토리얼을 건너뛰어도 보인다.</summary>
        public bool IsMissionAvailable => Step >= TutorialStep.TodayGoal;

        /// <summary>
        /// 문·[홈]으로 다른 장소에 갈 수 있는지. 숙소 안내를 마쳐야 숙소 문이 열리고, 밖에서는 튜토리얼을 마칠 때까지 다른 장소로 가지 않는다.
        /// </summary>
        public bool IsTravelAllowed =>
            Step == TutorialStep.LeaveLodging || Step == TutorialStep.Completed;

        /// <summary>
        /// 튜토리얼 중인지. 그동안 길을 묻는 행인(Ben)은 집 앞에서 튜토리얼 대본대로만 대화하고 생활하지 않는다(튜토리얼 모드).
        /// </summary>
        public bool IsGuiding => Step != TutorialStep.Completed;

        /// <summary>
        /// 장소 소리를 대화 중처럼 낮춰 둘지. Ben 대화에서 낮춘 소리가 피드백·휴대폰·오늘의 목표 안내 도중에 다시 커지지 않게
        /// 튜토리얼을 마칠 때까지 낮춰 둔다. 건너뛰었으면 남은 퀘스트 카드 안내 동안 평소대로 돌린다.
        /// </summary>
        public bool KeepsAmbienceQuiet =>
            Step >= TutorialStep.BenDialogue
            && Step < TutorialStep.Completed
            && Finish != TutorialFinish.Skipped;

        public void Continue()
        {
            if (!CanContinue)
            {
                return;
            }

            Step = Step switch
            {
                TutorialStep.Arrival => TutorialStep.Walk,
                TutorialStep.Walk => TutorialStep.Look,
                TutorialStep.Look => TutorialStep.SaveWord,
                _ => Step,
            };
            doneActions.Clear();
        }

        /// <summary>고정한 사물 카드에서 [저장]을 눌렀음을 알린다.</summary>
        public void ReportWordSaved()
        {
            if (Step == TutorialStep.SaveWord)
            {
                Step = TutorialStep.OpenVocabulary;
            }
        }

        public void ReportVocabularyOpened()
        {
            if (Step == TutorialStep.OpenVocabulary)
            {
                Step = TutorialStep.LeaveLodging;
            }
        }

        /// <summary>숙소 밖 공간에 들어왔음을 알린다. 숙소 안내 중이면(다른 장소에서 시작한 경우) Ben 만나기로 넘어간다.</summary>
        public void LeaveHome()
        {
            if (Step <= TutorialStep.LeaveLodging)
            {
                Step = TutorialStep.MeetBen;
            }
        }

        public void StartDialogue(string npcCode)
        {
            if (Step == TutorialStep.MeetBen && npcCode == tutorialNpcCode)
            {
                Step = TutorialStep.BenDialogue;
            }
        }

        /// <summary>
        /// 길 안내를 끝까지 마치면 피드백 단계로 넘어가고, 중간에 끝내면 Ben이 다시 다가오도록 Ben 만나기로 되돌린다.
        /// </summary>
        public void EndDialogue(string npcCode, bool isGuidedCompleted)
        {
            if (Step != TutorialStep.BenDialogue || npcCode != tutorialNpcCode)
            {
                return;
            }

            Step = isGuidedCompleted ? TutorialStep.BenFeedback : TutorialStep.MeetBen;
            if (isGuidedCompleted)
            {
                NeedsBenDialogueSave = true;
            }
        }

        public void CloseFeedback(string npcCode)
        {
            if (Step == TutorialStep.BenFeedback && npcCode == tutorialNpcCode)
            {
                Step = TutorialStep.TodayGoal;
            }
        }

        /// <summary>
        /// 퀘스트 카드를 눌러 수첩을 열었음을 알린다. 오늘의 목표 단계에서 열면 튜토리얼을 마친다.
        /// 건너뛰어 이 안내만 남았으면 건너뛰기로 끝낸 기록을 그대로 둔다.
        /// </summary>
        public void ReportJournalOpened()
        {
            if (Step == TutorialStep.TodayGoal)
            {
                Finish ??= TutorialFinish.Completed;
                Step = TutorialStep.Completed;
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

        /// <summary>안내 카드의 [건너뛰기]를 보일지. 첫 퀘스트를 알리는 오늘의 목표 안내는 건너뛸 수 없다.</summary>
        public bool CanSkip => Step < TutorialStep.TodayGoal;

        /// <summary>
        /// 안내 카드의 [건너뛰기]. 첫 퀘스트는 꼭 해야 하므로 퀘스트 카드를 누르는 오늘의 목표 안내만 남기고 그 앞 안내를 건너뛴다.
        /// 서버에는 바로 건너뛰기로 저장해 첫 퀘스트를 시작하게 한다. 같은 기기에서 이미 끝낸 새 계정처럼 안내 없이 끝낼 때는 <see cref="Skip"/>을 쓴다.
        /// </summary>
        public void SkipToTodayGoal()
        {
            if (!CanSkip)
            {
                return;
            }

            Finish = TutorialFinish.Skipped;
            Step = TutorialStep.TodayGoal;
        }

        /// <summary>
        /// 행인이 다가와 말을 걸어도 되는지. 튜토리얼 동안에는 Ben만 Ben 만나기·길 안내 때 다가오고, 다른 행인은 누구와 말해야 하는지
        /// 헷갈리지 않게 튜토리얼을 마친 뒤에 다가온다.
        /// </summary>
        public bool IsApproachAllowed(string npcCode)
        {
            if (!IsGuiding)
            {
                return true;
            }

            return npcCode == tutorialNpcCode
                && (Step == TutorialStep.MeetBen || Step == TutorialStep.BenDialogue);
        }

        private bool AreAllDone(IReadOnlyList<TutorialAction> actions)
        {
            foreach (TutorialAction action in actions)
            {
                if (!doneActions.Contains(action))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Contains(IReadOnlyList<TutorialAction> actions, TutorialAction action)
        {
            foreach (TutorialAction candidate in actions)
            {
                if (candidate == action)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
