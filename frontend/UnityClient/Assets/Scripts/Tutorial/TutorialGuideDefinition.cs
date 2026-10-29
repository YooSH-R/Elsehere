using System.Collections.Generic;
using UnityEngine;

namespace A305.Tutorial
{
    /// <summary>
    /// 튜토리얼 안내 문구와 튜토리얼이 기준으로 삼는 씬·NPC 키. 안내 문구가 없는 단계(길 안내 대화 중, 완료 뒤)는 카드를 숨긴다.
    /// 필드 초기값은 에셋을 처음 만들 때 들어가는 와이어프레임 문구이며, 이후 수정은 에셋에서 한다.
    /// </summary>
    [CreateAssetMenu(menuName = "A305/Tutorial/Guide Definition", fileName = "TutorialGuide")]
    public class TutorialGuideDefinition : ScriptableObject
    {
        [SerializeField]
        [Tooltip("튜토리얼을 시작하는 방 씬. 다른 공간 씬에 들어가면 방 안내를 마친 것으로 본다.")]
        private string homeSceneName = "HomeScene";

        [SerializeField]
        [Tooltip("길을 묻는 튜토리얼 행인(ERD npcs.code)")]
        private string tutorialNpcCode = "TUTORIAL_PASSERBY";

        [SerializeField]
        [Tooltip("미션 안내에서 대화하러 가는 NPC(ERD npcs.code)")]
        private string missionNpcCode = "CAFE_STAFF";

        [SerializeField]
        private List<TutorialStepGuide> stepGuides = new List<TutorialStepGuide>
        {
            new TutorialStepGuide(
                TutorialStep.Welcome,
                "첫걸음 가이드 1/4 · 우리 집에 오신 걸 환영해요",
                "이 작은 마을에서 길잡이가 되어 줄게요. 사물을 발견하고, 이웃과 이야기하며 영어를 일상으로 만들어 봐요.",
                "",
                "함께 시작하기"
            ),
            new TutorialStepGuide(
                TutorialStep.Move,
                "첫걸음 가이드 2/4 · 자유롭게 이동하기",
                "WASD로 걷고 Shift로 달려요. 마우스 왼쪽 버튼을 누른 채 움직이면 둘러볼 수 있어요.",
                "WASD로 한 번 걸어 보세요.",
                "이해했어요"
            ),
            new TutorialStepGuide(
                TutorialStep.Hover,
                "첫걸음 가이드 3/4 · 익숙한 사물, 새로운 영어",
                "가구에 마우스를 올리면 영어 이름과 뜻이 나와요. 대화나 피드백에서 [표현 저장]을 누르면 오른쪽 위 단어장에 모여요.",
                "멈춘 채 가구에 마우스를 올려 보세요.",
                "다음으로"
            ),
            new TutorialStepGuide(
                TutorialStep.MissionIntro,
                "첫걸음 가이드 4/4 · 이제, 이웃을 만나러 나가요",
                "밖에 나가면 길을 묻는 이웃이 다가와요. 화면에 나오는 대사를 읽으며 길을 알려 주고 대화 화면을 익혀요.",
                "방 남쪽 문으로 나가 보세요.",
                ""
            ),
            new TutorialStepGuide(
                TutorialStep.NeighborApproach,
                "이웃 만나기",
                "이웃이 길을 물으러 다가와요. 말을 걸어오면 [대화하기]를 눌러요.",
                "",
                ""
            ),
            new TutorialStepGuide(
                TutorialStep.NeighborFeedback,
                "첫 번째 대화, 해냈어요!",
                "피드백에서 다음 대화에 쓸 표현을 확인하고 [표현 저장]으로 단어장에 모아 보세요.",
                "다 봤으면 피드백의 [닫기]를 눌러요.",
                ""
            ),
            new TutorialStepGuide(
                TutorialStep.MissionWindow,
                "미션 안내 1/3 · 미션 창을 먼저 살펴볼까요?",
                "왼쪽 위 미션 창에 영어로 연습할 작은 목표가 보여요. 미션은 한 번에 하나씩 진행해요.",
                "",
                "미션 창 확인하기"
            ),
            new TutorialStepGuide(
                TutorialStep.MissionSelect,
                "미션 안내 2/3 · 마음이 가는 미션을 골라 보세요",
                "미션 창에서 [미션 시작]을 눌러 하나를 골라요.",
                "",
                ""
            ),
            new TutorialStepGuide(
                TutorialStep.MissionTalk,
                "미션 안내 3/3 · NPC와 대화해 보세요",
                "마을의 CAFE 건물로 들어가 카운터의 직원에게 다가가면 말을 걸어와요. 미션 창의 '말할 단어'를 넣어 말해 보세요.",
                "",
                ""
            ),
        };

        public string HomeSceneName => homeSceneName;
        public string TutorialNpcCode => tutorialNpcCode;
        public string MissionNpcCode => missionNpcCode;

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
