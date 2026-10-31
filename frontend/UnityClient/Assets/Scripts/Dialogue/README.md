# Dialogue 모듈

대화 화면은 아래쪽 대화창과 왼쪽 단어·문장 카드를 따로 표시합니다. 대화창은 NPC 이름과 최신 대사를 표시합니다. 긴 대사는 줄을 바꾸고, 넓은 화면에서는 대사 줄 수에 맞춰 대화창을 좁은 화면 높이까지 위로 키우며, 그보다 긴 대사·뜻은 스크롤합니다. [넣기]는 추천 문장을 입력창에 옮기며 전송하거나 모델을 호출하지 않습니다. 듣기·뜻·힌트 같은 보조 버튼은 입력창 위 한 줄에 바로 놓고, 대화 종류에 따라 숨는 버튼은 자리를 비우지 않고 당겨 붙입니다. 보내기·누르고 말하기·대화 종료도 바로 사용할 수 있습니다. NPC 얼굴은 대화창보다 위쪽에 오도록 화면 비율을 반영해 카메라 구도를 맞춥니다.

**Tools → A305 → UI → Apply Approved Dialogue Design**은 기존 대화 기능과 입력 연결을 유지하며 승인된 하단 배치·글꼴·여백을 프리팹에 적용합니다. 카페의 카운터와 소품 높이, 직원 방향, 마을 드럭스토어 출입구와 퀘스트 지도도 Unity Builder로 갱신합니다. 외부 AI 호출은 하지 않습니다.

NPC와의 대화를 시작하고 끝냅니다. 대화가 시작되면 NPC를 바라보는 1인칭 시점으로 바꾸고 이동을 잠그며, 끝나면 직전 시점과 이동을 되돌립니다. 대화는 한 번에 하나만 진행합니다.

튜토리얼 길 안내처럼 정해진 흐름이 있는 NPC는 확인용 대본(`MockDialogueScripts`)으로, 그 밖의 NPC는 서버 AI와 자유 대화로 진행합니다. 대화 화면 프리팹의 `DialogueServerLink`에서 서버를 끄면 모든 대화를 확인용 대본으로 진행하며, 이때 자유 대화는 키워드로 답하는 대본이 흉내 냅니다. 지금 화면은 흐름 확인용 초안입니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `DialogueSession` | 대화 중인 NPC 키(ERD `npcs.code`)를 기억하고 대화가 겹쳐 시작되지 않게 막습니다. Unity 객체 없이 테스트합니다. |
| `DialogueConversation` | 대화 한 번의 진행. 사용자가 보낸 문장 뒤에 대본에 따라 NPC 대사를 붙이고, 정해진 흐름의 읽을 대사·완료 여부를 알려 줍니다. Unity 객체 없이 테스트합니다. |
| `DialogueScript`, `GuidedTurn`, `KeywordReply`, `DialogueLine` | 대본과 대사. 정해진 턴이 있으면 정해진 흐름, 없으면 키워드 응답 규칙과 기본 응답으로 답합니다. |
| `DialogueRecord` | 끝난 대화의 기록. 피드백·미션·튜토리얼이 대화 결과를 판단할 때 씁니다. |
| `DialogueScriptSource`, `MockDialogueScriptSource` | NPC 키로 대본을 찾는 곳과 서버 연동 전 확인용 대본입니다. |
| `ServerConversation`, `IConversationApi`, `ConversationApi` | 서버 AI 자유 대화 한 번. 대화 시작(CONV-01)·발화(CONV-03)·녹음 전사(CONV-02)·뜻 보기(CONV-04)·힌트(CONV-05)·종료(CONV-08)를 부릅니다. 가짜 API로 Unity 객체 없이 테스트합니다. |
| `ConversationAllowance`, `IAiStatusApi`, `AiStatusApi` | 새 NPC 대화를 시작할 수 있는지와 게스트의 남은 대화 수(AI-01). 공간에 들어올 때와 대화가 끝날 때 다시 받고, 받지 못하면 직전 상태를 둡니다. 받기 전이나 서버를 끈 때는 막지 않습니다(최종 확인은 CONV-01). 가짜 API로 Unity 객체 없이 테스트합니다. |
| `VoiceInputNotice` | 누르고 말하기 안내 문구. 녹음 중, 버튼을 떼기 전에 녹음이 끝난 이유(최대 시간·말 멈춤·창 이동), 마이크를 쓸 수 없는 이유(권한 거부·장치 없음·미지원)를 나눠 알립니다. 음성 처리에 동의하지 않아 [누르고 말하기]가 없는 대화에서는 다른 상태 안내가 없을 때 입력칸 위 상태 줄에 동의를 켜는 곳(오른쪽 위 메뉴(≡) › 내 정보 › 선택 동의)을 보입니다. |
| `NpcSpeechSlot` | [다시 듣기]로 재생할 NPC 음성. 지금 대사의 음성만 가지며, 새 대사의 음성 합성이 실패하면 지난 대사의 음성을 버려 [다시 듣기]가 음성을 다시 만들게(CONV-07) 합니다. Unity 객체 없이 테스트합니다. |
| `FeedbackRegenerator` | 만들지 못한 짧은 피드백을 다시 만들고(FB-02), 새로 만들면 상세 평가(FB-03)도 받습니다. 피드백 화면의 [다시 만들기]가 씁니다. |
| `DialogueController` | 대화 시작·종료 때 카메라 시점, 이동 잠금, 대화 화면을 바꾸고 대화 진행을 화면과 연결합니다. 대화 중 Esc를 누르면 [대화 종료]와 같이 끝냅니다. |
| `DialoguePanelLayout` | 대사 길이에 맞춘 대화창 높이. Unity 객체 없이 테스트합니다. |
| `DialoguePanelView` | 대화 중 화면. 최근 대사, 이번에 읽을 대사, 추천 단어·추천 문장, 텍스트 입력과 [보내기]·[예시 문장]·[뜻 보기]·[표현 저장]·[대화 종료 (Esc)], 서버 대화의 [누르고 말하기]·[다시 듣기]·[힌트]·[다시 시도]가 있습니다. |

