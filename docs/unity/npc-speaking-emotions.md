# 말하기 중 NPC 감정 표정 검수

카페 남녀·호텔리어 남녀·비즈니스맨·중년 남녀·흑인 청년 남성·할머니·할아버지·경찰 남녀의 12종 외형에 공통 감정 표정과 립싱크를 연결합니다. 기존 서비스 프리팹의 경로와 골격은 유지하고, 원본 모델을 보존한 감정 메시를 참조합니다. Production 씬에서 이 프리팹을 사용하는 NPC에도 적용됩니다. 얼굴 메시가 없는 대체 외형에는 상세 표정이 생기지 않습니다.

실제 대화는 기존 LLM 요청에서 구절별 감정을 함께 생성하고 음성 시간표에 맞춰 재생합니다. 연결 구조·오류 처리·재생성 예외는 [NPC 대화 감정 표정](npc-dialogue-emotions.md)을 따릅니다. 이 문서의 검수 예문은 서버 없이 표정 형태를 확인할 수 있도록 별도로 작성한 감정 구간을 사용합니다.

## Unity에서 확인

1. `Assets/Scenes/Sandbox/NpcEmotionSandbox.unity`를 열고 Play를 누릅니다. 오른쪽 목록에서 12종 캐릭터 중 하나를 고릅니다. 창이 작으면 목록을 스크롤합니다. 카페만 확인하는 기존 `CafeFemaleEmotionSandbox.unity`도 유지합니다.
2. 화면의 16가지 표정 버튼으로 평온·따뜻한 미소·의문·당황·찡그림·놀람·장난기·웃음·걱정/공감·슬픔·폭소·무신경·분노·격한 분노·쑥스러움·누그러짐을 확인합니다.
3. **의문 → 장난기 → 웃음 재생** 또는 **찡그림 → 공감 → 평온 재생**으로 음성과 표정 전환을 함께 확인합니다.
4. 일시 정지 후 음성 시각 슬라이더를 움직여 입술 접촉과 눈꺼풀을 확인합니다. 정면·양쪽 사선 버튼으로 경계와 관통도 확인합니다.
5. **정지 · 중립**을 눌러 입·눈·눈썹이 원래 자세로 돌아오는지 확인합니다.

표정 버튼을 고른 뒤 **선택한 표정으로 대사 재생**을 누르면 그 감정으로 현재 예문을 말합니다. 기존 두 예문 버튼은 원래 지정된 감정 구간으로 재생합니다. 두 방식 모두 같은 음성과 발음 시간표를 사용하며 일시 정지·시간 이동을 지원합니다.

검수 음성은 Windows의 로컬 음성 합성으로 만든 영어 예문입니다. 재생에 네트워크나 API 키가 필요하지 않습니다. 웃음 표정은 말하는 동안의 얼굴 연기이며, 실제 웃음소리나 감정이 실린 TTS를 생성하는 기능은 포함하지 않습니다.

| 용도 | `Assets/` 기준 경로 |
| --- | --- |
| 검수 복사본 | `Prefabs/Npc/Review/LittleMung_Cafe_Female_Emotions.prefab` |
| 복사본 메시·눈 텍스처 | `Art/Npc/CafeFemaleEmotion/` |
| 표정 강도와 두 예문 구간 | `Data/Characters/CafeEmotion/` |
| WAV와 해당 음성의 발음 시간표 | `Audio/Characters/CafeEmotion/` |

서비스에 연결하는 프리팹은 다음 경로 하나씩을 사용합니다. 검수용으로 같은 이름의 프리팹을 추가하지 않습니다.

| 캐릭터 | `Assets/Prefabs/Npc/` 기준 경로 |
| --- | --- |
| 카페 여성 | `CafeFemaleDialogue.prefab` |
| 호텔리어 남성 | `LittleMung_HotelStaff_male_Dialogue.prefab` |
| 비즈니스맨 | `Review/LittleMung_Business_Male.prefab` |
| 중년 남성·여성 | `Review/LittleMung_MiddleAge_Male.prefab`, `Review/LittleMung_MiddleAge_Female.prefab` |
| 흑인 청년 남성 | `Review/LittleMung_BlackYoung_Male.prefab` |
| 할머니 | `Review/LittleMung_Grandmother.prefab` |
| 카페 남성 | `Review/LittleMung_Cafe_Male.prefab` |
| 할아버지 | `Review/LittleMung_Grandfather.prefab` |
| 호텔리어 여성 | `Review/LittleMung_HotelStaff_Female.prefab` |
| 경찰 남성·여성 | `Review/LittleMung_Police_Male.prefab`, `Review/LittleMung_Police_Female.prefab` |
| 존박 전용 공연 캐릭터 | `LittleMung_JohnPark_Male.prefab` |

