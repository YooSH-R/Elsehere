# 말하기 중 NPC 감정 표정 검수

카페 남녀·호텔리어 남녀·비즈니스맨·중년 남녀·흑인 청년 남성·할머니·할아버지·경찰 남녀의 12종 외형에 공통 감정 표정과 립싱크를 연결합니다. 기존 서비스 프리팹의 경로와 골격은 유지하고, 원본 모델을 보존한 감정 메시를 참조합니다. Production 씬에서 이 프리팹을 사용하는 NPC에도 적용됩니다. 얼굴 메시가 없는 대체 외형에는 상세 표정이 생기지 않습니다.

현재 연결은 NPC에 설정한 기분을 새 음성 대사에 적용하는 범위입니다. 대사 내용에서 LLM이 감정을 추론하거나 서버 감정을 자동 매핑하는 기능은 포함하지 않습니다. 검수 예문의 감정 전환 구간은 별도로 작성한 데이터입니다.

## Unity에서 확인

1. `Assets/Scenes/Sandbox/NpcEmotionSandbox.unity`를 열고 Play를 누릅니다. 오른쪽 목록에서 12종 캐릭터 중 하나를 고릅니다. 창이 작으면 목록을 스크롤합니다. 카페만 확인하는 기존 `CafeFemaleEmotionSandbox.unity`도 유지합니다.
2. 화면의 12가지 표정 버튼으로 평온·따뜻한 미소·의문·당황·찡그림·놀람·장난기·웃음·걱정/공감·슬픔·폭소·무신경을 확인합니다.
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

11종 외형의 추가 감정 메시는 `Assets/Art/Npc/Emotions/<캐릭터 이름>/`에 있습니다. 카페 여성 서비스 프리팹은 승인된 `CafeFemaleEmotion` 메시를 공유합니다. 눈색·피부색과 기존 발음 채널을 유지하고, 호텔리어 남성의 구형 닫힌 눈을 Base 눈꺼풀로 교체합니다.

카페 남성은 Leo, 호텔리어 여성은 Clara, 할아버지는 Arthur, 경찰 남성은 기존 경찰 역할의 외형 연결을 사용합니다. 경찰 여성은 역할 배정 없이 검수 프리팹으로 준비되어 있습니다. 감정 Builder는 역할이나 Production 씬 배치를 바꾸지 않습니다.

## 실제 대화의 기분 설정

각 프리팹의 `CharacterSpeechPlayer`에 공통 `CafeFemaleEmotions.asset`과 기본 기분 `WarmSmile`을 연결합니다. `DialogueSpeaker`는 해당 NPC 안의 재생기를 찾아 기존 `DialogueServerLink` 음성과 발음 시간표를 재생합니다. 새로운 대사에도 별도 예문 제작 없이 같은 설정이 적용됩니다.

게임 로직은 `player.ConfigureMood(profile, CharacterEmotion.Sad, 0.8f)`처럼 다음 발화의 기분을 설정할 수 있습니다. Inspector의 `Speaking Mood`와 `Mood Intensity`로도 지정합니다. 이미 재생 중인 음성에는 소급하지 않습니다. 발화별 `SpeechEmotionTrack`과 프로필을 명시적으로 전달하면 기본 기분보다 우선하며, 발화 종료·화자 변경에는 얼굴을 중립으로 돌립니다. 기본 기분 설정 자체는 유지됩니다.

감정 시간표가 없는 서버 응답은 NPC에 설정된 기분으로 재생합니다. 발음 시간표도 없으면 기존처럼 음성만 재생하며 임의로 음소 시각을 만들지 않습니다. 텍스트만 있는 모의 대화에는 음성 립싱크가 없습니다.

## 표정과 발음의 합성

`CharacterSpeechPlayer`는 `AudioSource.timeSamples`로 입모양·감정·깜박임을 같은 시각에 평가합니다. `SpeechEmotionTrack`은 시작·종료 시각, 감정 종류, 강도만 갖고 있으며 겹치는 구간, 음성 밖의 구간, 유효하지 않은 값을 거부합니다. 이어지는 감정은 짧게 보간하고 빈 구간·발화 종료에는 중립으로 돌아옵니다.

