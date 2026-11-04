# 호텔리어 표정과 대화

호텔리어 원본 `Assets/Prefabs/Characters/Tripo/LittleMung_HotelStaff_male.prefab`을 보존하고, `Assets/Prefabs/Npc/LittleMung_HotelStaff_male_Dialogue.prefab`에 표정 복사본을 구성한다. 외형 기준 높이는 머리카락부터 신발 밑까지 Avatar와 기본 동작을 적용한 메시 실측 1.7m다. 애니메이션 중 무릎 굽힘·머리 흔들림에 따른 순간 높이는 다를 수 있다.

원래 몸 메시의 정점·UV·가중치·바인드 포즈와 골격·Animator Controller를 유지한다. 복사본의 입 주변 면만 입술·구강으로 교체하고, 치아·혀·닫힌 눈꺼풀 선은 Head 본에 연결한다. 발음 9개와 미소·눈 감기 4개로 구성된 공통 13개 채널은 [카페 얼굴 규격](cafe-female-face.md#다른-npc의-자동-연결)을 따른다. 중립은 기존의 작은 미소를 기준으로 하며 머리카락·눈 크기·코·옷은 바꾸지 않는다.

## Unity에서 확인

- `Assets/Scenes/Sandbox/HotelierFacePreview.unity`: Play 후 입모양·미소·눈 감기와 강도, 정면·사선을 선택한다.
- `Assets/Scenes/Production/HotelScene.unity` 및 `Assets/Scenes/Sandbox/HotelInteriorSandbox.unity`: 기존 HOTEL_STAFF의 외형에 복사본을 연결한다. NPC 위치·대화 코드·이동 연결을 유지하고 캡슐 높이를 1.7m, 시선 높이를 모델 눈 위치에 맞춘다.
- `Tools → A305 → Hotel → Connect Dialogue Face`: 준비된 표정 복사본 연결을 다시 맞춘다. 기존 Hotel Builder도 표정 복사본이 있으면 이를 사용한다.
- `Tools → A305 → Characters → Check NPC Speech Support`: 현재 씬의 공통 표정 채널 누락 여부를 확인한다.

실제 대화 시작 시 NpcIdentity의 공통 자동 연결이 해당 NPC의 얼굴·표정 프로필·전용 AudioSource를 준비한다. 서버 음성은 기존 DialogueServerLink와 같은 WAV 검증·발음 시간표 경로를 사용한다. 모델에 특정 예문을 넣지 않으며, 새 대사도 음성과 일치하는 시간표가 있으면 재생한다. 운영 서버 모델·WebGL 검증 여부는 배포 과정에서 별도로 확인한다.

## 같은 음성으로 비교 렌더

`HOTEL_FACE_WAV`에 발음 시간표가 포함된 기존 WAV 경로, `HOTEL_FACE_OUTPUT`에 저장소 밖 출력 폴더를 지정한다. Editor가 닫힌 상태에서 `A305.EditorTools.Characters.HotelierFaceBuilder.RenderSpeechComparison`을 batchmode로 실행한다. Hotelier와 Cafe 폴더에 같은 음성을 같은 시간으로 샘플링한 30fps 프레임과 검증 JSON이 저장된다. 두 영상에 같은 WAV를 넣어 비교하며 음성 생성이나 외부 API 호출은 하지 않는다.

얼굴 인상, 완전히 닫힌 눈의 윤곽, F/V 치아 접촉과 TH 혀의 노출은 정면과 사선에서 최종 외관 검수가 필요하다. 자동 검사는 시각적 완성도를 보장하지 않는다.

크기 검사는 `BakeMesh(mesh, true)`로 Renderer 배율을 보정한 뒤 월드 좌표로 변환하고, 본 행렬·바인드 포즈를 직접 계산한 결과와도 대조한다. 영상용 메시 역시 배율을 중복 적용하지 않는다. [Unity BakeMesh 문서](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/skinnedmeshrenderer/bakemesh)의 useScale 옵션을 참고한다.
