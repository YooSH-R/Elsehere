# 리틀 멍 행인 검수 사본

Tripo 생성 원본과 마스터 연결 결과를 보존하고, 서비스의 대두 비율과 Base 입 메시를 적용한 검수용 사본입니다. 운영 NPC 등록이나 TTS 연결을 수행하지 않습니다.

## Unity에서 확인

`frontend/UnityClient` 프로젝트에서 다음 경로를 엽니다.

- 얼굴: `Assets/Scenes/Sandbox/NpcReview/<이름>_Face.unity`
- 이동: `Assets/Scenes/Sandbox/NpcReview/<이름>_Motion.unity`
- 사본 프리팹: `Assets/Prefabs/Npc/Review/<이름>.prefab`
- 수정 메시: `Assets/Art/Characters/ReviewCorrections/ServiceNpcs/<이름>/`

이름은 `LittleMung_Business_Male`, `LittleMung_MiddleAge_Male`, `LittleMung_MiddleAge_Female`, `LittleMung_BlackYoung_Male`, `LittleMung_Grandmother`입니다. 얼굴 씬을 실행하면 기존 `FacePosePreviewPanel`에서 표정과 입 모양을 선택할 수 있습니다. 이동 씬은 공통 Player 이동 검수 구성을 사용합니다.

