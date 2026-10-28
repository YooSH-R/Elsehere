# 플레이어 캐릭터 검증과 NPC 대화 연결 계획

현재 우선 목표는 이미 생성한 캐릭터 한 개를 플레이어로 연결하여 실제 조작과 몸 움직임이 원하는 수준인지 확인하는 것이다. NPC의 표정·립싱크·AI 대화는 그다음 단계로 보존한다. 개별 모델의 눈썹·눈꺼풀·피부 등을 계속 보정하는 작업은 중단한다.

플레이어는 [리틀 멍 플레이어 확인](little-mung-player.md)의 공용 Player·Production 연결과 전용 비교 씬을 사용한다. F는 보정 리틀 멍, M은 기존 대체 외형을 적용한다. 아래 A 단계는 전용 검수부터 공용 적용까지의 진행 기준이다. 아래 NPC 표정·립싱크 연결은 후속 계획이며 아직 구현 완료로 간주하지 않는다.

## 작업 범위와 승인 경계

- 기존 분리 작업 트리의 캐릭터 파이프라인 작업을 이어 사용한다. 기본 프로젝트의 기능 개발 체크아웃은 수정하지 않는다.
- 먼저 플레이어 검증 단계를 진행하고, NPC 단계는 별도 착수 확인을 받는다. 플레이어 단계 승인을 NPC 전체 구현 승인으로 확대하지 않는다.
- 원본 생성 모델과 기존 보정본은 유지한다. 검증용 모델·프리팹·씬은 별도 경로로 만든다.
- Tripo 생성이나 재생성, 유료 API 사용, 화면 자동제어는 이번 계획의 실행 범위에 포함하지 않는다.
- 코드 변경은 테스트 우선 개발로 진행하고 작은 기능 단위로 커밋한다. 시각 에셋 구성은 Builder로 만들고 실제 렌더·플레이 검증으로 확인한다.
- MR은 사용자 허락 후 생성한다. 계획 저장은 기능 구현 착수를 의미하지 않는다.
- 기술적인 조사·연결·검증은 에이전트가 수행하고, 사용자에게는 조작감과 외관을 판단할 수 있는 결과를 제시한다. 비용·배포·기존 권한 범위 변경은 별도 승인 대상이다.

## 현재 코드의 활용 구분

| 구분 | 대상 | 활용 방침 |
| --- | --- | --- |
| 유지 | TripoPipeline, TripoHttpApi, TripoJobStore | 생성·업로드·복구 기능을 보존한다. 이미 받은 모델의 플레이어 검증에서는 호출하지 않는다. |
| 유지 | TripoModelImporter, TripoMaterialImport, TripoLocalWorkflow, TripoBlenderBridge | 로컬 모델 가져오기, 재질 준비, 복사본 왕복에 사용한다. |
| 유지 | PlayerMover, PlayerMotor, CharacterAnimationDriver | 기존 이동·입력·카메라 동작을 재사용한다. Animator의 Speed, IsGrounded, VerticalSpeed를 연결한다. |
| 유지 | TripoModelReview, 구조·UV·본·소켓 검사 | 자동으로 확인할 수 있는 항목만 검증 근거로 사용한다. 검증 통과를 외관 완성으로 표현하지 않는다. |
| 수정 예정 | 검수 완료 상태 | 파일 생성, 리깅 준비, 동작 검수, 얼굴 준비, 음성 동기화, 외관 승인을 구분한다. 필수 항목 미검사를 합격으로 표시하지 않는다. |
| NPC 단계에서 분리 | TripoFacePreview.Mix, TripoFaceBindings.Apply, TripoSpeechTimeline.Sample | 필요한 계산·매핑을 런타임에서 사용할 수 있게 옮기고 에디터가 같은 로직을 사용하도록 한다. UnityEditor 의존 기능은 에디터에 둔다. |
| NPC 단계에서 수정 | TripoFaceClip.Evaluate와 다중 메시 얼굴 매핑 | 하나의 표정 제어가 얼굴과 속눈썹을 함께 움직일 때 기존 클립 평가가 중복 제어로 거부하는 경로를 테스트로 재현하고 수정한다. 클립 경로·이름 매핑도 검증한다. |
| 제한적으로 유지 | TripoAudioPreview, TripoMotionPreview, Review.controller | 에디터 음성·관절 검수 도구로 보존한다. 실시간 음성 재생기나 공통 이동 Controller로 간주하지 않는다. |
| 보류 | PoseCorrectiveDriver와 기존 무릎·옷 보정 프로파일 | 해당 검수 모델에 종속된 보정으로 보존한다. 새 여성 모델에 자동 적용하지 않는다. |
| 보류 | 개별 얼굴 보정 스크립트, BlinkCandidate, 추가 캐릭터 생성, 머리·옷 모듈화 확대 | 공통 생산 파이프라인으로 편입하지 않는다. 플레이어 동작 검증을 가로막지 않도록 현재 단계에서 확장하지 않는다. |

