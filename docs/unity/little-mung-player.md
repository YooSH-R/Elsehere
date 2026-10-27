# 리틀 멍 플레이어 확인

이미 생성한 여성 캐릭터를 기존 `PlayerMover`와 `CharacterAnimationDriver`에 연결한다. 보정된 계단 대응 모델은 공용 Player 프리팹과 집·마을·카페·호텔 Production 씬에서 F 선택 외형으로 사용하며, 전용 Sandbox는 동작 비교용으로 유지한다. 얼굴 표정·립싱크는 [후속 NPC 계획](character-integration-plan.md)에서 다룬다.

## 공용 플레이어와 캐릭터 선택

- `Assets/Scenes/Production/TownScene.unity`를 열고 Play하면 기본 F 리틀 멍을 조작할 수 있다. 집·카페·호텔도 같은 공용 `Assets/Prefabs/Player/Player.prefab`을 사용한다.
- 실제 가입·재접속은 `EntryScene`에서 시작한다. 가입의 F/M 선택은 기존 계정 API가 저장하고, `OnboardingFlow`가 가입 응답·bootstrap의 `avatarType`을 보존한다. `OnboardingScreen`이 월드로 넘어가기 전에 `PlayerCharacterSelection`에 전달한다.
- F는 `LittleMungStairPlayer.prefab`의 보정 Visual 복사본을 사용하며, 공용 `Female.prefab`의 루트 배율은 1.6이다. 이 값은 키가 아닌 모델 배율이다. F 정의의 충돌 캡슐은 대기 자세의 실제 스킨 범위를 기준으로 높이 1.61m·반경 0.68m이며, 카메라 눈높이는 약 1.108m·3인칭 조준점은 0.966m이다. 외형 크기를 바꾸면 캡슐·시점·관통을 다시 검수해야 한다. M은 기존 `LittleMungV7PlayerVisual.prefab`의 대체 외형 복사본이다. M에는 아직 남성 모델이나 몸 애니메이션이 없다. 이동·점프·카메라·상호작용은 기존대로 동작한다.
- `Assets/Data/Player/Characters/Catalog.asset`이 선택 ID와 `F.asset`·`M.asset`을 연결한다. 각 정의는 Visual 프리팹, 이동 설정, 충돌 캡슐 크기와 선택적인 카메라 높이를 갖는다. 남성 모델이 준비되면 M 정의의 외형·이동·캡슐 설정을 교체한다. 신규 ID는 서버에서 허용하는 선택값과 가입/설정 UI도 함께 확장해야 한다.
- `PlayerCharacterAppearance`가 루트를 유지하고 선택 외형만 연결한다. F의 발 골격·신발 스킨·접지·계단·팔 관통 보정을 그대로 재사용한다. `PlayerCharacterVisibility`가 V키 1인칭 및 대화 시점에서 선택 외형만 숨기고 3인칭 복귀 시 다시 보여 준다.
- 선택값은 씬 이동 동안 유지되며 재접속 때 서버에서 다시 읽는다. Entry 진입·인증 만료 시 이전 계정 선택을 비운다. 씬을 직접 실행해 선택값이 없거나 목록에 없는 ID이면 카탈로그 기본 캐릭터를 사용한다. 계정과 별개인 PlayerPrefs 저장은 하지 않는다.
- 새 캐릭터 설정 화면이나 플레이 도중 즉시 외형 변경 기능은 포함하지 않는다. 기존 가입 선택과 로그인 결과를 게임에 적용하는 연결이다.
- 재연결 메뉴는 **Tools → A305 → Player → Connect Selected Character**다. 공용 Player와 네 공간 씬의 플레이어 연결만 갱신한다. 기존 배경·NPC·카메라 배치와 원본 모델을 유지하고, 이미 등록한 Visual·프로필 에셋을 덮어쓰지 않는다. 전체 월드 Builder를 실행할 필요는 없다.

자동 검증은 F/M의 네 씬 이동, 플레이어·카메라·출입구 참조, 재연결 중복 방지, 걷기·달리기·점프, 이동 잠금, 1인칭/대화 시점의 외형 숨김을 확인한다. 리틀 멍의 팔·손 침범 검사는 스킨 정점과 몸통·머리 범위를 이용한 근사 판정이며 모든 외관 결함이 없다는 보증은 아니다. 실제 브라우저의 입력·프레임 성능과 최종 외관은 별도 확인한다.
## 열기와 조작