`EmotionExpressionProfile`의 12가지 감정은 실제 Blend Shape 가중치 조합입니다. 입술을 닫는 M/B/P, F/V의 접촉, TH의 혀 위치는 입꼬리 표정보다 우선합니다. 둥근 입과 크게 여는 발음도 미소·찡그림을 줄여 모양이 충돌하지 않게 합니다. 깜박임 중에는 눈 찡그림·크게 뜨기와 볼 올림을 줄입니다. 눈동자 메시 자체는 압축하지 않고 별도 눈꺼풀 메시가 눈을 덮습니다.

표정 차이는 재생 가중치만 올리지 않고 제작 메시의 눈썹 각도·입꼬리 곡선으로 조절합니다. 의문은 한쪽 눈썹, 걱정은 양쪽 눈썹 안쪽, 찡그림은 안쪽으로 내려오는 눈썹과 아래로 향하는 입꼬리로 구분합니다. 입술 접촉 발음 중에는 입꼬리가 제한되므로 같은 발음 순간의 눈썹·눈매도 비교해야 합니다.

웃음·놀람은 발음이 없는 순간에 `restMouthOpen`으로 입을 엽니다. 실제 적용량은 `설정 강도 × (1 - 발음 가중치 합)`이며, 발음 가중치 합은 0~1로 제한합니다. 이 변형은 기존 `viseme_AA`를 재사용합니다. 발음이 완전히 활성화된 순간에는 감정 입 벌림을 더하지 않고, 입꼬리와 눈썹·눈매로 감정을 유지합니다. 실제 웃음소리나 몸 전체의 웃음 동작은 포함하지 않습니다.

기존 공통 13개 채널에 다음 7종의 좌우 채널 14개를 추가합니다.

`browInnerUp`, `browOuterUp`, `browDown`, `eyeSquint`, `eyeWide`, `cheekRaise`, `mouthFrown` + `Left` / `Right`

폭소에는 선택 채널 `mouthLaughOpen`을 더합니다. 기존 `viseme_AA` 위에 더하는 변형으로, 둥근 웃음 윤곽의 입술·구강·위 치아·혀와 얼굴 접합부를 함께 움직입니다. 적용량은 발음이 없는 비중과 AA 입 벌림 양을 넘지 않으며 기존 발음 채널 자체는 변경하지 않습니다. 평상시의 미소도 별도 채널로 그대로 유지합니다. 이 채널이 없는 NPC는 기존 AA 입 벌림까지만 적용합니다.

슬픔의 `eyeClosure`는 미소 곡선 없이 눈꺼풀만 내립니다. 장난기도 이 값을 한쪽 눈에만 적용해 윙크합니다. 당황은 양눈을 뜨고 양쪽 눈썹을 올리는 표정이므로 장난기와 구별됩니다. 기존 감정의 직렬화 번호는 유지하고 슬픔·폭소를 뒤에 추가합니다.

무신경(`Indifferent`)은 반쯤 내려간 양쪽 눈꺼풀, 낮은 눈썹 변화와 수평에 가까운 입꼬리를 조합합니다. 슬픔의 안쪽 눈썹 올림과 강한 입꼬리 내림을 사용하지 않습니다. 발음의 강도와 시간표는 유지하며, 감정에 의한 추가 입 벌림은 없습니다. 기본 미소는 기존 프리셋과 메시를 그대로 사용합니다.

`eyeSquint`는 눈을 다시 닫는 독립 변형이 아니라, `eyeBlink`로 닫힌 눈꺼풀 위에 더하는 웃는 곡선 보정입니다. 재생기는 같은 눈의 닫힘을 `max(깜박임, 지원하는 찡그림, eyeClosure)`로 유지하고 깜박일 때 웃는 곡선만 줄입니다. 닫힘 궤적을 별도로 더해 눈동자가 중간 프레임에서 뚫고 나오지 않게 합니다. `eyeSquint` 채널이 없는 기존 눈은 찡그림에 의한 닫힘을 추가하지 않습니다.

새 채널이 없는 기존 NPC는 기존 립싱크·미소·깜박임을 유지합니다. `CharacterFaceController.MissingEmotionChannels`로 누락을 확인할 수 있습니다. 새 채널 이름만 등록한다고 실제 메시 변형이 생기지는 않습니다.

## 제작과 재사용