## 서버 대화의 오류와 안내

상태 문구 아래에 사용자가 다음에 할 일을 함께 보여 줍니다(기능 명세 F-41). 서버가 준 오류 문구는 그대로 보여 줍니다.

| 상황 | 안내 |
| --- | --- |
| 연결 실패·시간 초과, 서버·AI 장애(5xx), 요청 속도 제한 | 대화 시작이나 발화가 실패했으면 [다시 시도]를 보여 줍니다. 발화는 같은 문장을 같은 요청 ID로 다시 보냅니다. |
| AI 사용량 소진(`AI_QUOTA_EXHAUSTED`) | 다시 시도하지 않고 [대화 종료]로 탐험에 돌아가라고 안내합니다. 게스트가 대화 횟수를 다 썼으면 서버가 "게스트는 NPC와 3번까지 대화할 수 있어요…"를 보냅니다. 보통은 그 전에 [말 걸기]가 막혀 이 화면까지 오지 않습니다. |
| 대화 중 사용량 소진(`endReason: QUOTA_EXHAUSTED`) | 오류가 아닌 정상 종료지만, NPC가 왜 대화를 마쳤는지와 계속 쓸 수 있는 기능을 알립니다. |
| 무음·인식 실패(`STT_NO_SPEECH`) | 다시 말하거나 텍스트로 입력하라고 안내합니다. |
| NPC 음성 삭제·만료(`EXPIRED`), 음성 정보 없음, 음성 조회의 `404` | [다시 듣기]를 숨기고 자막을 유지합니다. 삭제된 음성은 다시 합성하지 않습니다. |
| 최초 NPC 음성 생성 실패(`FAILED`) | [다시 듣기]에서 음성 생성만 재시도합니다. |
| 저장 음성의 일시적인 다운로드 실패 | [다시 듣기]를 유지하고 같은 음성을 다시 받습니다. 음성 재생성 API를 부르지 않습니다. |
| 녹음 중·녹음 뒤 | 누르는 동안 "듣고 있어요"와 최대 녹음 시간을, 전사 뒤에는 문장을 확인하고 [보내기]를 누르라고 안내합니다. 버튼을 너무 짧게 누르면 누른 채로 말하라고 알립니다. |

## 의존성

| 대상 | 용도 |
| --- | --- |
| `A305.Player` | `PlayerCameraRig.EnterDialogueView`·`ExitDialogueView`, `PlayerMover.SetMovementLocked` |
| uGUI, TextMeshPro | 대화 화면과 텍스트 입력 |

