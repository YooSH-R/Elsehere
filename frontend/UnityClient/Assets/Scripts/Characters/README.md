# Characters 모듈

플레이어와 NPC가 함께 쓰는 캐릭터 표현을 담당합니다. `CharacterAnimationDriver`는 이동 상태를 Animator에 전달하고, `PoseCorrectiveDriver`는 관절 자세에 맞게 모델의 보정 블렌드셰이프를 적용합니다.

도형으로 개발하는 동안에는 Animator가 없으므로 아무것도 하지 않습니다. 모델과 애니메이션을 붙일 때 이동 코드를 고치지 않고 Animator Controller만 준비하면 되도록 하기 위함입니다. 다른 모듈과 게임 코드를 참조하지 않습니다.

## 모델을 붙일 때

1. 캐릭터 루트의 자식에 모델을 두고, 모델에 Animator와 Animator Controller를 연결합니다.
2. 캐릭터 루트의 `CharacterAnimationDriver`에 그 Animator를 연결합니다. 비워 두면 자식 객체에서 찾습니다.
3. Animator Controller에 아래 파라미터를 만들고 상태 전환에 사용합니다.

| 파라미터 | 형식 | 값 |
| --- | --- | --- |
| `Speed` | Float | 수평 이동 속도(m/s) |
| `IsGrounded` | Bool | 땅에 닿아 있는지 |
| `VerticalSpeed` | Float | 수직 속도(m/s). 양수는 올라가는 중, 음수는 떨어지는 중 |

Controller에 없는 파라미터는 전달하지 않으며, 시작할 때 Console에 경고로 알립니다.

## 사용하는 쪽

`PlayerMover`가 매 프레임 `SetLocomotion(수평 속도, 땅에 닿음, 수직 속도)`를 호출합니다. NPC 이동을 구현할 때도 같은 메서드로 상태를 전달합니다.

## 관절·의상 보정

`PoseCorrectiveProfile`에는 모델 루트 기준 본·렌더러 경로, 블렌드셰이프 이름, 기준 로컬 회전, 회전축과 보정 각도를 설정합니다. 기준 회전은 애니메이션 중간 자세가 아닌 해당 메시의 기본 자세에서 기록합니다. 이 프로필은 모델의 골격과 셰이프에 종속되므로 다른 모델에 그대로 재사용하지 않습니다.

`PoseCorrectiveDriver`는 Animator 평가 이후 `LateUpdate`에서 0~100의 보정값을 적용합니다. 무릎처럼 회전 크기를 사용하는 보정과 허벅지 앞뒤처럼 부호를 구분하는 보정을 지원합니다. 추가 관절 조건(gate)을 지정하면 두 보정값 중 작은 값을 사용해 웅크림처럼 복합 자세에만 적용할 수 있습니다. 연결한 셰이프만 제어하며 표정 등 나머지 값은 유지합니다. 비활성화하면 연결한 보정값을 0으로 되돌립니다.

Tripo 프리팹 Builder는 모델 옆에 `<모델 파일명>.correctives.asset`이 있으면 새 프리팹의 모델 인스턴스에 드라이버를 연결합니다. 원본 모델과 이미 존재하는 프리팹은 수정하지 않습니다. 본이나 셰이프가 없으면 설정 단계에서 오류를 내므로 모델을 교체하거나 골격을 바꾼 뒤에는 프로필을 다시 검증해야 합니다.

Blender 드라이버 수식은 FBX에 자동 이식되지 않습니다. 모델별 프로필과 블렌드셰이프를 함께 배포해야 합니다. 관절 보정은 관통을 자동 탐지하는 물리 시스템이 아니며, 새 애니메이션·리타게팅·크기 설정에는 별도 외형 검수가 필요합니다.

## 공통 마네킹 얼굴 검수

`Assets/Scenes/Sandbox/Mannequin/LittleMung_Base_Male_Face.unity`를 열고 Play하면 중립·미소·깜박임과 발음별 입 모양을 확인할 수 있습니다. 공용 Visual을 쓰는 1.6m·1.7m 프리팹은 같은 얼굴 메시와 채널을 공유합니다. 이 검수 씬은 카페·호텔 Production 대화 연결 여부를 증명하지 않습니다.

