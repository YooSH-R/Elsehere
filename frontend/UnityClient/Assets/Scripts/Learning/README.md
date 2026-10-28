# Learning 모듈

대화가 끝나면 피드백을 보여 주고, 대화·피드백에서 저장한 표현을 단어장에 모읍니다. 짧은 피드백은 요약과 다음 대화에서 활용할 표현, 상세 피드백은 발화별 참고 표현이며 [상세 보기]로 오갑니다. 단어장은 저장한 표현을 목록으로 보이고 '외웠어요' 표시와 삭제를 할 수 있습니다.

이 서비스의 학습 흐름을 다루는 게임 전용 기능입니다. 지금 화면은 흐름 확인용 초안입니다. 서버 AI와 나눈 대화는 서버가 만든 피드백(CONV-08, FB-03)을, 튜토리얼처럼 대본으로 진행한 대화는 키워드 규칙으로 만든 예시를 보여 줍니다. 지난 대화 기록 화면(CONV-09·10)·단어장 필터·사물 카드에서 바로 저장·단어장 서버 저장은 이후 작업에서 붙입니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `VocabularyBook`, `VocabularyEntry` | 저장한 표현. 같은 표현(대소문자·앞뒤 공백 무시)은 새로 만들지 않고 저장 횟수만 늘립니다. Unity 객체 없이 테스트합니다. |
| `ConversationFeedback`, `UtteranceReview`, `ReferenceExpression`, `ReferenceRule` | 요약·상세 피드백. 서버 피드백은 요약·잘했어요·아쉬워요·개선 표현과 발화별 더 나은 표현·이유·회화 표현을 담고, 대본 대화는 발화마다 키워드 규칙의 참고 표현을 찾습니다. Unity 객체 없이 테스트합니다. |
| `FeedbackText` | 피드백 화면 본문 문구. 서버 피드백은 AI가 쓴 이유와 회화 표현을 그대로 보여 주고, "문법 오류를 매기지 않는다"는 안내는 키워드 규칙 예시에만 붙입니다. [대화 기록 보기]의 대화 기록도 만듭니다. Unity 객체 없이 테스트합니다. |
| `FeedbackExampleSource`, `MockFeedbackExampleSource` | 참고 표현 규칙을 찾는 곳과 서버 연동 전 확인용 규칙입니다. |
| `LearningRecord` | 공간 씬이 바뀌어도 단어장이 남도록 씬 밖에 둡니다. Play를 시작할 때마다 비웁니다. |
| `LearningController` | 대화가 끝나면 피드백을 열고, 저장 요청을 단어장에 모으고, 화면이 열려 있는 동안 이동을 잠급니다. |
| `FeedbackView`, `VocabularyView`, `LearningHudView` | 피드백 화면, 단어장 화면, 오른쪽 위 [단어장] 버튼과 저장 알림입니다. |

## 의존성

| 대상 | 용도 |
| --- | --- |
| `A305.Dialogue` | `DialogueController.ConversationEnded`·`ExpressionSaveRequested`·`IsTalking`, `DialogueRecord`, `ServerFeedback`, `KeywordMatcher` |
| `A305.Player` | `PlayerMover.SetMovementLocked` |
| uGUI, TextMeshPro | 피드백·단어장 화면 |

## 동작 규칙

