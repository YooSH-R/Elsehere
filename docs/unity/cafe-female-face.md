# 카페 여자 표정 확인

Tripo 카페 여자 `Assets/Prefabs/Npc/Barista.prefab`을 보존한 표정 복사본이다. 개별 표정 검수 씬과 실제 카페 NPC 대화 연결을 함께 제공한다. 원본 메시·텍스처·프리팹은 변경하지 않는다.

말하는 동안 의문·찡그림·웃음 등 9종 감정을 합성하는 별도 카페 복사본은 [NPC 말하기 감정 검수](npc-speaking-emotions.md)에서 확인한다. 현재 Production의 고정 미소를 자동 감정 판정으로 교체한 기능은 아니다.

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

참고 이미지의 입 모양 10종과 미소·눈 감기에 더해, 별도 음성 검수 씬에서 오프라인 TTS가 생성한 발음 시각과 실제 오디오 재생 위치를 연결한다. 실제 카페 대화 재생 경로는 Kokoro의 발음 순서를 PocketSphinx로 최종 PCM에 정렬한 시간표를 소비한다. 운영 서버 배포와 실제 서비스 음성 수신 여부는 별도로 확인해야 한다. 특히 눈꺼풀 접힘과 F/V·TH의 입술 접촉은 정면·사선에서 외관을 확인해야 한다.

## Blender 원본과 재사용 범위

`frontend/ArtSources/Characters/CafeFemaleFace/CafeFemaleFacePreview.blend`에 Basis와 몸 기준 13개 Shape Key(발음 9개, 미소·눈 감기 4개)가 있다. Blender에서는 객체를 선택한 뒤 Object Data Properties의 Shape Keys에서 값을 조절한다. 이 파일은 얼굴 조형용 메시이며 몸의 기준 골격·가중치는 Unity 원본이 기준이다.

`create_face_shapes.py`는 Unity에서 추출한 정확한 정점 배열을 입력으로 별도 얼굴 후보를 만드는 로컬 제작 스크립트다. 현재 얼굴에 맞춘 위치 기준을 사용한다. `build_speaking_mouth.py`는 복사본의 입 주변 면과 전용 피부 색 텍스처를 구성한다. Unity는 명시적인 변형 법선도 함께 적용한다. Blender의 실시간 Shape Key 음영은 Unity와 차이가 있으므로 최종 외관은 Unity 검수 씬에서 확인한다. 사람이 편집한 blend를 같은 출력 경로로 재생성하지 않는다. `CafeFemaleFaceShapes.json`은 Unity가 읽는 변형 데이터이며 API 호출을 하지 않는다.

**Tools → A305 → Characters → Build Cafe Female Face Preview**는 아직 없는 검수 프리팹·씬을 만든다. 이미 있는 프리팹은 덮어쓰지 않는다. **Refresh Cafe Female Face Shapes**는 동일한 기준 정점 배열의 JSON으로 복사본 몸의 면 목록과 얼굴 채널, 동일한 정점 수의 얼굴 세부 메시를 갱신한다. 몸의 정점 순서나 기존 얼굴 세부 메시의 정점 수를 바꾸는 작업은 갱신 메뉴 범위에 포함되지 않는다.

다른 NPC는 같은 채널 이름으로 제작한 얼굴에 `CharacterFaceController`를 붙여 `SetPose`를 재사용할 수 있다. `BlendPoses(from, to, progress, intensity)`는 두 포즈 사이를 0~1 진행률로 전환하며, 기존 관절 보정 채널을 유지한다. 얼굴 크기·정점 순서가 다른 NPC에 이 모델의 변형 값을 그대로 복사해서는 안 된다. 필수 채널이 빠졌으면 부분 적용 대신 오류를 반환한다.

