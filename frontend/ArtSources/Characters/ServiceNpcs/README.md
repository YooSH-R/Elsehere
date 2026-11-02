# 리틀 멍 행인 검수 사본

Tripo 생성 원본과 마스터 연결 결과를 보존하고, 서비스의 대두 비율과 Base 입 메시를 적용한 검수용 사본입니다. 운영 NPC 등록이나 TTS 연결을 수행하지 않습니다.

## Unity에서 확인

`frontend/UnityClient` 프로젝트에서 다음 경로를 엽니다.

- 얼굴: `Assets/Scenes/Sandbox/NpcReview/<이름>_Face.unity`
- 이동: `Assets/Scenes/Sandbox/NpcReview/<이름>_Motion.unity`
- 사본 프리팹: `Assets/Prefabs/Npc/Review/<이름>.prefab`
- 수정 메시: `Assets/Art/Characters/ReviewCorrections/ServiceNpcs/<이름>/`

이름은 `LittleMung_Business_Male`, `LittleMung_MiddleAge_Male`, `LittleMung_MiddleAge_Female`, `LittleMung_BlackYoung_Male`, `LittleMung_Grandmother`입니다. 얼굴 씬을 실행하면 기존 `FacePosePreviewPanel`에서 표정과 입 모양을 선택할 수 있습니다. 이동 씬은 공통 Player 이동 검수 구성을 사용합니다.

검수 사본은 외관 승인 전까지 운영 씬에 연결하지 않습니다. 자동 검사 결과가 실패한 사본도 문제를 살펴볼 수 있도록 검수 씬만 제공합니다. 씬이 존재한다는 사실은 합격을 뜻하지 않습니다.

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

`NPC_REVIEW_OUTPUT`을 제작 데이터 루트로 지정하고 `ServiceNpcReviewBuilder`의 Unity 배치 메서드를 사용합니다. `Prepare`는 비율 적용 사본, `BuildFaces`는 얼굴 메시와 검수 씬, `ValidateAndScenes`는 골격·동작 검사, `RenderFaces`는 표정 렌더를 만듭니다. 기존 얼굴 사본은 자동 덮어쓰지 않으며 명시적 새 경로 또는 검토된 갱신이 필요합니다. 비즈니스맨은 기존 입 제작 사본을 비율에 맞춰 확대한 결과를 사용합니다.

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