| 상황 | 동작 |
| --- | --- |
| 내 발화가 있는 대화가 끝남 | 짧은 피드백을 열고 이동을 잠급니다. |
| 내 발화 없이 대화가 끝남 | 피드백을 만들지 않고 이동 잠금도 건드리지 않습니다. |
| 대화 중 [표현 저장] | 가장 최근 NPC 대사를 단어장에 저장하고 오른쪽 위에 알립니다. |
| 피드백 [표현 저장] | 다음 대화에서 활용할 표현을 저장합니다. 권할 표현이 없으면 버튼이 꺼집니다. |
| 서버 피드백 [상세 보기] | 고칠 표현이 없는 발화는 고정 문구 대신 AI가 쓴 이유를 보여 줍니다. 회화 표현은 개선 표현과 따로 한 줄로 보여 주며, 고칠 표현이 없는 발화에도 보여 줍니다. |
| 피드백 [닫기] | 피드백을 닫고 `FeedbackClosed`로 NPC 키와 정해진 흐름 완료 여부를 알립니다. |
| 피드백을 받지 못함 | 제목으로 받지 못했음을 알리고, 평가 문구를 지어내지 않습니다. [표현 저장]·[상세 보기] 대신 [대화 기록 보기]로 방금 나눈 대화를 보여 줍니다(기능 명세 F-32). |
| 서버가 피드백을 만들지 못함(`FAILED`) | [다시 만들기]로 다시 만듭니다(FB-02). 기다리는 동안 버튼을 막고, 실패하면 이유를 보여 주며 다시 누를 수 있습니다. 종료 요청 자체가 실패해 서버에 피드백 기록이 없으면 [다시 만들기]를 보이지 않습니다. |
| 피드백을 만들지 않음(`SKIPPED`) | 제목을 "이번 대화는 피드백이 없어요"로 바꿉니다. |
| 대화 중 | [단어장] 버튼이 꺼집니다. 단어장의 이동 잠금이 대화의 이동 잠금을 풀지 않게 하기 위함입니다. |

화면 정렬 순서는 [단어장] 버튼 95, 피드백 130, 단어장 135입니다. 피드백에서 [단어장 보기]를 누르면 단어장이 피드백 위에 열리고, 닫으면 피드백으로 돌아옵니다.

## 다른 기능에서 쓰는 API

| API | 설명 |
| --- | --- |
| `LearningController.FeedbackClosed` | 피드백을 닫으면 `(npcCode, isGuidedCompleted)`를 알립니다. 튜토리얼이 길 안내 피드백을 본 뒤 다음 단계로 넘어갈 때 씁니다. |
| `LearningController.IsShowingScreen` | 피드백이나 단어장이 열려 있는지 |
| `LearningRecord.Vocabulary`, `LearningRecord.Clear()` | 지금 단어장. 테스트에서 상태를 비울 때 `Clear()`를 씁니다. |

## 확인용 참고 표현

`Assets/Data/Learning/MockFeedbackExamples.asset`의 규칙을 위에서부터 확인해 발화에 키워드가 모두 들어 있는 첫 규칙을 씁니다(`soy`+`latte`, `latte`, `coffee`, `dessert`, `straight`, `weather`). 필드 초기값은 에셋을 처음 만들 때 들어가는 예시이며, 이후 수정은 에셋에서 합니다.

## Builder

**Tools → A305 → Learning → Build**(`Scripts/Editor/Learning/LearningBuilder.cs`)가 `Prefabs/Learning/`의 `FeedbackScreen`·`VocabularyScreen`·`LearningHud` 프리팹과 확인용 규칙을 준비합니다. 화면은 없는 요소만 만들고 이미 있는 요소의 디자인은 유지합니다. 공간 씬에 붙이는 일은 [World 모듈의 공간 기능 연결](../World/README.md#새-공간-씬-만들기)이 합니다.

## 테스트

| 어셈블리 | 위치 | 검증 |
| --- | --- | --- |
| `A305.Learning.Tests` | `Assets/Tests/EditMode/Learning/` | 새 표현 저장, 같은 표현 다시 저장, 빈 표현 무시, 삭제, 외웠어요 전환. 발화별 참고 표현, 참고 표현 없는 발화, 다음 대화 표현, 요약. 서버 피드백 변환(실패·건너뜀 포함)과 상태별 제목·다시 만들기 가능 여부, 상세 화면 문구(고칠 표현 없는 발화의 AI 이유, 회화 표현, 안내 문구), 대화 기록 문구 |
| `A305.World.Tests` | `Assets/Tests/EditMode/World/` | 공간 기능 연결이 피드백·단어장 화면과 학습 처리를 한 번씩 붙이고 대화·플레이어와 연결하는지 |

실행 방법은 [프론트엔드 개발 지침](../../../../AGENTS.md)을 따릅니다.