기존 문장 GIF는 수동 시간표 시안이었다. 현재 음성 검수 씬은 로컬 TTS가 WAV와 함께 생성한 Viseme 시각을 사용한다. 실제 대사의 감정값 연결은 [NPC 대화 감정 표정](npc-dialogue-emotions.md)을 따른다. 운영 서버 배포·WebGL 동기화 확인과 전체 29개 채널의 파이프라인 검증은 별도다. 이 씬의 동작만으로 전체 표정·립싱크 검증을 통과했다고 처리하지 않는다.

`viseme_I`와 `viseme_EU`는 첨부된 한국어 이·으 기준이다. 기존 Tripo 전체 얼굴 검사의 `IH` 및 DD·KK·CH·SS·NN·RR, 추가 감정 채널과 자동으로 동일 취급하지 않는다. 전체 얼굴 검사 기준을 낮추거나 통과 처리하지 않는다.

## 실제 음성으로 확인

1. `Assets/Scenes/Sandbox/CafeFemaleExpressiveSpeechPreview.unity`를 열고 Play를 누른다.
2. **Play / Replay**를 누르면 “May I help you. I find very happy.”를 실제 음성으로 재생한다.
3. **Pause / Resume**은 오디오와 입 모양을 함께 멈추고 이어 재생한다. **Stop**과 자연 종료는 입을 중립으로 복원한다. Replay는 처음부터 시작한다.

`CafeFemaleExpressiveSpeechPreview.prefab`은 음성 검수 프리팹의 별도 변형이다. 기존 `CafeFemaleSpeechPreview`는 Viseme ID 방식 비교본으로 보존한다. 원본 `Barista.prefab`과 기존 표정 확인 씬은 변경하지 않는다. 음성과 시간표는 `Assets/Audio/Characters/CafeFemale/`, 두 파일을 묶는 `CharacterSpeechClip`은 `Assets/Data/Characters/CafeFemaleExpressiveSpeech.asset`에 있다. 친근한 미소 강도·첫 눈 깜박임·간격은 `FriendlySpeechExpression.asset`에서 조절한다. 재생 중 음량이 들리지 않으면 Game 창의 Mute Audio와 OS 출력 장치를 확인한다.

`CharacterSpeechPlayer`는 프레임 수나 경과 시간을 누적하지 않고 `AudioSource.timeSamples / frequency`로 현재 발음을 찾는다. `SpeechVisemeTimeline`은 새 v2 음소 데이터에서 각 발음 내부와 인접 발음 사이를 보간한다. 무음·종료에서는 입이 중립으로 돌아간다. 기존 v1 Viseme 전용 데이터의 재생도 유지한다. 입력 형식/Viseme 범위/구간 순서/음성 길이가 잘못되면 재생을 거부한다. Builder는 WAV의 SHA-256이 시간표와 일치하는지도 확인한다. `CharacterFaceController.ApplySpeech`가 발음·미소·눈 깜박임을 한 번에 적용한다. 눈꺼풀은 발음과 독립적이고, 입술 접촉이나 둥근 모음이 우선되도록 미소의 입꼬리 영향을 낮춘다. F/V·TH/DH는 해당 음소의 중앙 구간에서 제작된 접촉 모양을 온전히 적용한다. 단일 시점의 약한 정점만 사용하지 않으며, 진입·이탈은 인접 발음과 보간한다. 이 접촉이 완성되는 동안에는 미소로 입꼬리가 당겨지지 않게 한다. 관절 보정 채널은 바꾸지 않는다. 일시정지하면 표정 시계도 정지하고 종료하면 모두 중립으로 돌아간다.

음성 에셋 회귀 검사는 원본 WAV의 샘플레이트·채널과 가져온 클립의 재생 길이·발음 시간표 일치를 구분한다. Unity가 대상 플랫폼에 맞춰 리샘플링할 수 있으므로 가져온 클립의 주파수를 원본과 같다고 가정하지 않는다. 실제 재생 시계는 해당 클립의 `timeSamples / frequency`를 사용한다. 에디터 검사는 CI와 같은 WebGL 대상으로 실행하고 Blender·마스터 골격 검사 입력도 준비한다. 이 검사는 브라우저에서 실제 음성과 입 모양을 대조하는 검수를 대신하지 않는다.