1. 분리 프로젝트의 `frontend/UnityClient`를 Unity 6000.3.26f1로 연다.
2. Project에서 `Assets/Scenes/Sandbox/LittleMungPosePlayerSandbox.unity`를 연다. 기존 골격/수정 발 골격만 비교하려면 아래의 별도 검수 씬을 사용한다.
3. Play를 누르고 Game 화면을 한 번 클릭한다.
4. WASD로 이동, Shift를 누른 채 이동하면 달리기, Space로 점프한다.
5. 마우스 오른쪽 버튼을 누르고 드래그하면 시점을 돌리고, 휠로 확대·축소한다.
6. 왼쪽 계단, 오른쪽 경사로, 앞쪽 벽에서 움직임을 확인한다.

이 씬에는 생성/API 키 입력이 필요하지 않다. Builder는 저장소에 포함된 `Assets/Art/Player/LittleMung/LittleMung.fbx`와 `BaseColor.png`를 사용한다. 파일이 누락되면 저장소에서 복원하며 개인 Tripo 생성 폴더를 요구하지 않는다. 메뉴 `Tools → A305 → Player → Build Little Mung Sandbox`는 누락된 전용 에셋과 씬을 구성한다. 이미 있는 전용 프리팹·컨트롤러·씬은 보존한다.

## 연결 구조와 에셋

| 역할 | Unity 프로젝트 기준 경로 |
| --- | --- |
| 플레이어 프리팹 | `Assets/Prefabs/Player/LittleMung/LittleMungPlayer.prefab` |
| 모델·텍스처·머티리얼·대기 클립·Animator | `Assets/Art/Player/LittleMung/` |
| 이동·카메라 수치 | `Assets/Data/Player/LittleMung/Movement.asset`, `Camera.asset` |
| 외부 원본 모션 | Assets/ThirdParty/MixamoLocomotion/ |
| 공통 휴식 Avatar·변환 모션 | Assets/Art/Player/LittleMung/Motions/Reference.fbx 및 Walk/Run/Jump.fbx |
| 생성 코드 | `Assets/Scripts/Editor/Player/LittleMungPlayerBuilder.cs`, `LittleMungAnimationBuilder.cs` |
| 조작 통합 테스트 | `Assets/Tests/PlayMode/Player/LittleMungPlayerTests.cs` |

- 이동 루트의 CharacterController가 위치를 바꾼다. 모델 Animator의 Root Motion은 꺼 두어 이동이 중복 적용되지 않는다.
- `Speed`로 Idle/Walk/Run을 혼합하고, `IsGrounded`와 `VerticalSpeed`로 Rise/Fall/Land를 전환한다. 점프를 고정 재생 시간만으로 판단하지 않는다.
- Humanoid가 정규화한 애니메이션 자세를 기준으로 +Z 정면과 발 높이를 맞춘다. 원본 FBX의 바인드 방향을 다시 회전시켜 이중 보정하지 않는다.
- 모델과 기본 색 텍스처는 기존 재질 수정본의 별도 복사본이다. 셰이더는 URP Lit이며 색 텍스처를 Normal Map으로 사용하지 않는다.
- 이동 설정 기본값은 걷기 0.8 m/s, 달리기 1.7 m/s, 점프 높이 0.45 m다. 짧은 다리의 현재 애니메이션 보폭에 맞춘 초기 조작값이며 최종 기획 수치가 아니다.
- Walk.anim과 Run.anim은 변환된 FBX에서 가져온 클립의 복사본이다. 발목을 포함한 근육 곡선을 그대로 유지한다. 걷기·달리기·점프는 실제 휴식 골격으로 만든 Reference Avatar를 공유하며, 본 이름·부모·길이가 맞지 않으면 재연결을 거부한다.
- Idle은 Mixamo Jump 종료 자세를 바탕으로 호흡 변화를 추가했다. 외부 모션 사용 조건은 `Assets/ThirdParty/CREDITS.md`를 따른다.
- 다른 씬에 넣을 때 `PlayerMover.cameraTransform`과 `PlayerCameraRig.followTarget`을 연결해야 한다. 전용 Builder에서는 `PlayerSandboxBuilder.ConnectPlayerAndCamera`를 재사용한다.

## 검수와 한계

