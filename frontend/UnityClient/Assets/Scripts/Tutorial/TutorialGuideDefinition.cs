using System.Collections.Generic;
using UnityEngine;

namespace A305.Tutorial
{
    /// <summary>
    /// 튜토리얼 안내 문구와 튜토리얼이 기준으로 삼는 씬·NPC 키. 안내 문구가 없는 단계(길 안내 대화 중, 완료 뒤)는 카드를 숨긴다.
    /// 카드가 숨는 대화 제안·대화·단어장·피드백 화면에서는 밝힌 곳 옆 말풍선(코치 마크 설명)으로 할 일을 알린다.
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
                "{WASD} 걷기 · {Shift} 뛰기 · {스페이스} 점프",
                "왼쪽 아래 {조이스틱}을 밀어 걷기",
                ""
            ),
            new TutorialStepGuide(
                TutorialStep.Look,
                "둘러보기",
                "{마우스 왼쪽} 누른 채 끌기 · {휠} 줌 · {V} 시점",
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
                "길을 헤매는 Ben이 다가오면 {대화하기}",
                "",
                ""
            ),
            new TutorialStepGuide(
                TutorialStep.BenFeedback,
                "피드백",
                "피드백을 읽고 {X}로 닫기",
                "",
                ""
            ),
            new TutorialStepGuide(
                TutorialStep.TodayGoal,
                "오늘의 목표",
                "왼쪽 위 {퀘스트 카드}를 눌러 오늘의 목표 보기",
                "",
                ""
            ),
        };

        [SerializeField]
        [Tooltip("Ben이 말을 걸어와 대화 제안이 뜬 동안 [대화하기] 옆에 보이는 설명")]
        private string talkOfferCaption = "{대화하기}를 눌러 Ben과 이야기해요";

        [SerializeField]
        [Tooltip("길 안내 대화를 시작하면 대사 칸을 밝히며 [다음]과 함께 보이는 첫 티칭")]
        private string dialogueLinesCaption =
            "Ben이 먼저 말을 걸었어요. 주고받은 대화는 여기에 쌓여요";

        [SerializeField]
        [Tooltip("대화 시작 티칭의 둘째. [뜻]·[예시 문장]을 밝히며 보인다.")]
        private string dialogueToolsCaption =
            "Ben의 말이 어려우면 {뜻}으로 한국어 뜻을 봐요. {예시 문장}은 할 말을 입력칸에 채워 줘요";

        [SerializeField]
        [Tooltip("대화 시작 티칭의 셋째. 왼쪽 [단어] 카드를 밝히며 보인다.")]
        private string suggestedWordsCaption = "{단어}에는 대답에 쓸 만한 추천 단어가 나와요";

        [SerializeField]
        [Tooltip("대화 시작 티칭의 넷째. 왼쪽 [문장] 카드를 밝히며 보인다.")]
        private string suggestedSentenceCaption =
            "{문장}은 지금 하면 좋은 추천 문장이에요. {넣기}를 누르면 입력칸에 채워져요";

        [SerializeField]
        [Tooltip(
            "대화 시작 티칭을 다 본 뒤 입력칸·[말하기] 옆에 보이는 설명. 아래에 지금 할 대사와 뜻이 붙는다."
        )]
        private string guidedLineCaption =
            "튜토리얼은 정해진 흐름대로 진행돼요. 이 대사를 {말하기}로 말하거나 입력하고 {보내기}";

        [SerializeField]
        [Tooltip(
            "길 안내 대화에서 대사와 다른 문장을 보냈을 때의 설명. 아래에 입력할 대사와 뜻이 붙는다."
        )]
        private string guidedLineMissCaption =
            "튜토리얼은 정해진 흐름대로 진행돼요. 이 대사와 같게 입력하거나 {예시 문장}으로 채워요";

        [SerializeField]
        [Tooltip(
            "길 안내를 마친 뒤 [대화 종료]를 밝히며 옆에 보이는 설명. 누르면 무엇이 이어지는지 알린다."
        )]
        private string dialogueEndCaption =
            "길 안내를 마쳤어요! {대화 종료}를 누르면 Ben과 나눈 대화를 돌아보는 피드백을 볼 수 있어요";

        [SerializeField]
        [Tooltip("대화 노트(피드백)를 열면 탭 줄을 밝히며 [다음]과 함께 보이는 첫 티칭")]
        private string feedbackSectionsCaption =
            "Ben과 나눈 대화를 돌아보는 대화 노트예요. 탭을 눌러 {요약}·{문장}·{회화}·{표현}을 봐요";

        [SerializeField]
        [Tooltip("대화 노트 티칭의 둘째. 쪽 넘김 화살표를 밝히며 보인다.")]
        private string feedbackPagesCaption = "내용이 길면 아래 화살표로 다음 장을 넘겨 봐요";

        [SerializeField]
        [Tooltip("대화 노트 티칭 끝에 오른쪽 위 X를 밝히며 보이는 설명")]
        private string feedbackCloseCaption =
            "다 봤으면 오른쪽 위 {X}로 닫아요. 이어서 오늘의 목표를 알려 드려요";

        [SerializeField]
        [Tooltip(
            "단어장을 처음 열어 안내 카드가 숨은 동안 단어장 창 옆에 보이는 단어장 설명과 다음 할 일"
        )]
        private string vocabularyCaption =
            "단어장에는 사물 카드에서 {저장}한 단어가 모여요. 뜻·예문을 다시 보고 {외웠어요}·메모로 정리해요\n다 봤으면 오른쪽 위 {X}로 닫고 밖으로 나가요";

        [SerializeField]
        [Tooltip(
            "튜토리얼 단계를 마치기 전에 문·[홈]으로 다른 장소에 가려 할 때 안내 카드의 할 일 아래 보이는 안내. 카드 한 줄에 들어가게 짧게 쓴다"
        )]
        private string travelLockedMessage = "아직 다른 곳으로 갈 수 없어요. 할 일부터 해 주세요.";

        public string HomeSceneName => homeSceneName;
        public string TutorialNpcCode => tutorialNpcCode;
        public string TutorialScriptKey => tutorialScriptKey;
        public string TalkOfferCaption => talkOfferCaption;
        public string GuidedLineCaption => guidedLineCaption;
        public string FeedbackSectionsCaption => feedbackSectionsCaption;
        public string FeedbackPagesCaption => feedbackPagesCaption;
        public string FeedbackCloseCaption => feedbackCloseCaption;
        public string DialogueLinesCaption => dialogueLinesCaption;
        public string DialogueToolsCaption => dialogueToolsCaption;
        public string SuggestedSentenceCaption => suggestedSentenceCaption;
        public string SuggestedWordsCaption => suggestedWordsCaption;
        public string GuidedLineMissCaption => guidedLineMissCaption;
        public string DialogueEndCaption => dialogueEndCaption;
        public string TravelLockedMessage => travelLockedMessage;
        public string VocabularyCaption => vocabularyCaption;

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

        /// <returns>
        /// 그 단계나 그보다 앞선 단계 중 가장 가까운 안내가 있으면 true. 대화처럼 안내 카드가 없는 단계에서 [건너뛰기]만 남긴 카드의 이름으로 쓴다.
        /// </returns>
        public bool TryGetNearestStepGuide(TutorialStep step, out TutorialStepGuide guide)
        {
            guide = null;
            foreach (TutorialStepGuide candidate in stepGuides)
            {
                if (candidate.Step <= step && (guide == null || candidate.Step > guide.Step))
                {
                    guide = candidate;
                }
            }

            return guide != null;
        }
    }
}