- `LeftEye`, `RightEye`는 고정된 눈 표면입니다. 깜박임으로 흰자·눈동자를 압축하지 않습니다.
- 위·아래 눈꺼풀과 `LeftLashes`, `RightLashes`가 `eyeBlinkLeft`, `eyeBlinkRight`의 25·50·75·100 프레임을 사용합니다. 눈썹은 깜박임에 참여하지 않습니다.
- 입술은 `SpeakingLips`의 피부 면에 만든 부드러운 굴곡입니다. 모델 좌표에서 폭 0.008m 범위에 윗입술 최대 0.0022m·아랫입술 최대 0.0028m의 볼륨을 주고 입꼬리에서는 약하게 만듭니다. 같은 피부 색상에 넓게 퍼지는 명도 차이를 더하며, 별도 색상 띠나 선명한 외곽선은 사용하지 않습니다. 입 벌림 경계와 바깥 피부는 계속 이어집니다. 이전 색상 띠 `LipRim`은 갱신할 때 제거합니다.
- 입술과 원본 얼굴 경계 사이에는 발화 중 고정된 완충 면을 둡니다. 불규칙한 바깥 경계는 제약 삼각분할로 연결하고, 안쪽은 입술에서 일정한 거리로 배치합니다. 미소는 얇은 입꼬리의 면이 접히지 않도록 곡선 변형을 나누어 적용하며, 음영 법선은 실제 곡면의 접선으로 계산합니다. 입꼬리 공간이 좁을 때는 굴곡 폭을 줄이고 최대 볼륨은 유지합니다.
- `CharacterFaceController.ApplySpeech`는 입모양과 미소·깜박임을 별도 채널로 합성합니다. 치아 노출이 필요한 이·E·F/V·TH에서는 해당 치아를 구강 앞쪽에 배치해 약한 발음도 보이도록 합니다. 오·우 등 치아를 드러내지 않는 발음에서는 별도 셰이프로 구강 안쪽에 숨깁니다. 윗니와 아랫니의 노출 발음은 구분하며, 입술 색·두께와 발음 시간표는 바꾸지 않습니다.
- `SpeechVisemeTimeline.SampleArticulation`은 전달된 발음 시각을 사용합니다. `ay`는 아→이, `y` 다음 `uw`는 우 준비 동작으로 연결합니다. 메시만 교체해도 잘못된 발음 시각이 자동으로 수정되지는 않습니다.

Blender 제작 소스는 `frontend/ArtSources/Characters/LittleMungMannequin/`에 있습니다. `create_mannequin_face.py`와 `build_mannequin_eyes.py`는 이 모델의 얼굴 위치를 기준으로 만든 도구입니다. 임의의 신규 NPC 얼굴에 그대로 적용하는 자동 리토폴로지 도구가 아닙니다. 원본 Tripo 폴더를 보존하고 Common 복사본의 얼굴 면·텍스처·셰이프만 교체합니다.

제작한 JSON을 반영할 때는 `MannequinMasterBuilder.RefreshFaceShapes`를 사용합니다. 본·가중치·바인드포즈를 유지하며, 이미 있는 부품의 정점 수가 바뀌면 수작업 손상을 방지하기 위해 중단합니다. 기존 원본에 수작업 변경이 있다면 새로 생성하기 전에 별도 사본으로 보존해야 합니다.

입 안쪽의 검은 테두리처럼 보이는 표현을 피하려고 `MouthInterior`는 따뜻한 적갈색을 사용합니다. 제작 JSON의 색을 다시 적용할 때는 `MannequinMasterBuilder.RefreshAuthoredMouthColor`를 명시적으로 실행합니다. 일반 얼굴 메시 갱신은 수작업 머티리얼 수정을 덮어쓰지 않습니다.

`MannequinSpeechRenderer.Render`는 기존 WAV와 해당 음성의 발음 JSON을 받아 검수 이미지를 만듭니다. `MANNEQUIN_SPEECH_AUDIO`, `MANNEQUIN_SPEECH_TIMELINE`, `MANNEQUIN_SPEECH_OUTPUT`을 지정하고, 느린 비교가 필요하면 `MANNEQUIN_SPEECH_FPS=120`을 사용합니다. `MANNEQUIN_SPEECH_YAW`는 -60~60도 사선 검수 각도이며 생략하면 기존 정면을 사용합니다. `MANNEQUIN_SPEECH_SCENE`으로 다른 검수 씬을 지정할 수 있으며 기본값은 남자 Base 얼굴 씬입니다. 인스턴스의 피부색 PropertyBlock도 렌더에 복사합니다. 저장된 씬 카메라는 변경하지 않습니다. JSON의 `audioSha256`은 WAV 전체 파일과 일치해야 합니다. 이 도구는 음성 합성이나 외부 API 호출을 하지 않으며, 0.5초 대기 후 발화를 렌더하므로 영상에 음성을 합칠 때도 같은 지연을 적용해야 합니다.

