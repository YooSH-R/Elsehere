# 카페 여자 표정 확인

Tripo 카페 여자 `Assets/Prefabs/Npc/Barista.prefab`을 보존한 표정 검수용 복사본이다. 운영 NPC를 자동 교체하지 않는다.

## Unity에서 확인

1. `Assets/Scenes/Sandbox/CafeFemaleFacePreview.unity`를 연다.
2. Play를 누른다.
3. 왼쪽 버튼에서 참고 이미지 순서의 입 모양 01~10을 선택한다. Smile(미소)과 Eyes closed(눈 감기)도 별도로 확인한다. 작은 창에서는 목록을 스크롤한다.
4. Strength 슬라이더로 0~100%를 확인하고 Left / Front / Right로 눈꺼풀·입술·치아·혀를 확인한다.
5. REST / SIL을 누르면 얼굴 변형을 모두 해제한다. 기존 관절 보정 채널은 변경하지 않는다.

검수 프리팹은 `Assets/Prefabs/Npc/CafeFemaleFacePreview.prefab`, 메시·구강 재질은 `Assets/Art/Npc/CafeFemaleFace/`에 있다. 프리팹은 원래 카페 NPC를 부모로 사용하고 원본 몸 메시의 정점·UV·가중치·바인드 포즈를 복사한다. 복사본의 입 주변 면은 별도 입술 루프와 안쪽 구강 면으로 교체한다. 기존 골격과 Avatar를 참조하며 얼굴 메시와 구강 세부 메시만 별도로 저장한다. 검수 프리팹에서는 이동·대화 컴포넌트를 제거하고, 검수 씬에서는 Animator를 꺼 얼굴을 고정해서 살펴본다. 원본 NPC의 컴포넌트는 바꾸지 않는다.

## 변형 구성

| 확인 모양 | 실제 Blend Shape | 구분 기준 |
| --- | --- | --- |
| 01 REST / SIL | 모든 얼굴 채널 0 | 원래 인상의 작은 휴식 미소 |
| 02 M/B/P | viseme_PP | 위·아래 입술이 닫힌 직선 이음선과 접촉 윤곽 |
| 03 AA/AH | viseme_AA | 가로로 둥글게 열린 입과 아래쪽 혀 |
| 04 E/EH | viseme_E | 가로로 열린 입과 윗니 |
| 05 OH | viseme_OH | 둥글게 모은 입 |
| 06 OU/OO | viseme_OU | OH보다 좁고 앞으로 모인 입 |
| 07 F/V | viseme_FF | 윗니와 올라온 아랫입술 |
| 08 TH | viseme_TH | 치아 사이 앞으로 나온 혀 |
| 09 이 | viseme_I | 가로 입과 위·아래 치아 |
| 10 으 | viseme_EU | 치아 노출 없이 좁고 가로로 열린 입 |
| 미소 | mouthSmileLeft, mouthSmileRight | 추가 감정 포즈 |
| 눈 감기 | eyeBlinkLeft, eyeBlinkRight | 추가 감정 포즈 |

같은 메시의 정점 위치를 변형한다. 눈 감기에는 열린 눈의 음영이 남지 않도록 법선 변화도 저장한다. 입술·구강 안쪽·위아래 치아·혀·닫힌 눈꺼풀 선은 Head 본에 연결한 별도 메시다. 입술과 구강은 중립에서도 연결되며, 치아·혀·눈꺼풀 선은 해당 포즈에서 나온다. M/B/P에는 닫힌 입술이 작은 화면에서도 읽히도록 얇은 접촉 윤곽 메시를 함께 변형한다. 표정 이미지를 교체하는 방식은 아니다.