## 다른 기능에서 쓰는 API

| API | 설명 |
| --- | --- |
| `DialogueController.TryStart(string npcCode, Transform lookTarget, ConversationInitiator initiatedBy = Npc)` | 대화를 시작합니다. 이미 대화 중이면 false를 돌려줍니다. 플레이어가 [말 걸기]로 먼저 건 대화는 `ConversationInitiator.User`로 넘기고, 서버 대화 시작(CONV-01)에 `initiatedBy: USER`로 보냅니다. NPC가 제안한 대화는 기본값(`NPC`)입니다. |
| `DialogueController.End()` | 대화를 끝내고 시점과 이동을 되돌립니다. [대화 종료] 버튼과 Esc도 이 동작을 합니다. |
| `DialogueController.RegenerateFeedbackAsync(ServerFeedback)` | 만들지 못한 피드백을 다시 만듭니다. 서버 대화가 아니거나 대화 ID가 없으면 받은 피드백을 그대로 돌려줍니다. |
| `DialogueController.IsTalking` | 대화 중인지 |
| `DialogueController.Allowance` | 새 NPC 대화를 시작할 수 있는지(`CanStartConversation`)와 게스트의 남은 대화 수(`RemainingConversations`). 행인 말걸기와 [말 걸기]가 씁니다. |
| `DialogueController.FindNpcName(string npcCode)` | 대화 대본에 적힌 NPC 이름. 대본이 없으면 null입니다. 행인 제안 화면의 머리말에 씁니다. |
| `DialogueController.Started` | 대화가 시작되면 NPC 키를 알립니다. |
| `DialogueController.Ended` | 대화가 끝나면 끝난 NPC 키를 알립니다. |
| `DialogueController.NpcResponded` | 서버가 NPC 턴을 확정하면 알립니다. 첫날 연계 미션 UI가 서버 진행을 다시 읽을 때 사용합니다. |
| `DialogueController.ConversationEnded` | 대화가 끝나면 `DialogueRecord`를 알립니다. 시점과 이동을 되돌린 뒤 가장 마지막에 알리므로, 받는 쪽이 화면을 열며 이동을 잠가도 풀리지 않습니다. |
| `DialogueController.ExpressionSaveRequested` | [표현 저장]을 누르면 가장 최근 NPC 대사의 영어 문장과 한국어 뜻을 알립니다. 서버 대화는 [뜻 보기]로 뜻을 받기 전이면 뜻이 빈 문자열입니다. 단어장 저장은 받는 쪽(Learning 모듈)이 합니다. |

## 확인용 대본

`Assets/Data/Dialogue/MockDialogueScripts.asset`에 NPC 키별 대본이 있습니다. 목록에 없는 NPC는 `Fallback` 대본으로 대화합니다.

| NPC 키 | 이름 | 대화 |
| --- | --- | --- |
| `TUTORIAL_PASSERBY` | Alex | 카페 가는 길을 묻는 정해진 3턴. 화면에 읽을 대사를 보여 주고, 무엇을 보내도 다음 턴으로 넘어갑니다. 마지막 턴 뒤에는 입력을 받지 않습니다. |
| `CAFE_STAFF` | Mia | 키워드 응답. `soy`+`latte`, `latte`, `dessert`, `coffee`, `thank` 순서로 확인합니다. |
| `TOWN_PASSERBY` | Ben | 키워드 응답. `weather`, `cafe`, `thank` |
| `TAXI_DRIVER` | Joe | 어디로 가는지 묻고 키워드 응답. `hotel`, `thank` |

- 키워드는 대소문자를 구분하지 않고 단어 일부여도 맞습니다(`desserts`도 `dessert`에 맞음). 규칙은 위에서부터 확인해 처음 맞는 규칙으로 답합니다.
- 필드 초기값은 에셋을 처음 만들 때 들어가는 예시이며, 이후 수정은 에셋에서 합니다. 이름·대사는 서버 데이터라 서버 연동 후에는 서버 값을 씁니다.
- 서버 시드(`backend/src/main/resources/db/seed/world.sql`)에 있는 NPC는 대본의 이름·역할 이름을 시드와 같게 둡니다. 서버 대화로 넘어가도 이름이 바뀌지 않게 하려는 것이며, `MockDialogueScriptsAssetTests`가 시드를 읽어 확인합니다. 시드 이름이 바뀌면 필드 초기값을 고치고 **Dialogue → Build**를 실행하면 에셋의 이름도 맞춰집니다.
- Web 빌드의 Unity 입력창은 한국어 입력이 되지 않습니다(S15P31A305-200 PoC 결론). 입력 UI를 HTML로 옮기는 작업 전까지 영어로 입력합니다.

