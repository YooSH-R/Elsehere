# 리틀 멍 행인 검수 사본

Tripo 생성 원본과 마스터 연결 결과를 보존하고, 서비스의 대두 비율과 Base 입 메시를 적용한 검수용 사본입니다. 운영 NPC 등록이나 TTS 연결을 수행하지 않습니다.

## Unity에서 확인

`frontend/UnityClient` 프로젝트에서 다음 경로를 엽니다.

- 얼굴: `Assets/Scenes/Sandbox/NpcReview/<이름>_Face.unity`
- 이동: `Assets/Scenes/Sandbox/NpcReview/<이름>_Motion.unity`
- 사본 프리팹: `Assets/Prefabs/Npc/Review/<이름>.prefab`
- 수정 메시: `Assets/Art/Characters/ReviewCorrections/ServiceNpcs/<이름>/`

대상은 `Tripo/NPC/<이름>/MasterRig/<이름>_Visual.prefab`이 있는 캐릭터를 자동으로 찾습니다. 기존 행인 5종과 `LittleMung_Cafe_Male`, `LittleMung_Grandfather`, `LittleMung_HotelStaff_Female`, `LittleMung_Police_Female`, `LittleMung_Police_Male`의 검수본이 있습니다. 얼굴 씬을 실행하면 기존 `FacePosePreviewPanel`에서 표정과 입 모양을 선택할 수 있습니다. 이동 씬은 공통 Player 이동 검수 구성을 사용합니다.

