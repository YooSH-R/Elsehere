# 존박 얼굴 저작 사본

Unity 전용 리듬 모델의 눈·입 부분에 공통 Base 모듈을 맞춘 데이터입니다. 원본 Tripo FBX, 마스터, 리듬 FBX의 메시와 골격을 변경하지 않습니다. 완성 자산은 기존 `Assets/Prefabs/Npc/LittleMung_JohnPark_Male.prefab` 하나에 연결합니다.

## 재제작

1. Unity의 `JohnParkFaceBuilder.ExportSource`로 원본 FBX 메시를 `Surface.json`에 내보냅니다. 기존 파일이 있으면 덮어쓰지 않고 중단합니다.
2. Blender background에서 상위 폴더의 `author_john_park_face.py`를 실행합니다. 이 저작 폴더의 결과는 다시 쓰므로 수동 편집 결과는 먼저 보관합니다.
3. Unity `JohnParkFaceValidation.ApplyAndValidate`로 메시·재질·프리팹·확인 씬을 생성하고 얼굴과 리듬 검사를 실행합니다.

FBX의 메시 로컬축과 공통 저작 도구의 정면축이 다릅니다. `JohnParkFaceBuilder`는 모델·메시의 변환 행렬로 -X 정면에 맞춰 내보내고, 적용할 때 역변환합니다. 본 변환이나 원본의 정면을 바꾸지는 않습니다.

`Face`에는 입술·구강·치아·혀의 9발음과 좌우 미소, `Eyes`에는 기존 갈색 홍채와 크기를 유지한 Base 눈·눈꺼풀이 있습니다. 독립된 원본 안구 2개만 연결성분으로 특정하여 제거합니다. 눈꺼풀 내부의 원본 틈은 교체 영역에 포함하고, 바깥 피부 경계는 반드시 닫힌 고리여야 합니다. `EyeFit.json`의 `patchScale: 1.3`은 입 연결 면과 겹치지 않게 하는 피부 범위이며 안구 크기 설정이 아닙니다. `replaceEyeOpenings`는 교체 영역 내부의 기존 열린 면 경계를 새 피부 연결 대상으로 오인하지 않게 합니다. 다른 캐릭터에는 기본적으로 적용하지 않습니다.

좌우 피부 연결 모서리의 불일치가 있으면 제작을 중단합니다. Unity에서는 표정의 실제 정점 변화, 몸·의상 정점과 전체 가중치·bind pose 보존, 표정·리듬 동시 재생 중 앰프 고정을 검사합니다. 정면·사선과 모든 발음은 렌더로 별도 확인합니다.

이는 시각 에셋 저작으로 테스트 우선 적용에서 제외하며, 새 서비스 런타임 로직·외부 API·의존 패키지는 추가하지 않습니다. 자동 수치 검사는 외관 검수나 운영 대화 연결 확인을 대체하지 않습니다.

## 공통 감정과 NPC 대화

기본 13채널(9발음·좌우 미소·좌우 깜빡임)에 공통 감정 14채널과 폭소용 입을 추가합니다. 감정 프로필과 제어 코드는 기존 NPC와 공유하고, 본·중립·발음 델타는 유지합니다. 사용 방법과 운영 배치 조건은 [말하기 표정 문서](../../../../../docs/unity/npc-speaking-emotions.md#존박-공연-캐릭터)를 따릅니다.

1. `JohnParkEmotionBuilder.Export`로 현재 얼굴의 `Emotions/Source.json`을 내보냅니다. 이미 있으면 보존하고 중단합니다.
2. 공통 `ServiceNpcEmotions/author_emotions.py`에 이 Source, 새 `--work`, 새 `--output` 폴더를 전달합니다.
3. 상위 폴더의 `protect_john_park_emotion_boundaries.py`에 `--source Emotions/Source.json --input <공통 저작 출력> --output Emotions`를 전달합니다. 얼굴 피부·눈썹을 연결성분으로 구별하고 모자·머리카락·의상·소품의 감정 델타를 고정합니다. 기존 검수 출력은 바뀌므로 수동 수정분을 먼저 보존합니다.
4. `prepare_john_park_dialogue_review.py --output frontend/UnityClient/Assets/Audio/Characters/JohnParkDialogue`는 저장 음성·발음 시간표에 검수 감정 메타데이터를 포장합니다. 원본 PCM을 검증하며 새 합성·API 호출은 하지 않습니다.
5. `JohnParkDialogueBuilder.BuildAndValidate`로 같은 프리팹에 적용하고 검수 씬과 렌더를 만듭니다. 자동 검사 결과와 실제 정면·사선 렌더를 함께 확인합니다.

`BoundaryProtection.json`에는 얼굴·눈썹과 보호 정점 목록, `Landmarks.json`에는 얼굴 좌표 변환, `.blend`에는 공통 규격의 Shape Key가 있습니다. 검수 WAV는 로컬 예문 음성으로, 실제 존박의 목소리가 아닙니다.

## 저장 재생 안내

상위 폴더의 `create_john_replay_guide.py --model-dir <로컬 Kokoro 모델 폴더>`는 설치된 ONNX 모델과
`voices-v1.0.bin`만 사용해 안내를 합성합니다. `ReplayGuide/Guide.json`에 영문·번역·목소리·합성 속도·
발음 시간표·PCM 해시를 기록하고, Unity `ReplayGuide.bytes`에 음성·음소·감정을 함께 포장합니다.
새 외부 음성 API를 호출하지 않으며 기존 랩 음성은 바꾸지 않습니다.
`RecordedPerformanceUiBuilder.Build`로 이 안내와 기존 랩을 `OrderRap.asset`, 공용 대화창의 전용
버튼에 연결합니다. 뜻 보기는 이 에셋의 번역을 재사용합니다.