공간 씬의 대화 처리와 화면은 [World 모듈의 공간 기능 연결](../World/README.md#새-공간-씬-만들기)이 붙이므로 씬마다 직접 배치하지 않습니다. 화면 프리팹과 확인용 대본은 **Tools → A305 → Dialogue → Build**(`Scripts/Editor/Dialogue/DialogueBuilder.cs`)로 준비합니다. 화면은 없는 요소만 만들고, 이미 있는 요소의 디자인은 유지합니다.

## 테스트

| 어셈블리 | 위치 | 검증 |
| --- | --- | --- |
| `A305.Dialogue.Tests` | `Assets/Tests/EditMode/Dialogue/` | 대화 시작, 대화 중 두 번째 시작 거절, 종료 뒤 다시 시작. 첫 대사, 키워드 응답 순서, 기본 응답, 빈 입력 무시, 정해진 흐름의 턴 진행·완료, 예시 문장, 최근 NPC 대사의 뜻, 대화 기록. 서버 대화의 뜻 보기·힌트(한 번만 요청, 실패 안내, 새 NPC 대사에서 비움, 대화가 끝난 뒤 힌트 요청 안 함). 오류별 안내와 재시도(대화 시작 다시 하기, 같은 요청 ID로 발화 다시 보내기), 사용량 소진 종료 안내, 녹음 안내 문구, 피드백 다시 만들기, AI 상태(AI-01)의 대화 시작 가능 여부·게스트 남은 대화 수 받기와 실패 시 직전 상태 유지 |

대화 시점·이동 잠금·[대화 종료]는 Npc 모듈의 PlayMode 테스트에서 함께 확인합니다. Esc 종료는 키 입력을 그대로 전달하는 연결이라 Play에서 확인합니다. 실행 방법은 [프론트엔드 개발 지침](../../../../AGENTS.md)을 따릅니다.

## NPC 얼굴과 음성 재생

대화 시작 시 `LookTarget`의 부모 `DialogueSpeaker`가 선택된 NPC의 얼굴·재생기를 준비합니다. `NpcIdentity`가 화자 컴포넌트를 자동으로 제공하므로 NPC별 Builder 연결을 반복하지 않습니다. 얼굴 채널이 부족하면 `LipSyncNotice`에 이유를 남기고 기존 음성 재생을 유지합니다. 지원되는 모델은 같은 WAV의 음소 시간표와 AudioSource 샘플 위치를 사용하며, 화자 변경·종료·늦은 음성 응답 취소는 기존 수명 처리를 따릅니다. 표정 메시 생성이나 LLM 감정 추론 기능은 아닙니다.

새 대사에 음성이 없으면 `ForgetSpeech`가 이전 음성과 발음 시간표를 함께 버리고 얼굴을 중립으로 되돌립니다. 화자 연결은 유지하므로 다음 정상 음성은 같은 NPC에서 재생됩니다. 대화 종료는 화자 연결도 해제합니다. 내려받는 중 대사가 바뀌거나 종료되면 이전 응답을 재생하지 않습니다.

음성 보관 정책은 [NPC 음성 저장 운영](../../../../../docs/npc-audio-storage.md)을 따릅니다. 대사가 바뀌거나 대화를 끝내면 이전 음성과 진행 중인 다운로드를 무효화합니다. EditMode 대화 테스트는 실제 대화 프리팹의 상태별 버튼 표시, 404 후 자막 유지, 최초 실패 재시도, 일시적인 다운로드 실패와 이전 턴의 늦은 응답을 검증합니다.

현재 음성의 404는 `ServerConversation`에 전달해 만료 상태로 바꾸고 다시 듣기를 숨긴다. 대화가 끝난 뒤 도착한 이전 음성의 404는 무시하므로 새 대화의 상태를 바꾸지 않는다.