자동 테스트는 입력 이동·회전·달리기 속도·점프/착지 전환, 실제 다리 본 변화, 몸의 정면, 정지 시 루트 이동 없음, 벽 충돌·계단·경사로 통과를 확인한다. 프레임 간격을 고정하고 가상 키보드 이벤트를 사용한다. 사용자의 실제 키보드나 화면은 제어하지 않는다.

시각적으로는 정면·측면에서 발 접지와 보폭, 급회전 때 미끄러짐, 무릎·어깨 변형, 소매·반바지 관통을 확인한다. 단위 테스트 통과는 모든 메시 관통이 없다는 뜻이 아니다. 초기/수정 골격 비교 씬에는 발 IK가 없다. 접지·팔 보정 사본의 동작과 지형별 한계는 아래에서 설명한다. 현재 자동 리깅 모델의 세부 얼굴·옷 가중치를 새로 조형하지 않는다.

PlayMode 테스트에서 `LittleMungPlayerTests`를 실행한다. 배치 실행에서는 `-runTests -testPlatform PlayMode -testFilter LittleMungPlayerTests`를 사용하고 `-quit`은 함께 쓰지 않는다. 실제 게임 카메라·정면·측면 캡처는 `CharacterRespondsToWalkRunJumpAndReturnsToIdle` 테스트만 선택하여 `-playerCaptureOutput <저장 폴더>`를 추가한다. 캡처 실행에는 `-nographics`를 쓰지 않는다.

## 모션 기준 Avatar 재구성

원본 FBX와 별도로 Blender에서 휴식 골격 Reference.fbx와 동일 좌표계의 Walk/Run/Jump.fbx를 내보낸다. 움직이는 프레임을 새 T-Pose로 간주하거나, 임의의 발목 곡선으로 보행을 보정하지 않는다. 변환 FBX와 메타 파일은 저장소에 포함되어 있으므로 일상적인 플레이 검수에 Blender 실행은 필요하지 않다.

다시 내보내야 할 때 저장소 루트에서 실행한다.

    & "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python-exit-code 1 --python frontend/ArtSources/Player/LittleMung/export_motions.py -- --source frontend/UnityClient/Assets/ThirdParty/MixamoLocomotion --output frontend/UnityClient/Assets/Art/Player/LittleMung/Motions --replace-generated

Unity에서 Tools → A305 → Player → Reimport Little Mung Motions를 실행하면 생성된 이동 클립과 기존 Controller의 이동·점프 상태 연결을 갱신한다. 전용 Walk/Run/Idle 클립은 재생성하므로 수동 편집할 모션은 별도 이름으로 복사해야 한다. 원본 ThirdParty 모션과 플레이어 입력 코드는 변경하지 않는다.

회귀 검사는 수평 발 방향, 실제 모션 평가 여부, 기준 골격의 좌우 무릎 대칭, 공통 Avatar 연결, 발목 대체 곡선이 없는지도 확인한다.

## Blender 발 골격 편집본

frontend/ArtSources/Player/LittleMung/LittleMung_Player_Ankle_Adjusted.blend는 원본을 보존한 골격 편집본이다. 이 골격은 별도의 Unity 플레이어 검수 씬에 연결한다. 기존 LittleMungPlayerSandbox와 기존 FBX는 비교 기준으로 보존한다.

- 발 구조는 발목 → 앞꿈치 관절 → 발가락 → 끝점 순서다. 본 선이 발바닥 표면을 나타내는 것은 아니다.
- 왼발의 최종 끝점은 사용자 지정 위치를 유지한다. 앞꿈치 관절은 발목보다 앞에 두고 오른발을 몸 중심면 기준으로 맞춘다.
- Edit Mode는 기본 골격, Pose Mode는 동작 자세를 편집한다. G로 이동, E로 연결 본을 추가한다.
- 본을 추가하면 해당 메시의 가중치도 연결해야 한다. 기존 본 이름·부모 구조를 바꾸면 Unity Avatar 매핑과 모션 재검사가 필요하다.
- Blender 편집본과 함께 저장된 FBX는 후보 교환 파일이다. 수정본을 자동으로 Unity 원본에 덮어쓰는 기능은 설정하지 않는다.

## 수정 발 골격 검수 씬

- 씬: Assets/Scenes/Sandbox/LittleMungAdjustedPlayerSandbox.unity
- 프리팹: Assets/Prefabs/Player/LittleMung/LittleMungAdjustedPlayer.prefab
- 모델: Assets/Art/Player/LittleMung/AdjustedRig/LittleMungAdjusted.fbx
- 메뉴: Tools → A305 → Player → Build Adjusted Foot Rig Sandbox

