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
        [Tooltip("튜토리얼을 시작하는 숙소 씬. 다른 공간 씬에 들어가면 숙소 안내를 마친 것으로 본다.")]
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
                "여행 첫날 · 숙소에 도착했어요",
                "오늘 이 마을에 막 도착했어요. 밖으로 나가기 전에 숙소에서 움직이는 법과 영어 단어를 모으는 법을 익혀 봐요.",
                "",
                "시작하기"
            ),
            new TutorialStepGuide(
                TutorialStep.Walk,
                "숙소 둘러보기 1/5 · 걸어 보기",
                "WASD나 방향키로 걷고, Shift를 누른 채 걸으면 달려요. 휴대폰에서는 왼쪽 아래 조이스틱을 밀어요.",
                "숙소 안을 걸어 보세요.",
                "다음으로"
            ),
            new TutorialStepGuide(
                TutorialStep.Look,
                "숙소 둘러보기 2/5 · 둘러보기",
                "마우스 왼쪽 버튼을 누른 채 끌면 주위를 둘러봐요. 휠로 가까이·멀리 보고, V로 1인칭과 3인칭을 바꿔요. 휴대폰에서는 화면을 손가락으로 끌어요.",
                "마우스를 끌어 주위를 둘러보세요.",
                "다음으로"
            ),
            new TutorialStepGuide(
                TutorialStep.SaveWord,
                "숙소 둘러보기 3/5 · 사물 단어 모으기",
                "멈춰 서서 가구에 마우스를 올리면 영어 단어 카드가 떠요. 가구를 클릭하면(휴대폰은 누르면) 카드가 고정되고 예문·[발음 듣기]·[저장]을 쓸 수 있어요.",
                "가구를 클릭해 카드를 고정하고 [저장]을 눌러 보세요.",
                ""
            ),
            new TutorialStepGuide(
                TutorialStep.OpenVocabulary,
                "숙소 둘러보기 4/5 · 단어장 열기",
                "저장한 단어는 단어장에 모여요. 오른쪽 위 메뉴(≡)에는 [단어장], 지난 대화를 다시 보는 [피드백 목록], 숙소로 돌아오는 [홈]이 있어요.",
                "≡를 누르고 [단어장]을 열어 보세요.",
                ""
            ),
            new TutorialStepGuide(
                TutorialStep.LeaveLodging,
                "숙소 둘러보기 5/5 · 이제 밖으로",
                "숙소 문이 열렸어요. 문 앞에서 누군가 길을 헤매고 있는 것 같아요.",
                "표시된 문으로 나가 보세요.",
                ""
            ),
            new TutorialStepGuide(
                TutorialStep.MeetBen,
                "Ben 만나기",
                "머리 위에 표시가 있는 사람이 Ben이에요. Ben도 이 마을에 막 와서 카페를 찾고 있어요. 가까이 가면 말을 걸어와요.",
                "Ben에게 다가가 [대화하기]를 눌러요.",
                ""
            ),
            new TutorialStepGuide(
                TutorialStep.BenFeedback,
                "첫 대화, 해냈어요!",
                "피드백에서 잘한 점과 더 자연스러운 표현을 확인해요. 지난 피드백은 ≡ 메뉴의 [피드백 목록]에서 다시 볼 수 있어요.",
                "다 읽었으면 피드백의 [닫기]를 눌러요.",
                ""
            ),
            new TutorialStepGuide(
                TutorialStep.TodayGoal,
                "오늘의 목표 확인하기",
                "왼쪽 위 지도 버튼을 누르면 마을 지도와 오늘의 여정이 담긴 수첩이 열려요. 멀리 갈 땐 길가에서 [택시 세우기]를, 쉬고 싶으면 벤치에서 [잠깐 앉기]를 눌러요.",
                "지도 버튼을 눌러 오늘의 목표를 확인해 보세요.",
                ""
            ),
        };

        [SerializeField]
        [TextArea]
        [Tooltip("Ben이 말을 걸어와 대화 제안이 뜬 동안 [대화하기] 옆에 보이는 설명")]
        private string talkOfferCaption = "Ben이 말을 걸어왔어요. [대화하기]를 눌러 이야기를 시작해요.";

        [SerializeField]
        [TextArea]
        [Tooltip("길 안내 대화 중 읽을 대사와 입력칸 옆에 보이는 설명")]
        private string guidedLineCaption =
            "튜토리얼 대화는 정해진 대사로 연습해요. 위에 나온 문장을 그대로 입력하고 [보내기]를 눌러요. 마을 대화부터는 자유롭게 말해요.";

        [SerializeField]
        [TextArea]
        [Tooltip("길 안내를 마친 뒤 [대화 종료] 옆에 보이는 설명")]
        private string dialogueEndCaption = "길 안내를 마쳤어요. [대화 종료]를 누르면 피드백을 볼 수 있어요.";

        [SerializeField]
        [Tooltip("튜토리얼 단계를 마치기 전에 문·[홈]으로 다른 장소에 가려 할 때 보이는 안내")]
        private string travelLockedMessage = "아직 다른 곳으로 갈 수 없어요. 안내 카드의 할 일을 먼저 해 보세요.";

        public string HomeSceneName => homeSceneName;
        public string TutorialNpcCode => tutorialNpcCode;
        public string TutorialScriptKey => tutorialScriptKey;
        public string TalkOfferCaption => talkOfferCaption;
        public string GuidedLineCaption => guidedLineCaption;
        public string DialogueEndCaption => dialogueEndCaption;
        public string TravelLockedMessage => travelLockedMessage;

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