Blender 제작 원본은 `frontend/ArtSources/Characters/CafeFemaleEmotion/`에 있습니다. `author_emotions.py`는 원본 카페 모델과 Base 눈의 검수 데이터를 읽어 새 폴더에만 출력합니다. 원본 몸의 중립 정점·UV·리깅을 유지하면서 눈 주변 면을 Base 눈꺼풀에 연결하고 감정 변형을 추가합니다. 기존 발음별 입 메시를 재사용합니다.

`CafeEmotionBuilder.Build`는 출력 프리팹이 없을 때만 검수 에셋과 씬을 생성합니다. 제작 표정을 다시 가져오려면 **Tools → A305 → Characters → Refresh Cafe Emotion Authored Shapes**를 명시적으로 실행합니다. 이 명령은 검수 복사본의 감정 변형과 눈 메시의 Blend Shape를 제작 JSON으로 교체하므로 Unity에서 직접 수정한 변형이 있다면 먼저 보존해야 합니다. 기존 발음 채널·프리팹·골격·머티리얼·감정 강도는 바꾸지 않습니다.

검수 복사본의 `mouthSmileLeft/Right`도 제작 JSON의 곡선으로 갱신합니다. 같은 이름의 채널은 교체하고, JSON에 없는 기존 발음 채널은 유지합니다. 입꼬리 변형은 눈 접합 면까지 같은 변위를 적용해 볼에 틈이 생기지 않게 합니다.

`CafeEmotionBuilder.ResetReviewProfile`은 카페 검수 프로필의 모든 강도를 코드의 기본값으로 되돌립니다. 메시 갱신과 별도 명령이며, 프로필을 직접 조정했다면 실행 전에 보존해야 합니다.

새 감정만 추가할 때는 `CafeEmotionBuilder.AddMissingReviewPresets`를 실행합니다. `EmotionExpressionProfile.AddMissingDefaults`로 누락된 프리셋만 추가하므로 기존 미소 등 직접 조정한 값은 유지되고, 반복 실행해도 중복되지 않습니다.

다른 NPC의 제작 어댑터는 `frontend/ArtSources/Characters/ServiceNpcEmotions/author_emotions.py`입니다. 두 눈과 입 기준점으로 좌표계를 정규화하고, 텍스처에서 눈썹을 측정한 뒤 카페의 검수된 변형을 각 얼굴 좌표로 되돌립니다. 눈 구멍을 통해 보이는 머리 뒷면이나 속눈썹을 눈썹으로 오인하지 않도록 눈 접합부 전체보다 위쪽을 탐색합니다. 앞머리와 눈썹이 붙어 있으면 아래쪽 경계로 높이를 측정하며, 경계가 불명확하면 중단합니다. 입 접합 경계와 눈꺼풀의 고정 경계를 유지합니다. 얼굴 형태가 다른 새 모델은 결과를 정면·양사선·발화 중에 검수해야 합니다.

`NpcEmotionBuilder.Export`는 `NPC_EMOTION_BATCH_OUTPUT` 아래에 기존 프리팹의 읽기용 `Source.json`을 내보냅니다. 출력이 이미 있으면 중단합니다. Blender 어댑터는 `--source <Source.json> --work <새 작업 폴더> --output <새 결과 폴더>`로 실행하며, 새 눈이 필요한 모델에는 검수한 `Blink.json` 폴더를 `--new-eyes`로 전달합니다. 기존 출력 폴더를 덮어쓰지 않습니다.

캐릭터별 제작 결과는 `ServiceNpcEmotions/<캐릭터 이름>/`의 `.blend`, `Landmarks.json`, 압축된 `Emotions.json.gz`로 보관합니다. **Tools → A305 → Characters → Apply Reviewed NPC Emotions**는 이 데이터를 기존 프리팹에 연결합니다. 기준 정점이 달라지거나 발음 채널을 덮어쓰는 입력이면 거부합니다. 감정 제작 결과를 다시 적용하는 명령이므로 직접 수정한 감정 메시가 있다면 먼저 보존해야 합니다. `NPC_EMOTION_CHARACTER`로 한 캐릭터만 지정할 수 있습니다.