기존과 동일한 WASD·Shift·Space 입력 및 Locomotion.controller를 사용한다. 발목 곡선 대체나 Foot IK는 추가하지 않는다. Builder는 기존 검수 에셋에서 별도 사본을 만들고 수정 모델·Animator·카메라 참조를 연결한다. 이미 만들어진 수정 프리팹과 씬은 다시 만들지 않는다.

Blender에서 새 사본을 내보낼 때 frontend/ArtSources/Player/LittleMung/export_player.py에 --source <blend 파일> --output <새 FBX 경로>를 전달한다. 기존 출력 파일은 덮어쓰지 않는다. 이 스크립트는 FBX_SCALE_ALL로 단위 변환을 FBX에 저장하며, Blender 원본에 저장하지 않는다. 예전 교환용 LittleMung_Player_Ankle_Adjusted.fbx 대신 이 절차의 Unity용 FBX를 사용한다.

수정 모델의 Avatar는 기존 본 대응 이름을 재사용하되, 관절 위치·회전·스케일은 수정 FBX의 휴식 골격에서 다시 읽는다. 기존 Avatar의 관절 위치를 복사하면 단위 변경 전 좌표가 남아 애니메이션 중 메시가 축소되거나 이동할 수 있다.

LittleMungPlayerTests는 기존/수정 씬을 같은 허용값으로 검사한다. 명시적인 -playerCaptureOutput 옵션을 주면 아래의 original/before/after 구분에 따라 실제 플레이 이미지와 foot-contact.csv를 기록한다. CSV의 발 높이는 발·발가락 가중치 합이 0.5 이상인 메시 정점의 최저 높이이며, 평지 y=0 기준이다. 공중 동작의 발 높이와 달리기의 비행 구간은 접지 실패로 간주하지 않는다. 본의 위쪽 방향 값만으로 메시 뒤집힘을 판정하지 않고 렌더와 함께 검수한다.

## 접지·팔 관통 보정 검수 씬

- 씬: `Assets/Scenes/Sandbox/LittleMungPosePlayerSandbox.unity`
- 프리팹: `Assets/Prefabs/Player/LittleMung/LittleMungPosePlayer.prefab`
- 메뉴: Tools → A305 → Player → Build Contact And Arm Clearance Sandbox

수정 발 골격 모델과 동일한 이동·점프 클립을 사용한다. Builder는 별도 사본에 보정 컴포넌트를 연결한다. 기존 두 검수 씬과 모델·모션 파일을 덮어쓰지 않는다.

`LittleMungGroundContact`는 초기 스킨 밑면의 배치 높이와 CharacterController의 충돌 여유 높이를 분리한다. 첫 접지 후에만 충돌 여유를 제외하고, 공중에서도 같은 모델 기준 높이를 유지한다. 접지 상태에서는 신발 밑면이 지면에 들어갈 때 다리의 두 관절 IK로 들어 올린다. 정지할 때는 양발을 지면에 맞추며, 보행 중 원래 발을 들어 올리는 구간은 유지한다. 발목의 월드 회전과 본 길이를 유지하므로 발목 곡선을 대체하거나 다리를 늘리지 않는다.

`LittleMungArmClearance`는 현재 리틀 멍의 머리·몸통 스킨 범위에서 손과 전완이 겹치는 목표 위치를 찾는다. 손을 몸통 옆과 턱 아래의 도달 가능한 위치로 옮기고, 어깨·팔꿈치의 두 관절 IK로 연결한다. 손목의 원래 월드 회전과 모션 재생 속도는 유지한다. 머리와 손의 비율이 다른 NPC에 그대로 적용하는 공통 리깅 규격은 아니다.

보정은 Animator 갱신 후 LateUpdate에서 실행한다. 스킨 계산과 렌더가 같은 네 개의 본 가중치를 사용하도록 이 프리팹의 SkinnedMeshRenderer 품질을 Bone4로 지정한다. 현재 모델에는 Blend Shape가 없으므로 이 스킨 샘플 계산은 Blend Shape 변위를 포함하지 않는다. 향후 표정/변형이 있는 모델에 적용할 때는 그 변위를 포함하는 검사를 별도로 연결해야 한다.