입 벌림은 원래 미소를 아래로 늘리는 방식에서 교체했다. AA는 위·아래 입술이 벌어지는 윤곽과 구강 안쪽, 아래쪽 혀를 사용한다. F/V는 아랫입술을 올린 상태의 윗니 노출, TH는 치아 사이 혀를 구분한다. 첨부 기준은 `frontend/ArtSources/Characters/CafeFemaleFace/References/`에 보관한다. 첨부 이미지의 10종 모두 해당 복사본에 구성되어 있다. REST는 Basis이며 나머지 9종은 별도 발음 변형이다.

현재는 참고 이미지의 입 모양 10종과 미소·눈 감기 검수본이다. 음성에서 발음 시각을 추출하거나 TTS에 동기화하는 전체 립싱크 완료 상태로 간주하지 않는다. 특히 눈꺼풀 접힘과 F/V·TH의 입술 접촉은 정면·사선에서 외관을 확인해야 한다.

## Blender 원본과 재사용 범위

`frontend/ArtSources/Characters/CafeFemaleFace/CafeFemaleFacePreview.blend`에 Basis와 몸 기준 13개 Shape Key(발음 9개, 미소·눈 감기 4개)가 있다. Blender에서는 객체를 선택한 뒤 Object Data Properties의 Shape Keys에서 값을 조절한다. 이 파일은 얼굴 조형용 메시이며 몸의 기준 골격·가중치는 Unity 원본이 기준이다.

`create_face_shapes.py`는 Unity에서 추출한 정확한 정점 배열을 입력으로 별도 얼굴 후보를 만드는 로컬 제작 스크립트다. 현재 얼굴에 맞춘 위치 기준을 사용한다. `build_speaking_mouth.py`는 복사본의 입 주변 면과 전용 피부 색 텍스처를 구성한다. Unity는 명시적인 변형 법선도 함께 적용한다. Blender의 실시간 Shape Key 음영은 Unity와 차이가 있으므로 최종 외관은 Unity 검수 씬에서 확인한다. 사람이 편집한 blend를 같은 출력 경로로 재생성하지 않는다. `CafeFemaleFaceShapes.json`은 Unity가 읽는 변형 데이터이며 API 호출을 하지 않는다.

**Tools → A305 → Characters → Build Cafe Female Face Preview**는 아직 없는 검수 프리팹·씬을 만든다. 이미 있는 프리팹은 덮어쓰지 않는다. **Refresh Cafe Female Face Shapes**는 동일한 기준 정점 배열의 JSON으로 복사본 몸의 면 목록과 얼굴 채널, 동일한 정점 수의 얼굴 세부 메시를 갱신한다. 몸의 정점 순서나 기존 얼굴 세부 메시의 정점 수를 바꾸는 작업은 갱신 메뉴 범위에 포함되지 않는다.

다른 NPC는 같은 채널 이름으로 제작한 얼굴에 `CharacterFaceController`를 붙여 `SetPose`를 재사용할 수 있다. `BlendPoses(from, to, progress, intensity)`는 두 포즈 사이를 0~1 진행률로 전환하며, 기존 관절 보정 채널을 유지한다. 얼굴 크기·정점 순서가 다른 NPC에 이 모델의 변형 값을 그대로 복사해서는 안 된다. 필수 채널이 빠졌으면 부분 적용 대신 오류를 반환한다.

문장 GIF는 작성한 발음 시간표에 이 10종을 적용한 시안이다. 입 모양이 모두 있다는 사실과 실제 음성 동기화가 끝났다는 사실은 구분한다. 감정 상태 연결, TTS 재생, 음소 시간표에 따른 립싱크 재생, 전체 29개 채널의 파이프라인 검증은 별도 작업이다. 이 씬의 동작만으로 전체 표정·립싱크 검증을 통과했다고 처리하지 않는다.

`viseme_I`와 `viseme_EU`는 첨부된 한국어 이·으 기준이다. 기존 Tripo 전체 얼굴 검사의 `IH` 및 DD·KK·CH·SS·NN·RR, 추가 감정 채널과 자동으로 동일 취급하지 않는다. 전체 얼굴 검사 기준을 낮추거나 통과 처리하지 않는다.