## 플레이어 캐릭터 구현 순서

| 순서 | 구현할 범위 | 확인할 결과 |
| --- | --- | --- |
| A1 기존 에셋 기준 고정 | 생성한 LittleMung_Casual_Female 원본과 재질 수정 복사본 중 검사에 적합한 복사본을 준비한다. 외형 조형은 하지 않는다. 정면, 높이, Humanoid 매핑, 텍스처, 바인드 자세를 확인한다. | 어떤 파일을 검증하는지 명확하고 재생성이 필요하지 않은 상태. 얼굴 표정이 없어도 이 단계의 실패 조건으로 삼지 않는다. |
| A2 이동 애니메이션 준비 | Idle, Walk, Run, Jump 또는 상승·낙하·착지에 대응하는 사용 가능한 클립을 확보하고 Humanoid 호환성을 검사한다. 현재 검수용 Diagnostic/Greeting 클립을 걷기·뛰기 클립으로 대체 사용하지 않는다. | 실제 클립 목록과 사용 조건, 리타게팅 결과. 아직 확보하지 못한 클립은 미완료로 표시한다. |
| A3 기존 플레이어에 연결 | 별도 검증용 플레이어 프리팹과 Animator Controller를 만든다. 기존 PlayerMover → CharacterAnimationDriver 흐름에 연결한다. 이동은 CharacterController가 맡고 제안 기본값은 루트 모션 비활성화다. | 입력으로 이동·회전·달리기·점프하며 외형 애니메이션이 상태와 일치한다. 루트 이동이 이중 적용되지 않는다. |
| A4 전용 씬에서 동작 검증 | 기존 Production 씬과 PlayerSandbox를 덮어쓰지 않는 별도 씬에서 정지, 출발, 정지 전환, 급회전, 걷기, 달리기, 점프, 착지, 벽·경사·계단 접촉을 확인한다. 인사는 이동 검증 뒤 별도 동작으로 확인한다. | 상태 전환, 발 미끄러짐·뜸, 팔·머리·옷 관통, 무릎·어깨 변형을 동일 동작으로 비교할 수 있다. 자동 검사와 시각 판정을 구분한다. |
| A5 사용자 확인과 적용 범위 결정 | 전신 정면·측면·게임 카메라 영상과 직접 조작 가능한 검증 씬을 제공한다. | 사용자가 외관·비율·조작감을 판단한다. 승인 전에는 기본 플레이어 프리팹과 Production 씬에 적용하지 않는다. |

플레이어 단계의 완료 조건은 캐릭터 한 개가 기존 입력·이동 구조로 정상 조작되고, 필요한 이동 클립과 전환이 실제로 연결되며, 외관 검수가 끝난 상태다. 표정·립싱크 완성이나 모든 NPC의 생산 자동화를 완료 조건에 섞지 않는다.

본 뒤집힘·잘못된 스케일처럼 연결 오류가 발견되면 해당 연결 문제를 수정한다. 메시·스킨 가중치의 결함이 남으면 동작과 위치를 기록해 보여주고, 현재 범위를 넘어서는 반복 조형 보정으로 자동 확대하지 않는다.

## 이후 NPC 한 개의 표정과 립싱크 구현 순서

플레이어 검증 뒤 별도로 착수한다. 기존에 검토한 표정·립싱크 설계를 이 단계에서 사용한다.