### 검수 순서와 한계

1. 정지 상태에서 양발 밑면과 바닥 사이를 정면·측면으로 확인한다.
2. WASD, Shift 이동에서 발을 딛는 순간·발을 드는 순간을 구분한다.
3. Space로 점프하고 상승·하강·착지에서 손이 머리 안으로 숨는지 확인한다.
4. 몸통 옆으로 팔이 지나갈 때 소매·겨드랑이·손목 모양을 확인한다.
5. 계단·경사로에서는 이동 가능 여부와 발바닥 기울기를 따로 확인한다.

이 보정은 지면 높이를 맞추며 경사면 법선에 발바닥을 회전시키거나 보행 중 발을 수평으로 고정하지 않는다. 모든 지형의 접지·미끄러짐 해결을 보장하지 않는다. 팔 검사는 머리·몸통을 감싸는 타원체로 깊은 관통을 찾는 근사 검사이며, 모든 메시 삼각형 사이의 충돌이나 모든 모션에서의 실루엣을 보장하지 않는다. 원본 메시의 옷·손 가중치나 토폴로지는 수정하지 않는다.

`LittleMungContactTests`는 생성 직후, 정지, 걷기·달리기의 실제 신발 스킨 높이를 검사한다. `HandsAndForearmsStayOutsideTheHeadAndTorso`는 같은 이동·점프 입력으로 손과 전완의 깊은 관통을 검사한다. 최종 자세 측정과 캡처는 보정 이후의 LateUpdate에서 실행한다. Unity `BakeMesh(true)`로 배율을 보상한 뒤 TransformPoint를 적용하여 캐릭터 확대 배율이 두 번 계산되지 않게 한다.

명시적인 `-playerCaptureOutput <폴더>` 실행은 `original`(초기 골격), `before`(수정 발 골격), `after`(접지·팔 보정) 폴더를 만든다. 같은 동작의 정면·측면 이미지와 `foot-contact.csv`로 비교한다. 캡처는 테스트용 카메라 렌더이며 사용자 화면을 조작하지 않는다.

## 계단·경사로 접지 실험 씬 — 미채택

이 씬은 플레이어 기본 사본으로 사용하지 않는다. 접지 중 정점 고정 검사는 통과하지만, 골반 높이의 불연속으로 몸이 떨리고 다음 계단의 디딤 위치를 계획하지 못한다. 비교·후속 개발 기준은 기존 LittleMungPosePlayerSandbox를 사용한다. 기존 Pose 사본도 계단 전용 보행은 구현하지 않았다.

현재 검사에는 몸 높이/속도의 연속성, 발을 들기 전 단차 예측, 계단 수직면 회피, 디딤면 내부 착지, 착지 후 체중 이동이 빠져 있다. 접지 수치 통과를 계단 보행 품질 합격으로 사용하지 않는다. 향후 수정은 다음 디딤면 탐색 → 발을 드는 궤적 → 착지/지지 전환 → 몸 높이 연속성 순서로 별도 검증해야 한다. 기존 PlayerMover의 CharacterController stepOffset은 장애물을 통과시키는 충돌 이동 설정이며 계단 보행 애니메이션이 아니다.

- 씬: `Assets/Scenes/Sandbox/LittleMungTerrainPlayerSandbox.unity`
- 프리팹: `Assets/Prefabs/Player/LittleMung/LittleMungTerrainPlayer.prefab`
- 밑창 가중치 사본: `Assets/Art/Player/LittleMung/TerrainSkin/Sole0.asset`
- 메뉴: Tools → A305 → Player → Build Terrain Contact Sandbox

씬을 열고 Play한 뒤 WASD·Shift로 계단과 경사로를 왕복하고 Space로 점프한다. 기존 Pose 씬과 같은 골격·이동 클립·입력 속도를 사용한다. 지형 사본에는 `LittleMungGroundContact` 대신 `LittleMungTerrainContact`를 연결한다.

접지할 때 발바닥을 지면 법선에 정렬하고 발과 발가락 자세를 고정한다. 발을 앞으로 가져오는 구간에는 고정을 풀어 원래 모션을 재생하고, 신발 앞뒤 범위로 다음 디딤면을 검사해 필요한 높이를 확보한다. 한쪽 발이 낮은 디딤면에 남아 있으면 다리 길이 안에서 골반을 내린다. 무릎 힌트는 캐릭터 정면으로 유지해 다리가 거의 펴질 때 굽힘 방향이 뒤집히는 것을 막는다. 점프 중에는 접지를 해제한다.