웃는 눈은 기존 닫힌 눈의 곡률에 맞춰 들어 올리고, 눈꺼풀 선이 얇아져 끊겨 보이지 않도록 감정 변형에서 두께와 눈꺼풀 앞쪽 위치를 유지합니다. 폭소의 혀는 해당 입 벌림 윤곽 안에 둡니다. 이 보정은 감정 채널에만 적용하며 중립·기존 발음 채널은 보존합니다.

**Build NPC Emotion Sandbox**는 비교 씬을 다시 만듭니다. Production 씬은 변경하지 않습니다. `NpcEmotionBuilder.Audit`는 내보낸 원본 경로와 현재 프리팹의 중립 정점·UV·노멀·가중치·bind pose·텍스처·발음 프레임을 비교해 `Preservation.json`으로 기록합니다.

## 검증 범위와 남은 연결

- 코드 검사: 감정 전환·립싱크 우선권·깜박임 충돌·잘못된 구간·기존 모델 호환·정지 및 재생 상태를 확인합니다.
- PlayMode 검사: 12종 프리팹의 `DialogueSpeaker`와 `DialogueServerLink`에 저장된 실제 응답을 전달해 표정·립싱크·중단을 확인합니다. 네트워크 호출은 하지 않습니다. 카페의 실제 AudioSource 재생과 몸 애니메이터 동시 실행 검사도 유지합니다.
- 외형 검사: 정면·양쪽 사선과 음성 포함 비교 영상으로 눈꺼풀·입 경계와 표정의 인상을 확인합니다. 가중치 검사만으로 외관을 합격 처리하지 않습니다.
- 서비스 적용: 외형 확정 후 서버의 감정 의도/강도를 Unity의 감정 종류로 매핑하고, 실제 음성에 해당하는 구간을 전달하는 연결이 필요합니다. 검수 예문의 시각이나 글자 수를 다른 대사의 감정 시각으로 재사용하지 않습니다.
- NPC 확장: 새 채널을 가진 외형에만 상세 감정을 적용하고, 미지원 얼굴은 기존 표정으로 동작시킵니다. `NpcSpeechAudit.ExportProductionReport`로 Production 씬의 발음·감정 채널 누락을 읽기 전용으로 확인합니다. 실제 서버 응답과 WebGL 음성 재생 검증은 별도 단계입니다.

## 비교 렌더

Editor를 닫고 `NPC_EMOTION_REVIEW_OUTPUT`에 저장소 밖 출력 폴더를 지정한 뒤 Unity batchmode에서 `A305.EditorTools.Characters.CafeEmotionBuilder.Render`를 실행합니다. `NPC_EMOTION_STILLS_ONLY=1`은 12종 표정의 세 방향 정지 이미지만 만듭니다.

`NPC_EMOTION_REACTION_REVIEW=1`은 폭소·슬픔·당황·장난기의 중립→표정→중립 전환과 같은 예문을 말하는 프레임을 각각 만듭니다. 이는 표정 비교 렌더이며 웃음소리나 몸의 웃음 동작을 합성하지 않습니다.

`NPC_EMOTION_INDIFFERENT_REVIEW=1`은 같은 방식으로 미소·무신경·슬픔을 비교합니다. 음성의 억양은 동일하며 얼굴 감정과 발음 혼합만 비교합니다.

기본 출력은 30fps이며 두 예문 각각 `Before`(기존 고정 미소·깜박임)와 `After`(감정 구간 포함) 프레임을 만듭니다. 양쪽은 같은 복사본 메시와 같은 발음 시간표를 사용합니다. 음성 시작 앞에 0.3초 여백이 있으므로 WAV를 합칠 때도 0.3초 지연을 적용합니다. `Samples.csv`에는 음성 시각별 실제 채널 가중치가 기록됩니다. 렌더는 씬·프리팹을 저장하지 않습니다.

12종 캐릭터 비교는 `NPC_EMOTION_BATCH_OUTPUT`을 지정하고 `NpcEmotionBuilder.Render`를 실행합니다. 기본값은 각 캐릭터의 12가지 표정 정면·양사선이며 `NPC_EMOTION_BATCH_SPEECH=1`이면 같은 예문의 30fps 발화 프레임도 만듭니다. `NPC_EMOTION_PHASE`는 출력 하위 폴더 이름, `NPC_EMOTION_CHARACTER`는 캐릭터 필터입니다. 프레임의 음성 앞 여백은 동일하게 0.3초입니다.