| 순서 | 구현할 범위 | 완료 조건 |
| --- | --- | --- |
| B1 얼굴 에셋 요구 확정 | 몸 리그와 표정 변형을 갖춘 기준 NPC 한 개를 준비한다. 기존 후보는 감정 7종, 감정·눈 제어 15개, 무음 포함 Viseme 15종이며 실제 키는 29개다. 기존 여성 모델에는 해당 키가 없다. 제작 방식·범위를 NPC 단계 착수 시 확정하고 반복적인 개별 얼굴 보정을 자동 재개하지 않는다. | 이름만 있는 빈 키가 아니라 실제 메시 변형이 존재하고 외관을 승인받는다. 한 제어가 얼굴·속눈썹 등 여러 메시를 함께 구동한다. |
| B2 공통 얼굴 런타임 연결 | 감정 혼합·얼굴 매핑·발음 시간표 샘플링을 게임 런타임에서 사용하도록 분리한다. 몸 Animator와 얼굴 제어가 서로 덮어쓰지 않게 한다. | 눈 감기·감정·발음을 게임 모드에서 실제 SkinnedMeshRenderer에 적용하고 중립으로 복원한다. 에디터와 런타임이 같은 계산 로직을 쓴다. |
| B3 저장 음성으로 먼저 검증 | 검증용 음성과 정확히 대응하는 발음 시간표를 사용한다. 런타임 오디오 재생 위치에 맞춰 Viseme를 구동한다. | 대화 중 감정과 입 모양이 함께 움직인다. 음성 중단·재시작·다음 발화·대화 종료에서 입 모양이 남지 않는다. 이 단계는 실시간 AI 대화 완료로 표시하지 않는다. |
| B4 기존 NPC 행동에 연결 | NpcIdentity와 기존 접근·대화 흐름을 유지하고 테스트용 NPC 프리팹의 Visual과 얼굴·음성 구성만 연결한다. | NPC 하나를 만나 대화를 시작하면 표정·립싱크가 동작하고 종료하면 기존 상태로 돌아간다. |
| B5 실제 AI 대화로 교체 | 백엔드·AI 담당 영역과 대사, 감정·강도, 음성 조각 순서, 발음 시간표, 취소 기준의 계약을 맞춘다. 현재 AI 워커는 대사와 음성 조각을 제공하지만 감정·Viseme는 제공하지 않는다. | 실제 LLM 대사·TTS 음성과 얼굴 변형이 연결된다. 음성 누락·지연·취소도 처리한다. 녹음·목업만 통과한 상태를 실제 연동 완료로 표시하지 않는다. |
| B6 NPC 확장 검증 | 두 번째 NPC에 같은 행동·Controller·얼굴 제어를 재사용하고 모델별 매핑만 교체한다. | NPC마다 전용 동작 코드를 복제하지 않아도 된다. 공통 골격·표정 이름뿐 아니라 실제 변형과 바인드 자세도 검증한다. |

## 기존 기능 개발과의 경계

플레이어 초기 검수 단계는 전용 캐릭터 에셋 폴더, 검증용 프리팹·Controller·씬, 전용 Builder를 추가하는 범위로 시작한다. 기존 PlayerMover, PlayerMotor, 입력 액션, 카메라, 대화, NPC 접근 로직과 Production 씬은 변경하지 않는 구성을 우선한다.

분리 작업 트리의 기존 플레이어 코드와 기본 체크아웃의 기능 코드는 버전 차이가 있다. 착수 전에 공유 클래스의 기준 버전과 차이를 읽고, 다른 기능 작업의 변경을 덮어쓰거나 통째로 가져오지 않는다. 공유 코드 변경이 실제로 필요하면 별도 커밋으로 분리하고 회귀 검사 범위를 명시한다.

PlayerSandboxBuilder.Build는 기존 Sandbox와 공용 에셋을 갱신하므로 모델 비교 검수에서 그대로 실행하지 않는다. 공용 적용은 PlayerCharacterBuilder로 플레이어 연결만 갱신한다. 필요한 구성을 별도 Builder로 생성한다. NPC 단계의 backend/·ai/ 변경은 계약을 합의한 별도 기능 작업에서 처리한다.

기능 단위 커밋은 플레이어의 검증 에셋 구성, Controller 연결, 검증 결과 제공 순으로 나눈다. NPC 단계는 얼굴 런타임 분리, 다중 메시 매핑 수정, 저장 음성 재생, NPC 연결, 실제 AI 연동을 각각 분리한다.

## 기존 코드 근거

- [PlayerMover](../../frontend/UnityClient/Assets/Scripts/Player/PlayerMover.cs): 입력과 CharacterController 이동 후 CharacterAnimationDriver.SetLocomotion 호출.
- [CharacterAnimationDriver](../../frontend/UnityClient/Assets/Scripts/Characters/CharacterAnimationDriver.cs): Speed, IsGrounded, VerticalSpeed 전달.
- [PlayerSandboxBuilder](../../frontend/UnityClient/Assets/Scripts/Editor/Player/PlayerSandboxBuilder.cs): 기존 공용 프리팹과 Sandbox 갱신 범위.
- [TripoFacePreview](../../frontend/UnityClient/Assets/Scripts/Editor/Tripo/TripoFacePreview.cs): 감정·발음 혼합의 현재 구현.
- [TripoFaceBinding](../../frontend/UnityClient/Assets/Scripts/Editor/Tripo/TripoFaceBinding.cs): 얼굴 변형 탐색·검사·적용.
- [TripoWindow.Speech](../../frontend/UnityClient/Assets/Scripts/Editor/Tripo/TripoWindow.Speech.cs): 기존 음성·시간표 검수. 게임 런타임 연결은 별도 구현 대상.
- [TripoWorkbench](../../frontend/UnityClient/Assets/Scripts/Editor/Tripo/TripoWorkbench.cs): 구조 검사와 선택적 얼굴·공통 모델 검사의 범위.