원본 신발 밑면에는 정강이 가중치가 섞여 있어 발목만 고정해도 밑창이 휘었다. Builder는 별도 메시 에셋을 만들고 밑창의 가중치를 기존 발·발가락 본 사이에서 정규화한다. 신발 위쪽으로 갈수록 원래 가중치에 부드럽게 연결하며, 정점 위치·면·UV·노멀·바인드 포즈는 보존한다. 생성된 메시가 이미 있으면 다시 덮어쓰지 않는다. 지형 사본의 손 여유 간격은 16mm이며, 기존 Pose 사본의 기본 8mm는 유지한다.

`LittleMungTerrainTests`는 실제 스킨 정점으로 지면 관통과 접지 중 프레임별/누적 이동을 측정한다. 경사면 정지, 경사로·계단 양방향 걷기·달리기와 손·전완 간격을 검사한다. `LittleMungPlayerTests(Terrain)`은 평지 걷기·달리기·점프·정지와 기존 Avatar 검사를 동일하게 실행한다. `LittleMungTerrainAssetTests`는 원본과 새 메시의 형상·UV·본 연결이 같은지 확인한다.

전후 렌더는 PlayMode 검사에 `-terrainCaptureOutput <폴더>`를 추가하면 생성된다. `before`는 기존 Pose 사본, `after`는 지형 사본이다. 같은 입력 경로의 정면 사선·측면 PNG와 실제 정점 측정 CSV를 저장한다. API나 사용자 화면 조작은 사용하지 않는다.

검증 지형은 Sandbox의 12도 고정 경사면과 단차 0.1m 계단이다. 접지 중 위치 고정과 발을 들어 옮기는 동작을 구분해 확인한다. 움직이는 발판·불규칙 지형·임의의 급회전까지 검증한 공통 IK 시스템은 아니다. 팔 관통은 머리·몸통 타원체에 대한 근사 검사와 렌더를 함께 확인하며 모든 삼각형의 무관통을 보증하지 않는다. Blender에서 신체 비율·신발·관절을 바꾸면 밑창 가중치 사본과 발바닥 기준 방향을 다시 만들고 같은 검사를 실행해야 한다.

## 다음 디딤면을 계획하는 계단 보행 사본

`LittleMungStairPlayerSandbox`는 기존 Pose 사본에서 분리한 플레이어 검수 씬이다. 이전 Terrain 실험의 접지 컴포넌트는 사용하지 않는다. 원본 모델, Pose 씬, 공유 PlayerMover, Avatar와 걷기·달리기·점프 클립은 유지한다.

- 씬: `Assets/Scenes/Sandbox/LittleMungStairPlayerSandbox.unity`
- 프리팹: `Assets/Prefabs/Player/LittleMung/LittleMungStairPlayer.prefab`
- 설정: `Assets/Data/Player/LittleMung/StairLocomotion.asset`
- 생성 메뉴: Tools → A305 → Player → Build Predictive Stair Sandbox

씬을 열고 Play한 뒤 왼쪽 계단을 WASD로 오르내린다. 계단 위에는 마지막 단에서 낙하하지 않고 보행을 마칠 수 있는 상단 발판이 있다. 자동 전후 비교에서도 이전 Pose 사본에 같은 발판을 실행 중에만 추가하며 기존 씬 파일은 저장하지 않는다.

`LittleMungStairLocomotion`은 전방 단차를 감지하고 신발 밑면이 들어갈 수 있는 평평한 디딤면을 찾는다. 발을 먼저 들어 올린 뒤 모서리를 넘어 이동하고, 디딤면 안쪽에서 내려놓는다. 디딘 발의 위치는 월드 좌표로 유지한다. 골반은 두 발의 지지 위치와 다리 길이를 고려해 이동하며, 내려가기에서는 아래 단으로의 체중 이동을 미리 시작한다. 골반 이동 변화량을 제한하여 충돌 캡슐이 단차를 넘는 순간 시각 모델이 함께 튀는 현상을 줄인다.