11종 외형의 추가 감정 메시는 `Assets/Art/Npc/Emotions/<캐릭터 이름>/`에 있습니다. 카페 여성 서비스 프리팹은 승인된 `CafeFemaleEmotion` 메시를 공유합니다. 눈색·피부색과 기존 발음 채널을 유지하고, 호텔리어 남성의 구형 닫힌 눈을 Base 눈꺼풀로 교체합니다.

카페 남성은 Leo, 호텔리어 여성은 Clara, 할아버지는 Arthur, 경찰 남성은 기존 경찰 역할의 외형 연결을 사용합니다. 경찰 여성은 역할 배정 없이 검수 프리팹으로 준비되어 있습니다. 감정 Builder는 역할이나 Production 씬 배치를 바꾸지 않습니다.

## 존박 공연 캐릭터

게임에 표시되는 이름은 **John**이며 `CafeFemaleEmotions.asset`의 16종 감정과 같은 발음 채널을 사용합니다. 기존 에셋 참조와 연결을 유지하기 위해 내부 코드는 `TOWN_JOHN_PARK`, 파일명은 `LittleMung_JohnPark_Male`로 유지합니다. `NpcIdentity`·`DialogueSpeaker`를 연결하며 백엔드 시드와 로컬 검수 대본의 이름은 John, 역할은 뮤지션입니다. 기본 음성 `am_michael`은 서비스의 합성 목소리이며 실제 가수의 음성을 복제하지 않습니다. 서버 시드와 클라이언트 변경의 배포는 별도입니다.

공용 외형 표 `NpcCast`에도 `NpcLooks.John`으로 같은 프리팹을 등록합니다. 이 외형의
`UsesRecordedPerformance`는 공용 외형 갱신에서 Humanoid 재연결·보행 복사본·배색 변경을 적용하지 않고
전용 Generic Avatar·공통 얼굴·저장 공연 연결을 확인하게 합니다. John은 출퇴근 보행 대상이 아닙니다.
Production의 공용 오프라인 대본에도 John과 뮤지션 역할을 등록하여 기본 `Neighbor` 대본으로 빠지지 않게 합니다.

`JohnParkDialogueBuilder.PlaceInRestaurant`는 Town의 식당 실내 상주 목록에 같은 프리팹을 연결합니다. 위치는 식당 좌표 `(0, 0, -5.2)`의 뒷주방 앞 중앙 통로입니다. 실내 입장 때 나타나고 퇴장 때 숨겨지며 별도 출퇴근 일과나 보행을 추가하지 않습니다. 첨부 화면을 기준으로 추정한 위치이므로 최종 구도는 실제 실내에서 확인합니다.

