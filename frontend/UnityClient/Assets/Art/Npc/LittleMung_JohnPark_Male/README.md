# 존박 전용 리듬 캐릭터

원본 Tripo 모델을 보존한 골격 복사본입니다. 마스터 본 67개의 이름과 부모 관계를 재사용하며, 관절의 휴식 위치는 앰프를 잡은 비대칭 자세에 맞췄습니다. 몸·왼팔·손목으로 리듬을 타고 오른팔은 가슴을 따라가며 잡는 자세를 유지합니다.

## Unity에서 확인

1. 이 저장소의 `frontend/UnityClient`를 Unity 6000.3.26f1로 엽니다.
2. Project에서 `Assets/Scenes/Sandbox/NpcJohnParkRhythmSandbox.unity`를 엽니다.
3. Play를 누릅니다. Game 화면 왼쪽은 정면, 오른쪽은 사선이며 같은 프리팹의 리듬 동작이 반복됩니다. Game 비율은 16:9로 맞춥니다.
4. 오른손과 앰프 손잡이가 벌어지지 않는지, 앰프 모양이 찌그러지지 않는지, 왼팔 소매와 반복 경계를 확인합니다.
5. 프리팹은 `Assets/Prefabs/Npc/LittleMung_JohnPark_Male.prefab` 하나를 사용합니다. 다른 씬에는 이 프리팹을 배치합니다.
6. 프리팹의 `Visual`을 선택하면 Animator의 Avatar와 Controller를 확인할 수 있습니다. 창에서 Controller를 열면 기본 상태 `Rhythm`에 `JohnPark_AmpRhythm`이 연결돼 있습니다.

## 에셋 연결

- 모델: 이 폴더의 `LittleMung_JohnPark_Male.fbx`.
- Avatar: FBX에서 생성된 **Generic Avatar**. 전용 본 동작을 그대로 재생합니다.
- Controller: `JohnParkRhythm.controller`. 기본 상태 한 개, 자동 반복, 루트 모션 꺼짐.
- 동작: FBX의 `JohnPark_AmpRhythm`, 24fps·4초. 끝에는 시작 자세와 같은 연결 프레임을 포함합니다.
- 재질: `LittleMung_JohnPark_Male.mat`, URP Lit, 4K BaseColor 참조.
- 오른손·앰프: 같은 `Right_Hand` 본의 가중치 100%. 일반 팔 애니메이션을 추가하면 잡는 자세를 별도로 유지해야 합니다.

## 재생 검사와 다시 만들기

`Tools → A305 → Npc → John Park → Validate Rhythm And Capture`는 Controller를 두 바퀴 평가해 Avatar, 잡기 영역의 강체 형태, 가슴 기준 오른손 자세, 왼팔·몸통 동작, 루트 이동, 루프 경계를 검사합니다. 메시를 굽는 동안 스케일을 중복 적용하지 않도록 `BakeMesh(mesh, true)`를 사용하며, 본·바인드 포즈의 스킨 변형 행렬과도 대조합니다.

검사 결과와 프레임은 환경변수 `JOHN_PARK_REVIEW_OUTPUT`의 폴더에 저장합니다. 지정하지 않으면 로컬 `artifacts/JohnParkRhythm/Unity`에 보관하며 Git에 추가하지 않습니다. 전체 PlayMode 검사가 아니라 이 프리팹의 Animator와 스킨에 대한 로컬 재생 검사입니다.

`Build Rhythm Sandbox`는 이 복사본의 가져오기 설정과 연결을 맞추고 확인 씬을 다시 만듭니다. 기존 프리팹의 외형·크기와 이미 만든 재질·Controller 편집 내용은 보존합니다. 사용자가 수정한 Sandbox 배치는 덮어쓰므로 필요하면 별도 씬으로 저장합니다. Unity를 닫은 상태에서는 `A305.EditorTools.Npc.JohnParkRhythmValidation.BuildAndValidate`를 batchmode `-executeMethod`로 실행할 수 있습니다.

## 적용 범위

프리팹·애니메이션 등 시각 에셋 구성으로 TDD 적용에서 제외하고 실제 Animator 재생·스킨 좌표 검사와 렌더로 검증합니다. 새 런타임 스크립트나 패키지는 필요하지 않습니다.

이 프리팹은 전용 리듬과 아래의 공통 얼굴 제어를 지원합니다. 공용 Humanoid 걷기·달리기 리타기팅, NPC 대화·생활 역할 배정, 운영 씬 배치는 포함하지 않습니다. 일반 몸 동작을 추가할 때는 오른팔을 덮어쓰지 않도록 애니메이션 레이어나 마스크를 별도로 설계해야 합니다. 전체 신체와 옷의 모든 관통을 자동으로 보장하는 검사는 아닙니다.

## 표정·입모양 확인

`Assets/Scenes/Sandbox/NpcJohnParkFaceSandbox.unity`를 열고 Play를 누릅니다. 왼쪽 표정 패널에서 기본·미소·눈 감기와 발음별 입 모양을 선택하고 강도를 조절할 수 있습니다. 자동 깜박임과 정면·사선 보기 버튼도 제공합니다. 같은 프리팹이므로 몸의 리듬과 오른손·앰프 연결도 함께 유지됩니다.

기존 Base 입술·구강·치아·혀와 눈·눈꺼풀 메시를 존박의 원래 위치·눈 크기·갈색 홍채에 맞췄습니다. 눈 영역과 입 영역만 새 메시로 연결하고 원본 FBX, 얼굴 외의 정점, 본·가중치·바인드 포즈는 보존합니다. 표정은 텍스처 교체가 아닌 실제 블렌드셰이프 변형입니다.

- 표정: `mouthSmileLeft/Right`, `eyeBlinkLeft/Right`.
- 발음: `viseme_AA`, `FF`, `TH`, `PP`, `E`, `OH`, `OU`, `I`, `EU`의 9개 채널.
- 제어: 기존 `CharacterFaceController`, 자동 눈 감기 `CharacterIdleBlink`.
- 음성 준비: 기존 `CharacterSpeechPlayer`와 `AudioSource`, `FriendlySpeechExpression` 연결. 서비스에서 음성과 발음 시간표를 공급하면 동일한 제어 경로로 재생합니다. 프리팹에는 임의의 대사나 존박 음성을 넣지 않습니다.

얼굴 데이터는 `frontend/ArtSources/Characters/ServiceNpcs/LittleMung_JohnPark_Male/`에 보관하며 제작 절차는 해당 폴더의 README를 참고합니다. `Tools → A305 → Npc → John Park → Apply Authored Face`는 이 프리팹의 저작 결과만 갱신하고 얼굴 Sandbox를 다시 만듭니다. 수동 편집한 메시·Sandbox는 먼저 별도 저장합니다.

`Validate Face And Capture`는 12가지 확인 자세(중립 포함)의 실제 정점 변형, 13채널 누락, 얼굴 외 정점·가중치 보존, 기존 로컬 음성 시간표 92프레임과 리듬의 동시 재생, 오른손·앰프 형태를 확인합니다. 표정 검수 결과는 `artifacts/JohnParkRhythm/Face`에 저장합니다. 검사용 음성은 기존 카페 여성 음성으로, 존박의 실제 목소리나 새로 합성한 음성이 아닙니다. 눈썹·볼·놀람 등 추가 감정 채널은 이번 범위에 포함하지 않습니다.