### 새 로컬 음성 만들기

Windows PowerShell 5.1에서 실행한다. 설치된 en-US 음성 Microsoft Zira를 사용하며 인터넷·계정·유료 API가 필요 없다. 이미 있는 결과는 덮어쓰지 않는다.

```powershell
powershell.exe -NoProfile -File frontend/ArtSources/Characters/CafeFemaleFace/export_local_speech.ps1 -Text "May I help you. I find very happy." -OutputDirectory <새-출력-폴더>
```

생성된 WAV/JSON 한 쌍을 Unity에 함께 가져온 뒤 **Create → A305 → Characters → Speech Clip**으로 만든 데이터 에셋에 연결한다. 같은 얼굴 채널을 가진 NPC에서 `CharacterSpeechPlayer`의 Face, Speech Source, Speech를 설정하면 같은 재생기를 사용한다. 얼굴 메시를 다시 만들거나 수동으로 문장별 키프레임을 찍을 필요는 없다. 임의 WAV만 넣어 음소를 인식하는 기능은 아니다.

이 내보내기는 [Microsoft System.Speech PhonemeReached](https://learn.microsoft.com/en-us/dotnet/api/system.speech.synthesis.speechsynthesizer.phonemereached?view=netframework-4.8)와 오디오 시각을 함께 저장한다. Desktop SAPI가 반환하는 음소 ID는 en-US `SpPhoneConverter`로 ay/iy/y/uw 등의 기호로 변환한다. PCM은 이벤트와 같은 16kHz로 유지하고 타임스탬프가 음성 길이를 벗어나면 결과를 사용하지 않는다. `version: 2`의 `phonemes`가 새 재생 기준이며 기존 `cues`는 비교를 위해 함께 남긴다.

- I/find의 `ay`는 열린 입에서 짧고 약한 /ɪ/로 변한다. 한국어 ‘이’ 모양을 100% 고정하지 않는다.
- you의 `y`는 뒤의 `uw`를 준비하는 둥근 입으로 이어진다. /j/를 긴 ‘이’ 모양으로 표시하지 않는다.
- 발음별 도달 지점을 연결해 앞소리의 잔여 움직임과 다음 소리 준비를 섞는다. P/B/M은 입술을 닫는 구간을 확보한다.
- 미소는 친근한 프리셋이다. LLM이 문장 감정을 분석해 자동 선택하는 기능은 아니다. 눈 깜박임은 같은 오디오 시계를 사용하고 닫힘·짧은 유지·느린 열림을 구분한다.

기존 메시에는 세밀한 혀·치아 조형이 없는 자음이 있으므로 해당 발음은 가까운 모양으로 근사한다. 지원하지 않는 음소는 조용히 다른 입으로 바꾸지 않고 오류를 반환한다. 한국어 TTS 및 다른 공급자의 이벤트는 별도 매핑이 필요하다.

`DialogueServerLink`는 아래 경로로 서버 WAV 안의 발음 시간표를 읽는다. 기존 Windows 음성 검수 씬도 유지한다. WebGL·운영 서버에서의 실제 수신과 동기화는 별도 검증 대상이다.

## 실제 카페 대화 연결

`CafeScene`과 `CafeInteriorSandbox`의 `CAFE_STAFF`는 `CafeFemaleDialogue.prefab`을 사용한다. 이 프리팹은 원본 `Barista.prefab`을 자식으로 포함하고 복사본 얼굴 메시·구강 부품·표정 플레이어를 연결한다. 원본 Avatar·몸 Animator Controller와 관절 보정은 유지한다. NPC 코드·접근 범위·시선 대상·캡슐과 플레이어 카메라 설정은 변경하지 않는다. **Tools → A305 → Cafe → Connect Dialogue Face**로 연결을 다시 맞출 수 있으며, 기존 대화 프리팹의 외관을 재생성하지 않는다.

대화 시작 시 `DialogueController`가 선택한 시선 대상의 부모 `DialogueSpeaker`를 찾는다. 서버의 기존 음성 GET 결과를 `DialogueServerLink` → `DialogueSpeechWav` → `CharacterSpeechPlayer` 순으로 전달한다. 플레이어는 다운로드한 PCM의 `AudioSource.timeSamples`를 기준으로 입모양·깜박임·미소를 함께 적용한다. 예제 두 문장이나 대사 철자에 종속되지 않는다. 음성 파일 안의 `a305` 시간표는 해시·길이·음소 범위를 검증한다. 발음 구간은 기존 부드러운 보간과 입술 접촉 우선 규칙을 재사용한다.

다시 듣기는 현재 대사의 받은 음성을 재사용한다. 새 대사에 음성이 없으면 이전 음성과 시간표를 버리며, 그 상태에서 사용자가 다시 듣기를 누르면 기존 대화 기능의 음성 생성 요청으로 이어진다. 대화 종료·화자 변경·씬 해제는 음성과 표정을 정지하고 늦게 끝난 이전 다운로드를 무시한다. 아직 얼굴 채널이 없는 NPC는 기존 AudioSource로 소리만 재생한다. 시간표가 없는 과거 WAV 또는 잘못된 시간표는 소리를 유지하되 발음을 추측해서 움직이지 않는다. `DialogueServerLink.LipSyncNotice`와 Console 경고로 확인할 수 있고, 음성을 자동 재생성하지 않는다.

**서버 조건:** [AI 워커의 NPC 발음 시간표 설정](../../ai/worker/README.md#npc-발음-시간표와-립싱크)을 적용해야 한다. 같은 이름의 구형 Kokoro 모델에는 duration 출력이 없을 수 있다. Unity만 갱신해도 오래된 서버 음성에 시간표가 생기는 것은 아니다. 현재 감정은 `FriendlySpeechExpression`의 친근한 미소 프리셋이며, LLM이 문장의 감정을 판정하는 기능은 별개다.

### 다른 NPC의 자동 연결

`NpcIdentity`는 실행 시 같은 NPC 루트에 `DialogueSpeaker`를 자동으로 준비한다. 대화 시작 때 선택된 `LookTarget`의 부모에서 화자를 찾고, 그 NPC 하위의 활성 얼굴만 검사한다. NPC 코드나 카페 이름으로 분기하지 않는다. 기존에 지정한 얼굴·음성 재생기·감정 프로필은 유지하며, 연결이 없으면 공통 재생기와 전용 AudioSource, 기본 미소·깜박임 프로필을 실행 중에 구성한다. 얼굴 메시·프리팹·씬 파일은 저장하거나 수정하지 않는다.

자동 연결에는 `mouthSmileLeft`, `mouthSmileRight`, `eyeBlinkLeft`, `eyeBlinkRight`, `viseme_AA`, `viseme_FF`, `viseme_TH`, `viseme_PP`, `viseme_E`, `viseme_OH`, `viseme_OU`, `viseme_I`, `viseme_EU`가 필요하다. 채널은 같은 얼굴의 여러 SkinnedMeshRenderer에 나뉘어 있어도 된다. 실제 변형은 모델에서 미리 제작해야 하며, 공통 골격만 있는 새 Tripo 모델에 얼굴 변형을 생성하는 기능은 아니다. 여러 얼굴·재생기가 있어 자동으로 고를 수 없으면 명시적으로 연결해야 한다. 다른 NPC에 속한 얼굴은 사용하지 않는다.

미지원 모델은 음성만 재생한다. 누락 채널은 실행 중 `DialogueSpeaker`의 Binding Notice와 `DialogueServerLink.LipSyncNotice`에서 확인할 수 있다. **Tools → A305 → Characters → Check NPC Speech Support**는 현재 열린 씬의 NPC별 채널 상태를 Console에 출력한다. 배치 검사는 `NPC_SPEECH_AUDIT_OUTPUT`을 저장소 밖 JSON 경로로 지정하고 `A305.EditorTools.Characters.NpcSpeechAudit.ExportProductionReport`를 실행한다. Production 씬에 배치된 NPC를 읽기만 하며, 동적으로 생성되는 NPC나 변형의 시각적 품질까지 합격시키는 검사는 아니다.

호텔리어의 공통 채널 복사본과 1.7m 크기·호텔 대화 연결은 [호텔리어 표정과 대화](hotelier-face.md)에서 확인한다.

### 서버 호출 없이 카페에서 확인

1. `Assets/Scenes/Sandbox/CafeDialogueSpeechSandbox.unity`를 열고 Play를 누른다.
2. 화면 아래 **Would you like coffee or tea?** 또는 **Thank you for visiting. Your coffee will be ready in five minutes.** 버튼을 누른다.
3. 카페 NPC의 입모양·깜박임·미소와 몸 Idle 동작이 함께 나오는지 확인한다. **멈춤**으로 입과 눈이 중립으로 돌아오는지 확인한다.

이 씬은 대화 서버를 끈 복사본이며, 무료 Kokoro로 미리 생성한 WAV를 실제 대화와 같은 디코딩·재생 경로로 전달한다. 화면 버튼은 LLM이나 TTS API를 호출하지 않는다. 실제 서비스 연결은 `CafeScene`에서 기존 NPC 대화를 시작해 확인한다. 그 경우 기존 서비스의 대사 생성·음성 정책을 따른다.

### 실제 워커 음성으로 영상 검수

AI 워커의 `/v1/tts` 또는 `/v1/audio/{id}`에서 받은 WAV를 저장한 뒤 `CAFE_DIALOGUE_REVIEW_WAV`에 파일 절대 경로, `CAFE_DIALOGUE_REVIEW_OUTPUT`에 저장소 밖 출력 폴더를 지정합니다. Editor가 닫힌 상태에서 Unity batchmode의 `-executeMethod A305.EditorTools.Characters.CafeDialogueBuilder.RenderSpeechVideo -quit`를 실행하면 카페 검수 씬의 실제 NPC와 Idle 애니메이션을 사용한 30fps PNG 프레임이 생성됩니다. 같은 WAV를 오디오 트랙으로 합치면 발음 시간과 프레임 시간이 일치하는 영상을 얻습니다. `CAFE_DIALOGUE_REVIEW_FPS`는 1~120 범위이며 기본값은 30입니다. 120fps로 생성한 프레임을 30fps로 재생하고 같은 음성도 0.25배로 재생하면 전환을 생략하지 않는 느린 비교가 가능합니다. 느린 재생은 검수 영상에만 적용하며 실제 서비스 음성을 변경하지 않습니다. 함께 생성되는 `MouthSamples.csv`에는 실제 음성 시각별 입모양 목표 가중치가 기록됩니다. 감정 합성 전 목표값이므로 최종 메시 형태는 PNG에서도 확인해야 합니다.

이 검수는 원본 모델·프리팹·씬을 저장하지 않고 `DialogueServerLink`의 WAV 검증과 `CharacterSpeechPlayer`의 표정 합성을 그대로 사용합니다. 시간표가 없거나 음성과 일치하지 않으면 렌더를 실패시킵니다. 고정된 두 예문 외의 대사도 검수할 수 있습니다. 영상 렌더 통과는 배포 서버나 WebGL에서의 전체 로그인·대화 연동 검증을 대신하지 않습니다.

검수 대사에는 F/V(예: “We have fresh coffee and vanilla muffins.”), 유성·무성 TH(“Those three muffins are for you. Thank you!”), 빠른 연결 발음(“Would you like your coffee with milk, or would you prefer it black?”)을 포함한다. 정상 속도와 빠른 합성 속도를 구분해 기록하고, 비교 양쪽에는 같은 WAV를 사용한다. 발음 구간 안의 목표 가중치뿐 아니라 치아·아랫입술·혀가 실제로 보이는지 확대 영상으로 확인한다. 음향 정렬 결과나 가중치만으로 모든 발음의 외관·정확도가 합격이라고 판단하지 않는다.