페르소나는 차분하고 예의 바른 음악가 손님으로 설정합니다. 짧고 쉬운 영어, 담백한 농담, 요청받았을 때만 리듬을 넣은 주문 예시를 사용하고 직원 업무는 Rosa에게 안내합니다. 이는 게임용 각색입니다. [본인 인터뷰](https://www.segye.com/newsView/20140919000136)의 말수가 적고 혼자 있는 것을 즐긴다는 설명과 [SBS 방송 발언](https://ent.sbs.co.kr/amp/article.do?article_id=E10004429011)의 과장된 애교를 어려워한다는 설명을 참고합니다. 과거 발언을 현재 성격 전체로 단정하지 않습니다. MBTI는 이차 자료에 INFP로 소개되지만 직접 발표 출처를 확인하지 못해 확정 정보로 사용하지 않습니다.

- 표정 형태: `Assets/Scenes/Sandbox/NpcJohnParkEmotionSandbox.unity`에서 16종 버튼과 정면·사선, 음성 재생을 확인합니다.
- 실제 대화 흐름: `Assets/Scenes/Sandbox/NpcJohnParkDialogueSandbox.unity`를 Play하고 존박을 클릭한 뒤 대화를 수락합니다. 첫 대사는 분노, `sad`를 입력하면 슬픔, `surprised`를 입력하면 놀람을 확인합니다. 대화 종료 시 음성과 입모양도 해제합니다.
- 검수 씬은 `DialogueServerLink.useServer=false`, API 설정·음성 녹음기 미연결로 저장 WAV만 재생합니다. 기존 카페 검수 음성을 재사용하므로 카페 관련 대사를 말하며, 이 대본은 서비스용 인격이나 새 LLM 응답이 아닙니다. 음성과 발음 시간표를 유지한 채 검수 감정 정보만 추가합니다.
- 운영 씬에 놓을 때는 위의 동일한 프리팹을 배치하고 **Tools → A305 → Npc → John Park → Connect Selected Performer To Dialogue**로 그 씬의 공용 플레이어·대화 화면·대화 제안 화면에 연결한 뒤 씬을 저장합니다. 임의 위치에 자동 배치하거나 운영 씬 전체를 다시 만들지 않습니다.

존박의 `Passerby` 연결은 제자리 공연용이며, 이동·공용 손짓을 별도로 적용하지 않습니다. 몸과 왼팔은 전용 Generic 리듬 Animator를 쓰고 오른손·앰프는 잡기 자세를 유지합니다. 발음과 감정은 얼굴 Blend Shape로 재생하므로 몸의 Humanoid 여부와 독립적입니다.

`JohnParkEmotionBuilder`가 FBX 좌표와 공통 감정 저작 좌표를 변환하고, 기존 `ServiceNpcEmotions/author_emotions.py`를 재사용합니다. 다른 Tripo 얼굴은 정점 위치가 다르므로 감정 프로필을 공유하는 것과 얼굴 변형 데이터를 맞추는 작업은 구분합니다. `protect_john_park_emotion_boundaries.py`는 얼굴 피부·눈썹 연결성분만 움직이고 모자·머리카락·소품을 보존합니다. 공통 Builder와 동일하게 눈 넓힘은 별도 눈꺼풀에만 적용하여 입 접합 피부가 벌어지지 않게 합니다. 기존 입모양·중립 정점·UV·본·가중치는 보존합니다.

`JohnParkDialogueBuilder.BuildAndValidate`는 저작 결과 적용, 두 검수 씬 연결, 정면·사선 렌더와 음성/감정/잡기 검증을 실행합니다. 기존 감정 메시를 다시 적용하므로 수동 편집분이 있으면 먼저 보관합니다. `JohnParkDialogueTests`는 서버를 호출하지 않고 실제 `DialogueController → DialogueSpeaker → DialogueServerLink → CharacterSpeechPlayer` 경로의 시작·재생·종료와 감정/발음 동시 적용을 확인합니다.

### John의 저장 안내와 랩 버튼

`Assets/Data/Npc/JohnPark/OrderRap.asset`은 랩과 안내의 **영문·한국어 번역·음성 참조**를 함께
보관합니다. `Assets/Audio/Characters/JohnParkDialogue/ReplayGuide.bytes`에는 로컬 `am_onyx`
합성 안내, 음소 시간표, `warm_smile` 감정 시간표가 들어 있습니다. `OrderRap.bytes`는 기존 1.5배
랩입니다. Unity 빌드에 포함되는 데이터이며 매 재생 때 DB나 AI에서 다시 만드는 문장이 아닙니다.

1. John과의 자유 대화에서 `랩 다시 들려줘` 또는 `Can you play that rap again?`를 보냅니다.
2. 로컬에서 명확한 요청으로 인식하면 “Tap 'Replay rap' and I'll play it again!” 안내와 저장된
   표정·입모양을 재생하고 **랩 다시 듣기** 버튼을 강조합니다. 뜻 보기에는 저장된 한국어를 표시합니다.
3. 버튼을 누르면 1.5배 랩 음성의 재생 시각으로 발음·미소·몸동작을 맞춥니다. 재생 중 버튼은
   **랩 멈추기**로 바뀌고, 끝나거나 중단한 뒤 다시 누르면 처음부터 재생합니다.
4. `I'd love an encore of that burger beat.` 같은 간접 요청은 서버 의도 분류만 요청합니다.
   `REPLAY_RAP`이면 같은 저장 안내를 사용하고 `CONVERSATION`일 때만 기존 대화 요청을 보냅니다.
   분류 실패에는 대사·TTS를 자동 생성하지 않고 버튼을 직접 누르도록 안내합니다.

버튼은 저장 공연을 연결한 John의 자유 대화에서만 표시합니다. 복습·다른 NPC에는 보이지 않습니다.
분류 중 중복 요청과 종료 뒤 늦게 도착한 결과는 무시합니다. 저장 안내·랩은 로컬 대화 화면에 표시하고
서버의 학습 평가용 사용자 턴을 만들지 않습니다. 일반 음성 입력은 기존 STT를 거칩니다.
저장 안내가 없거나 재생에 실패해도 일반 대사 생성으로 우회하지 않고 오류 안내를 표시합니다.

`RecordedPerformanceUiBuilder.Build`는 기존 대화창 디자인을 유지하며 버튼과 저장 안내를 연결합니다.
로컬 검수 씬 `NpcJohnParkDialogueSandbox.unity`는 외부 호출 없이 명확한 요청·뜻 보기·버튼·중단을
확인하는 용도입니다. 간접 요청 분류에는 새 Spring API와 AI 워커의 동시 배포가 필요합니다.

## 실제 대화의 기분 설정

각 프리팹의 `CharacterSpeechPlayer`에 공통 `CafeFemaleEmotions.asset`과 기본 기분 `WarmSmile`을 연결합니다. `DialogueSpeaker`는 해당 NPC 안의 재생기를 찾아 기존 `DialogueServerLink` 음성과 발음 시간표를 재생합니다. 새로운 대사에도 별도 예문 제작 없이 같은 설정이 적용됩니다.

게임 로직은 `player.ConfigureMood(profile, CharacterEmotion.Sad, 0.8f)`처럼 다음 발화의 기분을 설정할 수 있습니다. Inspector의 `Speaking Mood`와 `Mood Intensity`로도 지정합니다. 이미 재생 중인 음성에는 소급하지 않습니다. 발화별 `SpeechEmotionTrack`과 프로필을 명시적으로 전달하면 기본 기분보다 우선하며, 발화 종료·화자 변경에는 얼굴을 중립으로 돌립니다. 기본 기분 설정 자체는 유지됩니다.

감정 시간표가 없는 서버 응답은 NPC에 설정된 기분으로 재생합니다. 발음 시간표도 없으면 기존처럼 음성만 재생하며 임의로 음소 시각을 만들지 않습니다. 텍스트만 있는 모의 대화에는 음성 립싱크가 없습니다.

## 표정과 발음의 합성

`CharacterSpeechPlayer`는 `AudioSource.timeSamples`로 입모양·감정·깜박임을 같은 시각에 평가합니다. `SpeechEmotionTrack`은 시작·종료 시각, 감정 종류, 강도만 갖고 있으며 겹치는 구간, 음성 밖의 구간, 유효하지 않은 값을 거부합니다. 이어지는 감정은 최대 0.24초 동안 보간합니다. 독립 검수 재생은 종료 시 중립으로 돌아오고, 실제 대화는 마지막 감정을 유지합니다.

`EmotionExpressionProfile`의 16가지 감정은 실제 Blend Shape 가중치 조합입니다. 입술을 닫는 M/B/P, F/V의 접촉, TH의 혀 위치는 입꼬리 표정보다 우선합니다. 둥근 입과 크게 여는 발음도 미소·찡그림을 줄여 모양이 충돌하지 않게 합니다. 깜박임 중에는 눈 찡그림·크게 뜨기와 볼 올림을 줄입니다. 눈동자 메시 자체는 압축하지 않고 별도 눈꺼풀 메시가 눈을 덮습니다. 부위별 수치와 대화 테스트 예시는 [표정 설정과 수동 검수](npc-dialogue-emotions.md#설정값과-수동-검수)를 따릅니다.

표정 차이는 재생 가중치만 올리지 않고 제작 메시의 눈썹 각도·입꼬리 곡선으로 조절합니다. 의문은 한쪽 눈썹, 걱정은 양쪽 눈썹 안쪽, 찡그림은 안쪽으로 내려오는 눈썹과 아래로 향하는 입꼬리로 구분합니다. 입술 접촉 발음 중에는 입꼬리가 제한되므로 같은 발음 순간의 눈썹·눈매도 비교해야 합니다.

웃음·놀람은 발음이 없는 순간에 `restMouthOpen`으로 입을 엽니다. 실제 적용량은 `설정 강도 × (1 - 발음 가중치 합)`이며, 발음 가중치 합은 0~1로 제한합니다. 이 변형은 기존 `viseme_AA`를 재사용합니다. 발음이 완전히 활성화된 순간에는 감정 입 벌림을 더하지 않고, 입꼬리와 눈썹·눈매로 감정을 유지합니다. 실제 웃음소리나 몸 전체의 웃음 동작은 포함하지 않습니다.

기존 공통 13개 채널에 다음 7종의 좌우 채널 14개를 추가합니다.

`browInnerUp`, `browOuterUp`, `browDown`, `eyeSquint`, `eyeWide`, `cheekRaise`, `mouthFrown` + `Left` / `Right`

폭소에는 선택 채널 `mouthLaughOpen`을 더합니다. 기존 `viseme_AA` 위에 더하는 변형으로, 둥근 웃음 윤곽의 입술·구강·위 치아·혀와 얼굴 접합부를 함께 움직입니다. 적용량은 발음이 없는 비중과 AA 입 벌림 양을 넘지 않으며 기존 발음 채널 자체는 변경하지 않습니다. 평상시의 미소도 별도 채널로 그대로 유지합니다. 이 채널이 없는 NPC는 기존 AA 입 벌림까지만 적용합니다.

슬픔의 `eyeClosure`는 미소 곡선 없이 눈꺼풀만 내립니다. 장난기도 이 값을 한쪽 눈에만 적용해 윙크합니다. 당황은 양눈을 뜨고 양쪽 눈썹을 올리는 표정이므로 장난기와 구별됩니다. 기존 감정의 직렬화 번호는 유지하고 슬픔·폭소를 뒤에 추가합니다.

무신경(`Indifferent`)은 반쯤 내려간 양쪽 눈꺼풀, 낮은 눈썹 변화와 수평에 가까운 입꼬리를 조합합니다. 슬픔의 안쪽 눈썹 올림과 강한 입꼬리 내림을 사용하지 않습니다. 발음의 강도와 시간표는 유지하며, 감정에 의한 추가 입 벌림은 없습니다. 기본 미소의 프리셋 수치와 입꼬리 채널은 유지합니다.

`eyeSquint`는 눈을 다시 닫는 독립 변형이 아니라, `eyeBlink`로 닫힌 눈꺼풀 위에 더하는 웃는 곡선 보정입니다. 재생기는 같은 눈의 닫힘을 `max(깜박임, 찡그림 닫힘 이동량, eyeClosure)`로 유지하고 깜박일 때 웃는 곡선과 애교살 올림을 줄입니다. 약한 찡그림은 눈꺼풀이 안구 뒤에 머물지 않도록 `browDown` 강도에 따라 닫힘 이동량을 보강합니다. 웃음·폭소는 `eyeSquintOpening`으로 닫힘만 줄여 눈동자가 보이게 하고, `cheekRaise`로 아래 눈꺼풀과 애교살 중앙을 둥글게 올립니다. 실제 깜빡임과 직접 눈 감기는 완전히 닫히고, 인자한 미소의 프리셋 값은 유지합니다. `eyeSquint` 채널이 없는 기존 눈은 찡그림에 의한 닫힘을 추가하지 않습니다.

새 채널이 없는 기존 NPC는 기존 립싱크·미소·깜박임을 유지합니다. `CharacterFaceController.MissingEmotionChannels`로 누락을 확인할 수 있습니다. 새 채널 이름만 등록한다고 실제 메시 변형이 생기지는 않습니다.

## 제작과 재사용

Blender 제작 원본은 `frontend/ArtSources/Characters/CafeFemaleEmotion/`에 있습니다. `author_emotions.py`는 원본 카페 모델과 Base 눈의 검수 데이터를 읽어 새 폴더에만 출력합니다. 원본 몸의 중립 정점·UV·리깅을 유지하면서 눈 주변 면을 Base 눈꺼풀에 연결하고 감정 변형을 추가합니다. 기존 발음별 입 메시를 재사용합니다.

`CafeEmotionBuilder.Build`는 출력 프리팹이 없을 때만 검수 에셋과 씬을 생성합니다. 제작 표정을 다시 가져오려면 **Tools → A305 → Characters → Refresh Cafe Emotion Authored Shapes**를 명시적으로 실행합니다. 이 명령은 검수 복사본의 감정 변형과 눈 메시의 Blend Shape를 제작 JSON으로 교체하므로 Unity에서 직접 수정한 변형이 있다면 먼저 보존해야 합니다. 기존 발음 채널·프리팹·골격·머티리얼·감정 강도는 바꾸지 않습니다.

검수 복사본의 `mouthSmileLeft/Right`도 제작 JSON의 곡선으로 갱신합니다. 같은 이름의 채널은 교체하고, JSON에 없는 기존 발음 채널은 유지합니다. 입꼬리 변형은 눈 접합 면까지 같은 변위를 적용해 볼에 틈이 생기지 않게 합니다.

`CafeEmotionBuilder.ResetReviewProfile`은 카페 검수 프로필의 모든 강도를 코드의 기본값으로 되돌립니다. 메시 갱신과 별도 명령이며, 프로필을 직접 조정했다면 실행 전에 보존해야 합니다.

새 감정만 추가할 때는 `CafeEmotionBuilder.AddMissingReviewPresets`를 실행합니다. `EmotionExpressionProfile.AddMissingDefaults`로 누락된 프리셋만 추가하므로 기존 미소 등 직접 조정한 값은 유지되고, 반복 실행해도 중복되지 않습니다.

다른 NPC의 제작 어댑터는 `frontend/ArtSources/Characters/ServiceNpcEmotions/author_emotions.py`입니다. 두 눈과 입 기준점으로 좌표계를 정규화하고, 텍스처에서 눈썹을 측정한 뒤 카페의 검수된 변형을 각 얼굴 좌표로 되돌립니다. 눈 구멍을 통해 보이는 머리 뒷면이나 속눈썹을 눈썹으로 오인하지 않도록 눈 접합부 전체보다 위쪽을 탐색합니다. 앞머리와 눈썹이 붙어 있으면 아래쪽 경계로 높이를 측정하며, 경계가 불명확하면 중단합니다. 입 접합 경계와 눈꺼풀의 고정 경계를 유지합니다. 얼굴 형태가 다른 새 모델은 결과를 정면·양사선·발화 중에 검수해야 합니다.

`NpcEmotionBuilder.Export`는 `NPC_EMOTION_BATCH_OUTPUT` 아래에 기존 프리팹의 읽기용 `Source.json`을 내보냅니다. 출력이 이미 있으면 중단합니다. Blender 어댑터는 `--source <Source.json> --work <새 작업 폴더> --output <새 결과 폴더>`로 실행하며, 새 눈이 필요한 모델에는 검수한 `Blink.json` 폴더를 `--new-eyes`로 전달합니다. 기존 출력 폴더를 덮어쓰지 않습니다.

이미 Base 눈을 연결한 얼굴에서 접합 면의 방사형 음영이 보이면 `--smooth-eye-shading`으로 눈 주변 피부와 눈꺼풀의 노멀을 함께 보정할 수 있습니다. 중립 정점·눈 크기·UV·골격·가중치·텍스처는 유지하고, 주변 피부 곡면으로 음영 방향을 맞춥니다. 기존 노멀 자체를 보존하는 작업과 구분해 변경 전후를 정면·양사선·강한 측광에서 검수합니다. 새 눈 연결과 동시에 실행하지 않으며, 얼굴·발음 변위 보존도 따로 확인합니다. 표정 데이터를 다시 제작해도 90% 눈 감기 프레임을 중복 추가하지 않습니다.

찡그림 제작은 눈썹 안쪽을 낮추고 바깥쪽을 살짝 올리며, 슬픔·찡그림의 입꼬리 하강을 분명하게 만듭니다. 낮은 면 밀도에서 피부가 접혀 보이지 않도록 해당 변형의 음영 변화는 완화합니다. 인자한 미소와 발음별 입 변위는 유지합니다. 제작 스크립트 변경은 기존 모든 NPC 메시를 자동으로 덮어쓰지 않으므로, 새 결과를 가져온 캐릭터에서 먼저 검수합니다.

캐릭터별 제작 결과는 `ServiceNpcEmotions/<캐릭터 이름>/`의 `.blend`, `Landmarks.json`, 압축된 `Emotions.json.gz`로 보관합니다. **Tools → A305 → Characters → Apply Reviewed NPC Emotions**는 이 데이터를 기존 프리팹에 연결합니다. 기준 정점이 달라지거나 발음 채널을 덮어쓰는 입력이면 거부합니다. 감정 제작 결과를 다시 적용하는 명령이므로 직접 수정한 감정 메시가 있다면 먼저 보존해야 합니다. `NPC_EMOTION_CHARACTER`로 한 캐릭터만 지정할 수 있습니다.

웹 빌드 데이터를 줄이려고 눈꺼풀 메시(`Assets/Art/Npc/Emotions/<캐릭터 이름>/Face/Blink/`)의 깜빡임·찡그림은 25·50·75·90·100 다섯 단계 중 50·100 두 단계만 남깁니다. 눈꺼풀 하나의 표정 데이터에서 이 중간 단계가 80%를 차지했습니다. **Tools → A305 → Characters → Trim NPC Eyelid Frames**(`EyelidFrameTrimmer`)가 이미 만든 메시에서 단계만 고르며, 채널 순서·단계 하나짜리 채널·노멀 유무는 그대로 둡니다. 50·100 단계가 없는 채널은 바꾸지 않습니다. 남긴 단계 사이는 Unity가 보간합니다. 무게 25·75는 원래 보간 결과와 같아 모습이 바뀌지 않고, 90% 눈 감기 프레임이 빠져 무게 90에서는 눈이 완전히 닫히지 않습니다(깜빡임 중 한두 프레임). 감정 제작 결과를 다시 적용하면 다섯 단계로 돌아오므로 이 명령을 다시 실행합니다.

웃는 눈은 기존 닫힌 눈의 곡률에 맞춰 들어 올리고, 눈꺼풀 선이 얇아져 끊겨 보이지 않도록 감정 변형에서 두께와 눈꺼풀 앞쪽 위치를 유지합니다. `smile_surface.py`는 덜 닫은 눈·애교살 올림을 합친 상태에서 눈꺼풀이 안구 앞에 있도록 웃는 눈 채널의 깊이를 보정합니다. 중립·깜빡임·바깥 접합 링은 보존합니다. 폭소의 혀는 해당 입 벌림 윤곽 안에 둡니다. 이 보정은 감정 채널에만 적용하며 기존 발음 채널은 보존합니다.

**Build NPC Emotion Sandbox**는 비교 씬을 다시 만듭니다. Production 씬은 변경하지 않습니다. `NpcEmotionBuilder.Audit`는 내보낸 원본 경로와 현재 프리팹의 중립 정점·UV·노멀·가중치·bind pose·텍스처·발음 프레임을 비교해 `Preservation.json`으로 기록합니다.

## 검증 범위와 남은 연결

- 코드 검사: 감정 전환·립싱크 우선권·깜박임 충돌·잘못된 구간·기존 모델 호환·정지 및 재생 상태를 확인합니다.
- PlayMode 검사: 12종 프리팹의 `DialogueSpeaker`와 `DialogueServerLink`에 저장된 실제 응답을 전달해 표정·립싱크·중단을 확인합니다. 네트워크 호출은 하지 않습니다. 카페의 실제 AudioSource 재생과 몸 애니메이터 동시 실행 검사도 유지합니다.
- 외형 검사: 정면·양쪽 사선과 음성 포함 비교 영상으로 눈꺼풀·입 경계와 표정의 인상을 확인합니다. 가중치 검사만으로 외관을 합격 처리하지 않습니다.
- 서비스 적용: 서버가 반환한 구절별 감정을 실제 합성 음성 길이로 시간표에 넣고 Unity에서 재생합니다. 검수 예문의 시각이나 글자 수를 다른 대사의 감정 시각으로 재사용하지 않습니다. 서버·클라이언트 배포와 실서비스에서 선택되는 감정의 적절성은 별도로 확인합니다.
- NPC 확장: 새 채널을 가진 외형에만 상세 감정을 적용하고, 미지원 얼굴은 기존 표정으로 동작시킵니다. `NpcSpeechAudit.ExportProductionReport`로 Production 씬의 발음·감정 채널 누락을 읽기 전용으로 확인합니다. 실제 서버 응답과 WebGL 음성 재생 검증은 별도 단계입니다.

## 비교 렌더

Editor를 닫고 `NPC_EMOTION_REVIEW_OUTPUT`에 저장소 밖 출력 폴더를 지정한 뒤 Unity batchmode에서 `A305.EditorTools.Characters.CafeEmotionBuilder.Render`를 실행합니다. `NPC_EMOTION_STILLS_ONLY=1`은 16종 표정의 세 방향 정지 이미지만 만듭니다.

`NPC_EMOTION_REACTION_REVIEW=1`은 폭소·슬픔·당황·장난기의 중립→표정→중립 전환과 같은 예문을 말하는 프레임을 각각 만듭니다. 이는 표정 비교 렌더이며 웃음소리나 몸의 웃음 동작을 합성하지 않습니다.

`NPC_EMOTION_INDIFFERENT_REVIEW=1`은 같은 방식으로 미소·무신경·슬픔을 비교합니다. 음성의 억양은 동일하며 얼굴 감정과 발음 혼합만 비교합니다.

기본 출력은 30fps이며 두 예문 각각 `Before`(기존 고정 미소·깜박임)와 `After`(감정 구간 포함) 프레임을 만듭니다. 양쪽은 같은 복사본 메시와 같은 발음 시간표를 사용합니다. 음성 시작 앞에 0.3초 여백이 있으므로 WAV를 합칠 때도 0.3초 지연을 적용합니다. `Samples.csv`에는 음성 시각별 실제 채널 가중치가 기록됩니다. 렌더는 씬·프리팹을 저장하지 않습니다.

12종 캐릭터 비교는 `NPC_EMOTION_BATCH_OUTPUT`을 지정하고 `NpcEmotionBuilder.Render`를 실행합니다. 기본값은 각 캐릭터의 16가지 표정 정면·양사선이며 `NPC_EMOTION_BATCH_SPEECH=1`이면 같은 예문의 30fps 발화 프레임도 만듭니다. `NPC_EMOTION_PHASE`는 출력 하위 폴더 이름, `NPC_EMOTION_CHARACTER`는 캐릭터 필터입니다. 프레임의 음성 앞 여백은 동일하게 0.3초입니다.

### 존박 저장 주문 랩

`Assets/Prefabs/Npc/LittleMung_JohnPark_Male.prefab`은 `RecordedNpcPerformance`로 1.5배 남성 음성(`am_onyx`)과 121BPM 리듬을 재생합니다. 설정은 `Assets/Data/Npc/JohnPark/OrderRap.asset`, 음성·발음·감정 시간표는 `Assets/Audio/Characters/JohnParkDialogue/OrderRap.bytes`에 있습니다. 실제 인물의 녹음이나 복제 음성은 사용하지 않습니다.

- 음성은 선정한 1.5배 PCM을 그대로 보존합니다. 발화 중에는 양쪽 입꼬리가 같은 강도로 움직이는 `warm_smile`을 적용합니다. 한쪽 입꼬리를 드는 `playful`은 이 랩에서 사용하지 않습니다. 발음은 동일한 무반주 트랙을 로컬 PocketSphinx로 정렬하고, 그 시간표를 드럼을 포함한 음성의 PCM 해시에 묶습니다. 구절 전체를 같은 입모양으로 처리하지 않습니다.
- Generic 리듬의 왼팔 강세 두 번을 한 마디의 2·4박에 맞춥니다. 오른손·앰프의 기존 본 곡선과 강체 결합은 유지합니다. 공연 종료 뒤에는 기존 대기 리듬 속도로 돌아갑니다.
- `NpcJohnParkDialogueSandbox.unity`를 열고 Play → 존박 클릭 → `Can you rap?` 또는 `랩 해줘`를 입력합니다. 저장 안내가 나오면 [랩 다시 듣기]를 눌러 시작합니다. 요청은 안내를, 전용 버튼은 랩을 재생합니다. 이 씬은 서버가 꺼진 로컬 검수용입니다.
- Town의 식당 존박도 같은 프리팹 연결을 상속합니다. 일반 AI 대화·음성 입력 전사는 기존 서버 설정이 필요하지만, 저장 공연 재생에는 LLM/TTS 재호출이 없습니다. 저장 공연은 서버의 학습 턴으로 기록하지 않습니다.
- 연결 재구성은 `Tools → A305 → Npc → John Park → Connect Recorded Order Rap`으로 실행합니다. 기존 얼굴·본·머티리얼은 바꾸지 않습니다. `JohnParkRapValidation.Capture`는 실제 재생 컴포넌트의 시각 변환으로 전신·얼굴 프레임과 잡기 오차를 검사합니다. 로컬 영상은 `artifacts/JohnParkRhythm/OrderRap/Unity1.5/`에 보관합니다.

### John 전용 재생 버튼 검수

저장 자료와 실제 재생 분기는 [John의 저장 안내와 랩 버튼](#john의-저장-안내와-랩-버튼)을 따릅니다.
`JonePerformanceUiPreview.Capture`는 공용 대화창에 실제 연결된 버튼의 대기·강조·재생 상태를
넓은 화면과 좁은 화면에서 렌더합니다. 운영 씬은 저장하지 않습니다.