검수 사본은 외관 승인 전까지 운영 씬에 연결하지 않습니다. 운영 씬에 연결한 사본은 비즈니스맨(마을 행인 Ben, `TownScene`), 흑인 남성(드럭스토어 직원 Alex, `ShopScene`), 중년 남성(택시 기사 Joe, `TownScene` 택시 운전석)입니다([Npc 모듈](../../../UnityClient/Assets/Scripts/Npc/README.md#builder)). 세 사본 모두 입 모양·미소·눈 감기 채널을 갖춰 대화 중 립싱크·눈 깜박임을 씁니다. 자동 검사 결과가 실패한 사본도 문제를 살펴볼 수 있도록 검수 씬만 제공합니다. 씬이 존재한다는 사실은 합격을 뜻하지 않습니다.

## 비율과 골격

`CharacterHeadProportions.Match`는 기준 `LittleMungAdjusted.fbx`의 머리 높이/전신 높이 비율에 맞춰 Head 가중치가 있는 정점을 Neck 위치 기준으로 확대합니다. 머리 측정에는 머리카락도 포함됩니다. 기준 비율은 약 0.6055이며, 머리 형태 자체를 모두 동일하게 만드는 것은 아닙니다.

본의 계층·위치·회전, Avatar, 스킨 가중치와 bind pose는 유지합니다. 기존 블렌드셰이프의 위치 변화량도 머리 확대와 함께 조정합니다. 새 메시만 저장하므로 생성 원본의 정점이나 텍스처는 바뀌지 않습니다. 공통 서비스 크기에 따라 남성 사본은 1.7m, 여성·할머니 사본은 1.6m로 정규화합니다.

이 기능은 검수 사본 제작 단계에 연결되어 있습니다. 기본 Tripo 생성 버튼에서 새 캐릭터에 자동 적용되는 단계는 아닙니다.

## 입 메시와 재편집

검수 캐릭터는 Base의 입술·입안·치아·혀 형상을 각 얼굴의 입 위치와 피부색에 맞춥니다. 중립은 기본 메시이며 다음 11개 변화 채널을 사용합니다.

- 미소 좌/우: `mouthSmileLeft`, `mouthSmileRight`
- 발음: `viseme_PP`, `viseme_AA`, `viseme_E`, `viseme_OH`, `viseme_OU`, `viseme_FF`, `viseme_TH`, `viseme_I`, `viseme_EU`

Base 눈 이식 단계에서 `eyeBlinkLeft/Right`를 더해 총 13개 공통 채널을 사용합니다. 입 제작만 끝낸 중간 결과에는 눈 감기가 없으므로 얼굴 검수 패널에서 선택할 수 없습니다. 공통 채널을 지원하는 것과 실제 운영 NPC의 음성 재생에 연결되는 것은 별도 단계입니다.

각 캐릭터의 `Face/<이름>_Face.blend`에는 수정 가능한 Shape Key가 있습니다. `Surface.json`은 머리 비율 적용 후 표면, `Face/FaceShapes.json`은 Unity로 가져올 메시·변화량입니다. 원본 모델이 변경되면 예전 JSON을 재사용하지 않습니다. 가져오기에서 정점 불일치를 검사합니다.

오프라인 재제작 예:

```powershell
blender --background --python frontend/ArtSources/Characters/ServiceNpcs/fit_npc_mouth.py -- LittleMung_MiddleAge_Male
```

이 스크립트는 해당 ArtSources 폴더의 입 제작 결과를 다시 씁니다. 승인된 수동 수정이 있다면 먼저 별도 보관합니다. 피부색·입 위치 자동 검출은 저작 보조 도구이며, 다른 얼굴을 무조건 지원하지는 않습니다. 새 캐릭터는 정면·양쪽 사선과 전체 입 모양을 다시 확인해야 합니다.

`NPC_REVIEW_OUTPUT`을 제작 데이터 루트로 지정하고 `ServiceNpcReviewBuilder`의 Unity 배치 메서드를 사용합니다. `Prepare`는 비율 적용 사본, `BuildFaces`는 얼굴 메시와 검수 씬, `ValidateAndScenes`는 골격·동작 검사, `RenderFaces`는 표정 렌더를 만듭니다. 기존 얼굴 사본은 자동 덮어쓰지 않으며 명시적 새 경로 또는 검토된 갱신이 필요합니다. 비즈니스맨은 입을 먼저 만든 사본을 비율에 맞춰 확대한 결과이며, 그 결과인 `Prefabs/Npc/Review/LittleMung_Business_Male.prefab`이 준비본을 겸합니다. 몸·입 메시와 입 재질, Idle·이동 컨트롤러는 `ServiceNpcs/LittleMung_Business_Male/`에 모여 있습니다. `Prepare`는 이 프리팹이 있으면 비율만 확인하고, 없으면 이전 사본을 다시 만들지 않고 중단합니다. `BuildFaces`는 비즈니스맨을 건너뜁니다.

## 검사 범위와 승인 기준

- `CharacterHeadProportionTests`: 작은 머리 사본의 비율을 맞추면서 몸 정점·본·가중치를 보존하는지 확인합니다.
- `TripoMasterRig`: 마스터 골격과 Avatar 연결을 검사합니다.
- `TripoMasterMotionAudit`: 공통 Idle·Walk·Run·Rise·Fall·Land의 각 25포즈를 샘플링합니다. 손·전완과 머리·몸통의 근사 영역 거리는 기존 0.9 기준을 유지합니다.
- 정면·사선 외관, 입 경계·피부색, 손 모양·옷 주름과 전체 메시의 교차, 지형 접지는 사람이 추가 확인합니다. 거리 검사 통과만으로 자연스러운 움직임이나 모든 관통 해소를 보장하지 않습니다.

`LittleMungArmClearance`의 추가 표면 보정은 기본적으로 꺼져 있습니다. `ConfigureSurfaceContact`는 기존 보정으로 검사에 실패한 NPC 검수 사본에서만 추가 보정을 시험하고, 같은 0.9 기준에 통과했을 때 해당 프리팹에 설정을 저장합니다. 기존 플레이어 프리팹에는 적용하지 않습니다. 소매·전완의 실제 표면이 머리·몸통에 남으면 상완과 전완을 작은 각도로 추가 보정하며 손목의 로컬 회전과 본 위치는 유지합니다. 이 검사는 전체 옷의 삼각형 교차나 지형 접지를 보장하지 않습니다. 운영 등록은 외관 검수 후 별도로 수행합니다.

## 새 NPC 일괄 저작

`ServiceNpcBatchBuilder`는 기존 도구를 순서대로 호출하는 로컬 배치 진입점입니다. Tripo 생성·리깅 요청이나 운영 NPC 등록을 실행하지 않습니다.

1. `NPC_REVIEW_OUTPUT`을 이 저작 폴더의 절대 경로로 지정하고 `Prepare`로 리틀 멍 머리 비율을 맞춥니다. 머리 높이는 머리카락·모자를 포함하므로 얼굴 자체의 너비와 이마·턱 비율은 화면에서도 비교합니다.
2. 캐릭터별 `fit_npc_mouth.py -- <이름>`으로 기존 Base 입을 맞춥니다. `BuildMouthsAndExportEyes`는 아직 없는 얼굴 사본만 만들고 눈 입력을 내보냅니다. `NPC_BASE_EYES_OUTPUT`은 `BaseEyes`의 절대 경로입니다.
3. `measure_base_eye_fit.py -- <BaseEyes/이름>`으로 해당 캐릭터만 측정합니다. 기존 홍채 색과 위치를 확인한 뒤 `create_business_eyelids.py -- <BaseEyes/이름>`으로 Base 눈·눈꺼풀을 맞춥니다. `EyeFit.json`의 선택적 `patchScale`은 눈 전체 크기가 아닌 피부 연결 범위입니다.
4. `ApplyEyes`로 아직 눈 감기가 없는 프리팹을 갱신합니다. 명시적 재저작은 `NPC_BASE_EYE_NAMES`에 세미콜론으로 이름을 지정하고 `RefreshSelectedEyes`를 사용합니다. 같은 프리팹·메시 GUID를 갱신하며 새 후보 프리팹을 늘리지 않습니다.
5. `AuditFaces`에서 골격·13개 채널·실제 이동 클립을 확인합니다. `NPC_RENDER_OUTPUT`을 저장소 밖 경로로 지정하면 `RenderBefore`·`RenderAfter`가 동일 클립 비교 이미지와 리틀 멍 기준 모델을 저장합니다. 동작 비교 기준은 골격 원본 FBX가 아니라 기존 팔 보정·컨트롤러가 연결된 `Assets/Prefabs/Player/Characters/Female.prefab`입니다. 전신 비교 렌더는 비율 비교를 위해 화면상 키를 동일하게 정규화하며 실제 프리팹 키를 바꾸지 않습니다.

눈 접합부는 고정된 타원 대신 원래 얼굴 경계 안에 눈꺼풀 테두리를 맞춥니다. 입 접합 면을 보존하고 눈 경계에서 원래 피부 텍스처로 전환합니다. 연결되지 않은 경계는 저장 전에 실패 처리합니다. 정면·양쪽 사선에서 눈썹, 피부 이음새, 깜박임을 추가로 확인해야 합니다.

`ServiceNpcReviewTests`는 검색된 모든 검수 프리팹의 머리 비율·마스터 골격·표정 채널·공통 동작을 검사합니다. `BusinessBlinkPlaybackTests`는 실제 프레임 진행 중 눈을 깜박여도 입 가중치가 유지되는지 검사합니다. 미리보기 씬은 기존 로컬 음성·발음 시간표와 `MannequinSpeechRenderer`로 검수할 수 있으며 새 음성 API 호출이 필요하지 않습니다.

## 눈 외관 복원

Base 눈 이식 전 중년 남성·중년 여성·흑인 남성 검수 프리팹은 생성 원본의 눈 재질을 사용했습니다. 현재 검수본은 아래 Base 눈 이식 결과를 사용합니다. 머리 비율과 Base 입 메시를 유지하면서 눈 재질만 복원하며, `ServiceNpcEyeReviewBuilder.RestoreOriginalEyes`로 실행합니다.

`CharacterColor.png`, `Face/<이름>_SmallerIris.blend`, `resize_iris.py`의 홍채만 축소한 방식은 채택하지 않습니다. 흰자를 넓게 보이게 만들어 원래 인상을 해쳤으므로 현재 검수 프리팹에는 연결하지 않습니다. 해당 파일은 이전 시안을 보존하기 위한 자료이며 재적용 대상으로 사용하지 않습니다.

눈 크기 검수는 원본 리틀 멍의 눈 전체 크기·세로로 긴 눈동자·위아래 흰자 비율·음영을 함께 비교합니다. 수치 검사 통과를 외관 승인으로 취급하지 않습니다.

Blender 저작 스크립트·검수 씬 생성과 재질 복원은 시각 에셋 제작이므로 이미지 및 실제 Unity 결과로 검증합니다. 머리 비율 계산 변경은 실패를 재현하는 테스트를 먼저 실행하고 통과를 확인하는 방식으로 검증합니다.

## 눈 전체 크기 사본

Base 눈 이식의 입력 사본은 중년 남성·중년 여성·흑인 남성의 원본 눈 텍스처를 유지하면서 눈 전체 형상을 약 14% 줄인 `Face/EyeShape/FaceSurface.asset`을 사용합니다. 홍채만 줄이거나 흰자를 새로 칠하지 않습니다. 눈 주변의 피부 전환 영역은 부드럽게 연결합니다.

입 메시의 고정 접합부가 변형 영역과 겹치는 경우 `SpeakingLips.asset`도 사본으로 만듭니다. 세분화된 입 경계는 변형한 몸 메시의 원래 삼각형 모서리에 선형 보간하여 맞춥니다. 입 모양에 따라 움직이는 정점은 변경하지 않으며, UV·삼각형·본·가중치·블렌드셰이프 변화량을 유지합니다. 깊이 방향의 갑작스러운 범위 제한과 서로 다른 메시 경계의 비선형 변형은 면 찌그러짐과 틈의 원인이므로 사용하지 않습니다.

편집 가능한 결과는 `WholeEyes/<이름>/<이름>_WholeEyes.blend`, Unity 입력은 같은 폴더의 `EyeSurface.json`입니다. `resize_whole_eyes.py`는 이 세 캐릭터의 기존 `Surface.json`·눈 영역 측정·입 메시 자료로 사본을 제작하는 로컬 저작 스크립트입니다. 새로운 캐릭터를 무조건 지원하는 생성 파이프라인 단계는 아닙니다.

`NPC_REVIEW_OUTPUT`을 `WholeEyes` 데이터 루트로 지정한 뒤 Unity 배치 메서드 `ServiceNpcEyeShapeBuilder.Build`를 실행해 적용합니다. 기존 `Face/EyeShape` 결과가 있으면 자동 덮어쓰기를 중단합니다. 현재 얼굴 정점과 입력이 다르거나 변경 영역이 발음용 정점에 닿으면 적용하지 않습니다.

시각 에셋 변경이므로 별도의 동작 로직 테스트를 추가하지 않고 정면·양쪽 사선, Unity 입 모양 렌더, 기존 골격·동작 검사 및 정적 검사로 확인합니다. 눈 감기 채널은 이 작업에서 추가하지 않습니다.

## 비즈니스맨 눈 감기와 자동 깜박임

현재 통합 검수 프리팹 `Assets/Prefabs/Npc/Review/LittleMung_Business_Male.prefab`은 Base의 눈·위/아래 눈꺼풀·눈꺼풀 선과 25·50·75·100 가중치의 깜박임을 재사용한다. 저작 원본은 `LittleMungMannequin/MannequinFaceShapes.json`과 좌우 `EyeColor.png`다. 비즈니스맨의 눈 위치·크기에 맞춰 옮기고 작업용 얼굴 메시에서 기존 눈 표면을 잘라 연결한다. 얼굴 경계의 모든 정점을 연결하며 미연결 모서리가 있으면 저작 결과를 저장하지 않는다. 입·눈썹·골격은 유지한다. 생성 원본과 Base 자산은 수정하지 않는다.

눈 색은 Base의 갈색 홍채를 사용한다. 다른 눈 색이 필요하면 눈 텍스처의 홍채 영역만 별도로 편집하고 흰자·동공·반사광은 유지한다. 피부나 눈 전체 머티리얼을 틴트하지 않는다. 비즈니스맨은 검수된 고정 적합 값을 사용한다. 다른 행인 검수본은 아래의 측정 자료를 사용한다.

`CharacterIdleBlink`는 대기 중 기존 `SpeechExpressionProfile`의 타이밍으로 눈 가중치만 갱신한다. 미소·입·관절 보정 가중치는 유지하며, 상위 `CharacterSpeechPlayer`가 재생 또는 일시 정지 중이면 얼굴 제어를 양보한다. 얼굴 검수 씬에서는 `Automatic blink`를 켜서 자동 동작을 확인하고, 끄면 눈 감기 버튼과 강도 슬라이더로 정밀 검수한다. 음성 중 깜박임은 기존 음성 재생기의 감정 프로필 연결을 사용한다.

대기 중 프레임이 완전히 닫히는 짧은 구간을 건너뛰면 두 프레임 사이의 최대 눈 감기 값을 한 프레임에 반영한다. 0.25초보다 큰 시간 이동은 보간하지 않는다. 입 가중치와 음성 재생기의 얼굴 제어는 바꾸지 않는다.

저작 자료는 `frontend/ArtSources/Characters/LittleMungBusiness/Blink/`에 있다. Blender 백그라운드에서 `create_business_eyelids.py -- <저작 폴더 절대 경로>`로 실행한다. Unity에서는 `BUSINESS_BLINK_OUTPUT`을 같은 폴더로 지정하고 `BusinessBlinkBuilder.Apply`를 실행한다. 기존 눈꺼풀을 갱신하려면 `BUSINESS_BLINK_REFRESH=1`을 명시한다. 새 프리팹을 만들지 않고 해당 검수 프리팹의 눈꺼풀 자산을 같은 GUID로 갱신한다. 직접 편집한 결과에는 내용을 확인한 뒤 실행한다.

눈꺼풀의 피부 경계·음영과 닫힌 눈의 인상은 정면·양쪽 사선 외관 검수 대상이다. 눈꺼풀 제작은 시각 에셋 변경으로 실제 Unity 렌더와 메시 검사로 확인하며, 눈 전용 제어와 자동 타이밍은 실패를 재현하는 테스트 및 실제 프리팹 재생 테스트로 검증한다. 마을 행인 Ben은 이 프리팹을 쓰므로 대화 중 립싱크·눈 깜박임이 함께 적용된다.

## 나머지 행인의 Base 눈 이식

대상은 `Npc/Review`의 중년 남성·중년 여성·흑인 청년 남성·할머니 프리팹이다. 기존 프리팹을 갱신하며 이전 후보와 Tripo 원본은 유지한다. 중년 남녀의 파란 홍채와 흑인 청년·할머니의 짙은 갈색 홍채는 현재 텍스처에서 측정한다. Base 눈 텍스처의 홍채 색 영역만 바꾸고 흰자·동공·반사광은 유지한다. 여성과 할머니는 Base 속눈썹도 사용한다.

저작 폴더는 `ServiceNpcs/BaseEyes/<캐릭터 이름>`이다. `NPC_BASE_EYES_OUTPUT`에 `BaseEyes` 절대 경로를 지정해 `BusinessBlinkBuilder.ExportRemaining`으로 현재 메시를 읽는다. 기존 `Surface.json`은 덮어쓰지 않는다. Blender에서 `measure_base_eye_fit.py -- <BaseEyes 폴더>`로 위치·크기·홍채 색을 측정한 뒤 `EyeFit.json`을 검수한다. `create_business_eyelids.py -- <캐릭터 저작 폴더>`로 Base 부위를 이식하고 `BusinessBlinkBuilder.ApplyRemaining`으로 반영한다. 직접 편집한 기존 눈은 명시적인 `BUSINESS_BLINK_REFRESH=1` 없이 갱신하지 않는다.

기존 입 메시의 접합부에 닿는 얼굴 면은 잘라내지 않는다. 할머니의 안경처럼 눈 주변에 있는 분리된 표면도 보존한다. 이식 경계의 미연결 모서리, 현재 몸 메시와 저작 원본 불일치는 적용 실패로 처리한다. 자동 측정은 현재 네 검수본에 맞춰 확인하는 저작 보조 기능이며, 새로 생성하는 모든 캐릭터의 외관을 보장하지 않는다.

각 `Assets/Scenes/Sandbox/NpcReview/<캐릭터 이름>_Face.unity`에서 `Automatic blink`와 입 모양 버튼으로 확인한다. 서비스 씬에서는 흑인 청년 남성이 드럭스토어 직원 Alex(`ShopScene`), 중년 남성이 택시 기사 Joe(`TownScene` 택시 운전석)로 연결되어 대화 중 립싱크·눈 깜박임을 쓴다. 중년 여성과 할머니는 서비스 씬에 연결하지 않았다.

## 눈 접합부의 그림자 검수

Base 눈·눈꺼풀·접합 피부는 원래 얼굴과 같은 그림자 수신·생성 설정을 사용한다. 눈 부위만 그림자 수신을 끄면 야외에서 머리카락·나무·건물 그림자가 얼굴에 걸릴 때 이식한 영역만 밝게 남는다. 그림자 없는 얼굴 검수 씬만으로 승인하지 않는다.

기존 눈 에셋의 조명 설정만 갱신하려면 `BUSINESS_BLINK_OUTPUT`을 저장소 밖 검수 폴더로 지정하고 `BusinessBlinkBuilder.RepairEyeLighting`을 실행한다. 검수 프리팹 5종과 해당 눈 재질만 갱신하며 메시·텍스처·눈색·블렌드셰이프는 유지한다. 같은 조명과 임시 그림자 물체로 `LightingBefore`·`LightingAfter` 정면·사선 렌더를 저장한다. 임시 조명과 물체는 씬에 저장하지 않는다. `BaseEyeLightingTests`가 기존 얼굴과 눈 부위의 그림자 설정 불일치를 검사한다. 미세한 피부 색·노멀 경계는 별도의 외관 검수 대상이다.