## 남자 Base와 NPC Variant

검수한 마네킹은 `LittleMung_Base_Male`로 관리합니다. `Art/Characters/Tripo/LittleMung_Base_Female`은 제작 출처인 원본이므로 이름과 내용을 보존합니다. 외형·표정의 공통 편집 대상은 아래 Common Base입니다. 기존 플레이어 선택 데이터나 Production NPC를 이 마네킹으로 교체하지는 않습니다.

| 용도 | Assets 기준 경로 |
| --- | --- |
| 공통 외형 Base | `Art/Characters/Common/LittleMungMannequin/LittleMung_Base_Male_Visual.prefab` |
| 1.6m·1.7m 플레이어 검수 | `Prefabs/Characters/Mannequin/LittleMung_Base_Male_160.prefab`, `LittleMung_Base_Male_170.prefab` |
| NPC 외형 Variant 예제 | `Prefabs/Characters/Mannequin/Npc/LittleMung_Npc_Male.prefab` |
| NPC 표정·피부색 검수 | `Scenes/Sandbox/Mannequin/LittleMung_Npc_Male_Face.unity` |
| NPC 외형의 이동 검수 | `Scenes/Sandbox/Mannequin/LittleMung_Npc_Male_Motion.unity` |

새 NPC 외형은 Project 창에서 공통 외형 Base를 선택한 뒤 **Create → Prefab Variant**로 만듭니다. Variant의 Inspector에서 **Character Skin Tone → Skin Color**를 바꾸면 몸·위아래 눈꺼풀·입 주변에 함께 적용됩니다. 기본 기준색은 `#FFC799`이며, 텍스처에 이 기준색 대비 색상 비율을 적용합니다. 공유 머티리얼을 직접 변경하지 않으므로 다른 NPC의 색에는 영향을 주지 않습니다. 표정 가중치와 Animator 재생 상태도 각 인스턴스가 독립적으로 가집니다.

`CharacterSkin` 셰이더의 피부 마스크는 현재 마네킹의 몸 텍스처에서 기본복·어두운 눈썹을 제외합니다. 눈꺼풀과 입 주변은 전부 피부로 처리하며, 눈·속눈썹·치아·구강 재질은 바꾸지 않습니다. 몸 텍스처를 교체하면 해당 텍스처에 맞는 마스크를 별도로 검수해야 합니다. 새 피부 부품은 Base의 `CharacterSkinTone.skinRenderers`에 등록하고 같은 셰이더와 적절한 마스크를 사용합니다.

이 NPC 프리팹은 **외형용 Variant**입니다. 서비스의 NPC 이동·대화 루트에 Visual 자식으로 연결하는 대상이며, 그 자체에 AI 대화나 이동 판단은 없습니다. 상속된 발 접지·팔 보정은 부모의 `CharacterController`를 사용합니다. 이동 검수 씬은 기존 플레이어 이동 루트를 이용해 이 조건을 제공하고, 얼굴 전용 씬에서는 이동 보정 컴포넌트를 씬에서만 제거합니다.

Base의 메시·골격 참조·표정 컴포넌트·애니메이션 연결 변경은 Variant와 씬 인스턴스의 **오버라이드되지 않은 항목**에 전파됩니다. NPC별 피부색·의상처럼 자식이 명시적으로 바꾼 값은 유지됩니다. Unpack하거나 메시·Controller 참조를 다른 에셋으로 교체하면 해당 연결은 상속하지 않습니다. 배포된 실행 파일에는 변경 후 빌드·배포가 필요하며, 런타임에 생성된 객체를 원격으로 갱신하는 기능은 아닙니다.

`Tools → A305 → Characters → Prepare Male Base And NPC Variant`는 검수 복사본의 이름을 GUID 유지 방식으로 정리하고 색상 연결·예제 Variant·검수 씬을 준비합니다. 이미 있는 Variant와 이동 검수 씬의 사용자 수정은 덮어쓰지 않습니다. 메시·골격·표정 데이터를 새로 생성하거나 Tripo API를 호출하지 않습니다.