검수 사본은 외관 승인 전까지 운영 씬에 연결하지 않습니다. 비즈니스맨 사본은 마을 행인 Ben의 외형으로 `TownScene`에 연결했습니다([Npc 모듈](../../../UnityClient/Assets/Scripts/Npc/README.md#builder)). 자동 검사 결과가 실패한 사본도 문제를 살펴볼 수 있도록 검수 씬만 제공합니다. 씬이 존재한다는 사실은 합격을 뜻하지 않습니다.

## 비율과 골격

`CharacterHeadProportions.Match`는 기준 `LittleMungAdjusted.fbx`의 머리 높이/전신 높이 비율에 맞춰 Head 가중치가 있는 정점을 Neck 위치 기준으로 확대합니다. 머리 측정에는 머리카락도 포함됩니다. 기준 비율은 약 0.6055이며, 머리 형태 자체를 모두 동일하게 만드는 것은 아닙니다.

본의 계층·위치·회전, Avatar, 스킨 가중치와 bind pose는 유지합니다. 기존 블렌드셰이프의 위치 변화량도 머리 확대와 함께 조정합니다. 새 메시만 저장하므로 생성 원본의 정점이나 텍스처는 바뀌지 않습니다. 공통 서비스 크기에 따라 남성 사본은 1.7m, 여성·할머니 사본은 1.6m로 정규화합니다.

이 기능은 검수 사본 제작 단계에 연결되어 있습니다. 기본 Tripo 생성 버튼에서 새 캐릭터에 자동 적용되는 단계는 아닙니다.

## 입 메시와 재편집

새 네 캐릭터는 Base의 입술·입안·치아·혀 형상을 각 얼굴의 입 위치와 피부색에 맞춥니다. 중립은 기본 메시이며 다음 11개 변화 채널을 사용합니다.

- 미소 좌/우: `mouthSmileLeft`, `mouthSmileRight`
- 발음: `viseme_PP`, `viseme_AA`, `viseme_E`, `viseme_OH`, `viseme_OU`, `viseme_FF`, `viseme_TH`, `viseme_I`, `viseme_EU`

`eyeBlinkLeft/Right`는 이 사본에서 제작하지 않았습니다. 얼굴 검수 패널은 지원되지 않는 눈 감기를 선택할 수 없게 표시합니다. 공통 입 모양 이름을 지원하는 것과 실제 NPC 음성 재생에 연결되는 것은 별도 단계입니다.

각 캐릭터의 `Face/<이름>_Face.blend`에는 수정 가능한 Shape Key가 있습니다. `Surface.json`은 머리 비율 적용 후 표면, `Face/FaceShapes.json`은 Unity로 가져올 메시·변화량입니다. 원본 모델이 변경되면 예전 JSON을 재사용하지 않습니다. 가져오기에서 정점 불일치를 검사합니다.

오프라인 재제작 예:

```powershell
blender --background --python frontend/ArtSources/Characters/ServiceNpcs/fit_npc_mouth.py -- LittleMung_MiddleAge_Male
```

이 스크립트는 해당 ArtSources 폴더의 입 제작 결과를 다시 씁니다. 승인된 수동 수정이 있다면 먼저 별도 보관합니다. 피부색·입 위치 자동 검출은 이 네 입력에 맞춰 검수한 보조 도구이며, 다른 얼굴을 무조건 지원하지는 않습니다. 새 캐릭터는 정면·양쪽 사선과 전체 입 모양을 다시 확인해야 합니다.

`NPC_REVIEW_OUTPUT`을 제작 데이터 루트로 지정하고 `ServiceNpcReviewBuilder`의 Unity 배치 메서드를 사용합니다. `Prepare`는 비율 적용 사본, `BuildFaces`는 얼굴 메시와 검수 씬, `ValidateAndScenes`는 골격·동작 검사, `RenderFaces`는 표정 렌더를 만듭니다. 기존 얼굴 사본은 자동 덮어쓰지 않으며 명시적 새 경로 또는 검토된 갱신이 필요합니다. 비즈니스맨은 입을 먼저 만든 사본을 비율에 맞춰 확대한 결과이며, 그 결과인 `Prefabs/Npc/Review/LittleMung_Business_Male.prefab`이 준비본을 겸합니다. 몸·입 메시와 입 재질, Idle·이동 컨트롤러는 `ServiceNpcs/LittleMung_Business_Male/`에 모여 있습니다. `Prepare`는 이 프리팹이 있으면 비율만 확인하고, 없으면 이전 사본을 다시 만들지 않고 중단합니다. `BuildFaces`는 비즈니스맨을 건너뜁니다.

## 검사 범위와 승인 기준

- `CharacterHeadProportionTests`: 작은 머리 사본의 비율을 맞추면서 몸 정점·본·가중치를 보존하는지 확인합니다.
- `TripoMasterRig`: 마스터 골격과 Avatar 연결을 검사합니다.
- `TripoMasterMotionAudit`: 공통 Idle·Walk·Run·Rise·Fall·Land의 각 25포즈를 샘플링합니다. 손·전완과 머리·몸통의 근사 영역 거리는 기존 0.9 기준을 유지합니다.
- 정면·사선 외관, 입 경계·피부색, 손 모양·옷 주름과 전체 메시의 교차, 지형 접지는 사람이 추가 확인합니다. 거리 검사 통과만으로 자연스러운 움직임이나 모든 관통 해소를 보장하지 않습니다.

할머니는 낙하 동작에서 팔·손 영역이 검사 기준에 못 미치는 문제가 남아 있어 운영 등록 대상에서 제외합니다. 가중치·동작을 추가 검토한 뒤 같은 기준으로 재검사해야 합니다. 여유 거리만 늘리거나 검사 합격 기준을 낮추어 통과시키지 않습니다.

## 눈 외관 복원

중년 남성·중년 여성·흑인 남성 검수 프리팹은 생성 원본의 눈 재질을 사용합니다. 머리 비율과 Base 입 메시를 유지하면서 눈 재질만 복원하며, `ServiceNpcEyeReviewBuilder.RestoreOriginalEyes`로 실행합니다.

`CharacterColor.png`, `Face/<이름>_SmallerIris.blend`, `resize_iris.py`의 홍채만 축소한 방식은 채택하지 않습니다. 흰자를 넓게 보이게 만들어 원래 인상을 해쳤으므로 현재 검수 프리팹에는 연결하지 않습니다. 해당 파일은 이전 시안을 보존하기 위한 자료이며 재적용 대상으로 사용하지 않습니다.

눈 크기 검수는 원본 리틀 멍의 눈 전체 크기·세로로 긴 눈동자·위아래 흰자 비율·음영을 함께 비교합니다. 수치 검사 통과를 외관 승인으로 취급하지 않습니다.

Blender 저작 스크립트·검수 씬 생성과 재질 복원은 시각 에셋 제작이므로 이미지 및 실제 Unity 결과로 검증합니다. 머리 비율 계산 변경은 실패를 재현하는 테스트를 먼저 실행하고 통과를 확인하는 방식으로 검증합니다.

## 눈 전체 크기 사본

중년 남성·중년 여성·흑인 남성의 현재 검수 프리팹은 원본 눈 텍스처를 유지하면서 눈 전체 형상을 약 14% 줄인 `Face/EyeShape/FaceSurface.asset`을 사용합니다. 홍채만 줄이거나 흰자를 새로 칠하지 않습니다. 눈 주변의 피부 전환 영역은 부드럽게 연결합니다.

입 메시의 고정 접합부가 변형 영역과 겹치는 경우 `SpeakingLips.asset`도 사본으로 만듭니다. 세분화된 입 경계는 변형한 몸 메시의 원래 삼각형 모서리에 선형 보간하여 맞춥니다. 입 모양에 따라 움직이는 정점은 변경하지 않으며, UV·삼각형·본·가중치·블렌드셰이프 변화량을 유지합니다. 깊이 방향의 갑작스러운 범위 제한과 서로 다른 메시 경계의 비선형 변형은 면 찌그러짐과 틈의 원인이므로 사용하지 않습니다.

편집 가능한 결과는 `WholeEyes/<이름>/<이름>_WholeEyes.blend`, Unity 입력은 같은 폴더의 `EyeSurface.json`입니다. `resize_whole_eyes.py`는 이 세 캐릭터의 기존 `Surface.json`·눈 영역 측정·입 메시 자료로 사본을 제작하는 로컬 저작 스크립트입니다. 새로운 캐릭터를 무조건 지원하는 생성 파이프라인 단계는 아닙니다.

`NPC_REVIEW_OUTPUT`을 `WholeEyes` 데이터 루트로 지정한 뒤 Unity 배치 메서드 `ServiceNpcEyeShapeBuilder.Build`를 실행해 적용합니다. 기존 `Face/EyeShape` 결과가 있으면 자동 덮어쓰기를 중단합니다. 현재 얼굴 정점과 입력이 다르거나 변경 영역이 발음용 정점에 닿으면 적용하지 않습니다.

시각 에셋 변경이므로 별도의 동작 로직 테스트를 추가하지 않고 정면·양쪽 사선, Unity 입 모양 렌더, 기존 골격·동작 검사 및 정적 검사로 확인합니다. 눈 감기 채널은 이 작업에서 추가하지 않습니다.

## 비즈니스맨 눈 감기와 자동 깜박임

현재 통합 검수 프리팹 `Assets/Prefabs/Npc/Review/LittleMung_Business_Male.prefab`은 Base의 눈·위/아래 눈꺼풀·눈꺼풀 선과 25·50·75·100 가중치의 깜박임을 재사용한다. 저작 원본은 `LittleMungMannequin/MannequinFaceShapes.json`과 좌우 `EyeColor.png`다. 비즈니스맨의 눈 위치·크기에 맞춰 옮기고 작업용 얼굴 메시에서 기존 눈 표면을 잘라 연결한다. 얼굴 경계의 모든 정점을 연결하며 미연결 모서리가 있으면 저작 결과를 저장하지 않는다. 입·눈썹·골격은 유지한다. 생성 원본과 Base 자산은 수정하지 않는다.

눈 색은 Base의 갈색 홍채를 사용한다. 다른 눈 색이 필요하면 눈 텍스처의 홍채 영역만 별도로 편집하고 흰자·동공·반사광은 유지한다. 피부나 눈 전체 머티리얼을 틴트하지 않는다. 현재 스크립트의 위치·크기 적합 값은 비즈니스맨용이며 다른 NPC에 일괄 적용하는 범용 자동 맞춤은 아니다.

`CharacterIdleBlink`는 대기 중 기존 `SpeechExpressionProfile`의 타이밍으로 눈 가중치만 갱신한다. 미소·입·관절 보정 가중치는 유지하며, 상위 `CharacterSpeechPlayer`가 재생 또는 일시 정지 중이면 얼굴 제어를 양보한다. 얼굴 검수 씬에서는 `Automatic blink`를 켜서 자동 동작을 확인하고, 끄면 눈 감기 버튼과 강도 슬라이더로 정밀 검수한다. 음성 중 깜박임은 기존 음성 재생기의 감정 프로필 연결을 사용한다.

저작 자료는 `frontend/ArtSources/Characters/LittleMungBusiness/Blink/`에 있다. Blender 백그라운드에서 `create_business_eyelids.py -- <저작 폴더 절대 경로>`로 실행한다. Unity에서는 `BUSINESS_BLINK_OUTPUT`을 같은 폴더로 지정하고 `BusinessBlinkBuilder.Apply`를 실행한다. 기존 눈꺼풀을 갱신하려면 `BUSINESS_BLINK_REFRESH=1`을 명시한다. 새 프리팹을 만들지 않고 해당 검수 프리팹의 눈꺼풀 자산을 같은 GUID로 갱신한다. 직접 편집한 결과에는 내용을 확인한 뒤 실행한다.

눈꺼풀의 피부 경계·음영과 닫힌 눈의 인상은 정면·양쪽 사선 외관 검수 대상이다. 눈꺼풀 제작은 시각 에셋 변경으로 실제 Unity 렌더와 메시 검사로 확인하며, 눈 전용 제어와 자동 타이밍은 실패를 재현하는 테스트 및 실제 프리팹 재생 테스트로 검증한다. 마을 행인 Ben은 이 프리팹을 쓰므로 대화 중 립싱크·눈 깜박임이 함께 적용된다. 다른 NPC의 눈 감기는 완료되지 않았다.