평지에서는 기존 Pose 동작으로 전환하고 점프 시 계단 보행을 해제한다. `LittleMungGroundContact` → 계단 보행 → `LittleMungArmClearance` 순서로 최종 자세를 계산한다. 새 사본은 형상·UV가 같은 기존 밑창 가중치 사본(`TerrainSkin/Sole0.asset`)을 재사용하며, 손 간격 설정은 새 프리팹에만 적용한다. 발목에 고정 각도를 더해 뒤집힘을 숨기는 보정은 추가하지 않는다.

팔 관통 보정은 전완의 실제 스킨 정점과 팔꿈치–손목 축 사이의 최대 거리를 두께로 사용한다. 외형 배율이 달라져도 고정 반경 때문에 전완 표면을 놓치지 않도록 매 프레임 측정한다. 손·팔 목표를 이동시키며 기존 본 길이와 손목 회전은 유지한다.

설정 에셋의 보폭, 발 들기 높이, 몸 높이 보간 시간, 전방 탐색 거리는 이 리틀 멍 체형용 값이다. 다른 체형에 그대로 적용하는 공통 보행 규격으로 취급하지 않는다.

### 검수 범위

`LittleMungStairTests`는 60fps 고정 간격에서 높이 0.1m의 네 단을 양방향으로 걷는다. 입력·속도·지형·카메라 조건을 맞춘 Pose/계단 사본의 렌더와 실제 스킨 정점 측정을 남긴다.

- 충돌 캡슐이 다음 단에 올라서기 전 착지 위치를 선택하는지
- 발을 들어 모서리를 넘기는 궤적과 디딤면 안쪽 착지
- 골반의 상하·앞뒤·좌우 이동 변화량
- 신발의 지면 관통, 접지 높이, 접지 중 프레임별 이동
- 손·전완과 몸통·머리 사이의 근사 간격
- 계단 사본의 평지 걷기·달리기·점프 및 기존 이동 기능 회귀

자동 검사는 외관 합격을 대신하지 않는다. 발의 미끄러짐 수치는 프레임별 이동이며 완전한 무미끄러짐을 보증하지 않는다. 손 관통 검사는 스킨 정점과 몸통·머리 타원체 사이의 근사 검사이고 모든 삼각형 사이의 충돌 검사는 아니다. 빠른 계단 달리기, 계단 위 급회전, 움직이는 발판, 임의의 단높이·발판 폭까지 검증한 범용 보행 시스템은 아니다. 계단 전용 보행은 평평한 디딤면을 대상으로 하며 경사면 발바닥 기울기 보정은 포함하지 않는다.

## 선택 캐릭터의 캡슐과 카메라 높이 맞추기

`Data/Player/Characters/F.asset`의 `Height Meters`, `Radius Meters`가 여성 충돌 캡슐을 정한다. `Camera Eye Height Meters`는 1인칭·대화 눈높이, `Camera Pivot Height Meters`는 3인칭 조준점이다. 0이면 씬의 `PlayerCameraSettings` 값을 사용하므로 M 대체 모델과 기존 Sandbox는 기존 설정을 유지한다. 거리·회전·줌·벽 충돌 설정은 계속 씬에서 참조한 카메라 데이터 에셋을 따른다. 눈높이·조준점은 실행 중에도 설정 에셋의 값을 읽으며, 초기 거리·각도는 Play를 다시 시작하면 적용된다.

**Tools → A305 → Player → Fit Female Capsule And Camera**는 Female 프리팹 복사본에 `Idle.anim`을 샘플링해 실제 스킨 정점을 측정한다. 여성 정의의 캡슐·카메라 높이를 다시 계산하고 공용 Player와 Production 네 씬의 캡슐 연결을 갱신한다. 외형 프리팹의 배율·메시·골격, 남성 정의와 씬 카메라 에셋은 바꾸지 않는다. 얼굴 중앙을 기본 눈높이로 사용하므로 최종 시점은 F 설정에서 조절한다. 단순한 Connect Selected Character는 이 계산을 반복하지 않고 이미 설정한 값을 유지한다.

`PlayerCharacterIntegrationTests`는 F/M의 실제 이동·대화·씬 전환과 함께, 여성 배율 1.6 유지·대기 메시 높이/머리 폭에 대한 캡슐 범위·얼굴 높이의 대화 시점·3인칭 조준점을 검사한다. 손·전완의 머리/몸통 간격 합격 기준은 타원체 정규화 거리 0.9 이상이다. 이는 근사 관통 검사이며 전체 삼각형 충돌이나 모든 체형·모션의 외관 품질을 보증하지 않는다.
