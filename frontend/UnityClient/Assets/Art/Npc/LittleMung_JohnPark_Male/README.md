# John 전용 리듬 캐릭터

게임 표시 이름은 John입니다. 기존 참조를 유지하기 위해 파일명과 내부 NPC 코드는 `LittleMung_JohnPark_Male`, `TOWN_JOHN_PARK`로 유지합니다.

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
- 음성 재생: 기존 `CharacterSpeechPlayer`와 `AudioSource`, `FriendlySpeechExpression` 연결. 일반 대화는 서비스의 음성과 발음·감정 시간표를 재생합니다. 저장 주문 랩은 `RecordedNpcPerformance`와 `OrderRap.asset`으로 재생하며, 실제 인물의 녹음이나 복제 음성은 사용하지 않습니다.

얼굴 데이터는 `frontend/ArtSources/Characters/ServiceNpcs/LittleMung_JohnPark_Male/`에 보관하며 제작 절차는 해당 폴더의 README를 참고합니다. `Tools → A305 → Npc → John Park → Apply Authored Face`는 이 프리팹의 저작 결과만 갱신하고 얼굴 Sandbox를 다시 만듭니다. 수동 편집한 메시·Sandbox는 먼저 별도 저장합니다.

`Validate Face And Capture`는 기본 얼굴 제작 단계의 12가지 확인 자세(중립 포함), 13채널, 얼굴 외 정점·가중치 보존과 오른손·앰프 형태를 확인합니다. 최종 프리팹은 추가 감정 변형까지 총 28채널을 사용하며, 공통 감정 16종은 `NpcJohnParkEmotionSandbox`, 대화 연결은 `NpcJohnParkDialogueSandbox`에서 확인합니다. 일반 대화의 검수용 음성은 기존 카페 여성 음성을 재사용하므로 실제 서비스의 남성 목소리와 구분합니다.

저장 랩은 남성 `am_onyx` 1.5배 음성, 121BPM, 약 8.1초입니다. 좌우 입꼬리가 같은 `warm_smile`과 음소 시간표를 사용하고 왼팔·몸의 리듬을 같은 재생 시각에 맞춥니다. 명확한 요청에는 저장 안내를 재생하고 John 전용 [랩 다시 듣기] 버튼을 강조합니다. 버튼으로 랩 시작·중단·반복이 가능하며 공용 [다시 듣기]는 마지막 안내나 대사를 재생합니다. 안내 원문·번역·음성·발음·감정은 `OrderRap.asset`과 `ReplayGuide.bytes`에 연결되어 재생 때 AI를 호출하지 않습니다. 사용 방법은 [말하기 표정 문서](../../../../../../docs/unity/npc-speaking-emotions.md#john의-저장-안내와-랩-버튼)를 따릅니다.

## 앰프를 든 하반신 걷기·달리기

`Locomotion/JohnLowerBodyWalk.anim`, `JohnLowerBodyRun.anim`은 전용 Generic 골격용 제자리 반복 클립입니다. 골반 위치와 양다리 회전만 저작하며, 원본 FBX·메시·본 길이·가중치·상반신 회전·얼굴 변형은 유지합니다. 걷기는 뒤꿈치 착지·발바닥 지지·발끝 밀어내기를 연결하고 발을 낮게 회수합니다. 달리기는 뒤쪽 발을 먼저 접어 회수하고 착지·도약에 맞춰 골반 높이를 바꿉니다. 신발의 실제 지지 정점과 다리 길이를 사용해 발 회전 중 접지점과 무릎의 도달 범위를 맞춥니다.

기존 John 프리팹에 연결한 `JohnParkLocomotion`은 공연의 오디오 기반 자세 평가 뒤 하반신만 합성합니다. 따라서 랩이 Animator 시간을 고정한 동안에도 다리가 움직이고, 오른손·앰프와 표정·립싱크는 기존 제어를 유지합니다. 정지하면 하반신 합성을 풀어 원래 리듬 자세로 돌아갑니다.

이동 바운스는 골반을 빠르게 낮춰 잠깐 눌렀다가 천천히 펴는 리듬 동작입니다. 걷기는 매 디딤, 달리기는 두 디딤에 한 번 눌러 빠른 떨림을 피합니다. 실제 발바닥 접점을 유지하면서 본 길이로 계산한 무릎 굽힘 120도와 다리 도달 범위 안에서 골반 높이를 제한합니다. 상반신은 골반의 상하 이동을 따라가고, 별도의 상반신 회전이나 원본 리듬·음성 시간표 변경은 없습니다. 정지 자세에는 적용하지 않습니다.

이동 호출부는 기존 `CharacterAnimationDriver.SetLocomotion(speed, true, 0)`을 재사용합니다. 기본 예시는 정지 `0`, 걷기 `1`, 달리기 `2`이며 조절값·클립 참조는 `Assets/Data/Npc/JohnPark/Locomotion.asset`에 있습니다. 중간 속도는 두 클립을 섞고 전환 시 위상을 이어갑니다. 실제 경로 이동·회전·지형 접지는 이동 시스템에서 처리해야 합니다. 클립 자체에 루트 이동이나 새 출퇴근 일과는 없으며, 식당 배치는 계속 고정됩니다.

**Tools → A305 → Npc → John Park → Build Lower Body Locomotion**은 전용 클립과 기존 프리팹 연결을 갱신합니다. 이 클립의 수작업 편집분은 갱신 전 보관합니다. 이미 있는 검수 씬의 배치와 속도 설정은 덮어쓰지 않습니다. `Assets/Scenes/Sandbox/NpcJohnParkLocomotionSandbox.unity`를 열고 Play하면 화면 왼쪽부터 달리기·걷기·정지를 비교합니다. **Validate Lower Body And Capture**는 실제 하반신 이동량·상반신 보존·발바닥 높이·반복 경계·무릎 과굴곡과 정면·사선·측면 렌더를 검사합니다. 출력은 로컬 `artifacts/JohnLocomotion/Visual`에 저장합니다. 수치 검사는 외관의 자연스러움을 대신하지 않으므로 디딤·회수·상하 움직임을 영상에서도 확인합니다. 계단·경사·실제 보행 경로에서의 검증과는 구분합니다.
