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

`Assets/Scenes/Sandbox/Mannequin/LittleMung_Base_Female_Face.unity`를 열고 Play하면 중립·미소·깜박임과 발음별 입 모양을 확인할 수 있습니다. 공용 Visual을 쓰는 1.6m·1.7m 프리팹은 같은 얼굴 메시와 채널을 공유합니다. 이 검수 씬은 카페·호텔 Production 대화 연결 여부를 증명하지 않습니다.

- `LeftEye`, `RightEye`는 고정된 눈 표면입니다. 깜박임으로 흰자·눈동자를 압축하지 않습니다.
- 위·아래 눈꺼풀과 `LeftLashes`, `RightLashes`가 `eyeBlinkLeft`, `eyeBlinkRight`의 25·50·75·100 프레임을 사용합니다. 눈썹은 깜박임에 참여하지 않습니다.
- `CharacterFaceController.ApplySpeech`는 입모양과 미소·깜박임을 별도 채널로 합성합니다. 치아·혀는 실제 구강 안에 두고 입술이 가리도록 하므로, 약한 발음에서도 구강 벽 뒤로 숨지 않습니다.
- `SpeechVisemeTimeline.SampleArticulation`은 전달된 발음 시각을 사용합니다. `ay`는 아→이, `y` 다음 `uw`는 우 준비 동작으로 연결합니다. 메시만 교체해도 잘못된 발음 시각이 자동으로 수정되지는 않습니다.

Blender 제작 소스는 `frontend/ArtSources/Characters/LittleMungMannequin/`에 있습니다. `create_mannequin_face.py`와 `build_mannequin_eyes.py`는 이 모델의 얼굴 위치를 기준으로 만든 도구입니다. 임의의 신규 NPC 얼굴에 그대로 적용하는 자동 리토폴로지 도구가 아닙니다. 원본 Tripo 폴더를 보존하고 Common 복사본의 얼굴 면·텍스처·셰이프만 교체합니다.

제작한 JSON을 반영할 때는 `MannequinMasterBuilder.RefreshFaceShapes`를 사용합니다. 본·가중치·바인드포즈를 유지하며, 이미 있는 부품의 정점 수가 바뀌면 수작업 손상을 방지하기 위해 중단합니다. 기존 원본에 수작업 변경이 있다면 새로 생성하기 전에 별도 사본으로 보존해야 합니다.

`MannequinSpeechRenderer.Render`는 기존 WAV와 해당 음성의 발음 JSON을 받아 검수 이미지를 만듭니다. `MANNEQUIN_SPEECH_AUDIO`, `MANNEQUIN_SPEECH_TIMELINE`, `MANNEQUIN_SPEECH_OUTPUT`을 지정하고, 느린 비교가 필요하면 `MANNEQUIN_SPEECH_FPS=120`을 사용합니다. JSON의 `audioSha256`은 WAV 전체 파일과 일치해야 합니다. 이 도구는 음성 합성이나 외부 API 호출을 하지 않으며, 0.5초 대기 후 발화를 렌더하므로 영상에 음성을 합칠 때도 같은 지연을 적용해야 합니다.
