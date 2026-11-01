using System.Collections.Generic;
using UnityEngine;

namespace A305.Tutorial
{
    /// <summary>
    /// 튜토리얼 안내 문구와 튜토리얼이 기준으로 삼는 씬·NPC 키. 안내 문구가 없는 단계(길 안내 대화 중, 완료 뒤)는 카드를 숨긴다.
    /// 카드가 숨는 대화 제안·대화 화면에서는 코치 마크 설명으로 할 일을 알린다.
    /// 필드 초기값은 에셋을 처음 만들 때 들어가는 문구이며, 이후 수정은 에셋에서 한다.
    /// </summary>
    [CreateAssetMenu(menuName = "A305/Tutorial/Guide Definition", fileName = "TutorialGuide")]
    public class TutorialGuideDefinition : ScriptableObject
    {
        [SerializeField]
        [Tooltip(
            "튜토리얼을 시작하는 숙소 씬. 다른 공간 씬에 들어가면 숙소 안내를 마친 것으로 본다."
        )]
        private string homeSceneName = "HomeScene";

        [SerializeField]
        [Tooltip(
            "튜토리얼에서 길을 묻는 행인(ERD npcs.code). 마을 행인 Ben이 튜토리얼 동안 이 역할을 한다."
        )]
        private string tutorialNpcCode = "TOWN_PASSERBY";

        [SerializeField]
        [Tooltip(
            "튜토리얼 동안 그 행인이 쓰는 길 안내 대본·인사말의 키. 튜토리얼을 마치면 NPC 키로 평소 대화한다."
        )]
        private string tutorialScriptKey = "TUTORIAL_PASSERBY";

        [SerializeField]
        private List<TutorialStepGuide> stepGuides = new List<TutorialStepGuide>
        {
            new TutorialStepGuide(
                TutorialStep.Arrival,
                "여행 첫날",
                "숙소에서 기본 조작부터 익혀 볼까요?",
                "",
                "시작하기"
            ),
            new TutorialStepGuide(
                TutorialStep.Walk,
                "걷기",
                "{W}{A}{S}{D} 걷기 · {Shift} 달리기",
                "왼쪽 아래 {조이스틱}을 밀어 걷기",
                ""
            ),
            new TutorialStepGuide(
                TutorialStep.Look,
                "둘러보기",
                "{마우스 왼쪽} 누른 채 끌기 · {휠} 확대 · {V} 시점",
                "화면 오른쪽을 끌어 둘러보기",
                ""
            ),
            new TutorialStepGuide(
                TutorialStep.SaveWord,
                "단어 모으기",
                "가구를 클릭해 카드 고정 → {저장}",
                "가구를 눌러 카드 고정 → {저장}",
                ""
            ),
            new TutorialStepGuide(
                TutorialStep.OpenVocabulary,
                "단어장",
                "{≡} 메뉴에서 {단어장} 열기",
                "",
                ""
            ),
            new TutorialStepGuide(
                TutorialStep.LeaveLodging,
                "밖으로",
                "화살표가 가리키는 문으로 나가기",
                "",
                ""
            ),
            new TutorialStepGuide(
                TutorialStep.MeetBen,
                "Ben 만나기",
                "길을 헤매는 Ben에게 다가가 {대화하기}",
                "",
                ""
            ),
            new TutorialStepGuide(
                TutorialStep.BenFeedback,
                "피드백",
                "피드백을 읽고 {닫기}",
                "",
                ""
            ),
            new TutorialStepGuide(
                TutorialStep.TodayGoal,
                "오늘의 목표",
                "{지도} 버튼으로 오늘의 목표 보기",
                "",
                ""
            ),
        };

        [SerializeField]
        [Tooltip("Ben이 말을 걸어와 대화 제안이 뜬 동안 화면 위쪽에 보이는 설명")]
        private string talkOfferCaption = "{대화하기}를 눌러 Ben과 이야기해요";

        [SerializeField]
        [Tooltip("길 안내 대화 중 입력칸을 밝힐 때 보이는 설명. 아래에 지금 입력할 대사가 붙는다.")]
        private string guidedLineCaption = "정해진 대사로 연습해요 · 아래 문장을 입력하고 {보내기}";

        [SerializeField]
        [Tooltip("길 안내 대화에서 대사와 다른 문장을 보냈을 때의 설명. 아래에 입력할 대사가 붙는다.")]
        private string guidedLineMissCaption = "문장이 달라요 · {예시 문장}으로 채울 수 있어요";

        [SerializeField]
        [Tooltip("길 안내를 마친 뒤 [대화 종료]를 밝힐 때 화면 위쪽에 보이는 설명")]
        private string dialogueEndCaption = "길 안내 끝! {대화 종료}를 눌러요";

        [SerializeField]
        [Tooltip("튜토리얼 단계를 마치기 전에 문·[홈]으로 다른 장소에 가려 할 때 보이는 안내")]
        private string travelLockedMessage =
            "아직 다른 곳으로 갈 수 없어요. 안내 카드의 할 일을 먼저 해 보세요.";

        public string HomeSceneName => homeSceneName;
        public string TutorialNpcCode => tutorialNpcCode;
        public string TutorialScriptKey => tutorialScriptKey;
        public string TalkOfferCaption => talkOfferCaption;
        public string GuidedLineCaption => guidedLineCaption;
        public string GuidedLineMissCaption => guidedLineMissCaption;
        public string DialogueEndCaption => dialogueEndCaption;
        public string TravelLockedMessage => travelLockedMessage;

        /// <summary>안내 카드가 있는 단계 수. 진행 점의 개수다.</summary>
        public int StepGuideCount => stepGuides.Count;

        /// <returns>그 단계가 안내 카드 중 몇 번째인지(1부터). 안내 카드가 없는 단계면 0.</returns>
        public int StepGuideNumber(TutorialStep step)
        {
            int index = stepGuides.FindIndex(candidate => candidate.Step == step);
            return index + 1;
        }

        /// <returns>그 단계의 안내 문구가 있으면 true. 없으면 안내 카드를 숨긴다.</returns>
        public bool TryGetStepGuide(TutorialStep step, out TutorialStepGuide guide)
        {
            foreach (TutorialStepGuide candidate in stepGuides)
            {
                if (candidate.Step == step)
                {
                    guide = candidate;
                    return true;
                }
            }

            guide = null;
            return false;
        }
    }
}
