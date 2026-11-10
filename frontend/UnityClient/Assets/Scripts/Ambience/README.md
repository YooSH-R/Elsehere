# 장소 생활소음과 효과음

승인받은 서비스용 음원을 장소에 맞게 반복 재생하고, 실제 보행에는 선택한 저음 발걸음을 재생합니다. 추가 외부 API나 AI 호출은 없고 새 배경음악은 추가하지 않습니다. 기존 이동·대화·날씨 처리는 바꾸지 않고 현재 상태만 관찰합니다.

| 상황 | 반복음 |
| --- | --- |
| 집·숙소 방 | 09_room |
| 거리 | 02_street_speech |
| 호텔 로비 | 01_hotel |
| 카페 | 03_cafe |
| 식당 | 04_restaurant |
| 빨래방 | 05_laundromat |
| 택시 승객 | 06_taxi |
| 다른 작은 매장·경찰서 | 08_quiet_shop |
| 비 오는 거리 | 07_umbrella_rain |
| 로그인·테스트 씬 | 재생하지 않음 |

장소 전환은 두 AudioSource 사이에서 약 1초에 걸쳐 교차 페이드합니다. 우산 음원에는 아주 작은 거리 배경이 포함되어 있어 비 오는 거리에서는 일반 거리 음원을 동시에 겹치지 않습니다. 생활소음·효과음의 평상시 음량 계수는 0.6으로, 이전 0.8보다 25% 낮습니다. 대화 중에는 이 음량의 15%로 낮추고 대화가 끝나면 부드럽게 복원합니다. NPC 음성과 AudioListener의 전체 음량·일시정지 값은 변경하지 않습니다.

일반 문은 실제 장소 전환 때, 택시 문은 승객 상태가 바뀔 때 한 번 재생합니다. 호텔 프런트 직원과 상호작용을 시작하면 안내 벨을 울리며 20초 이내 재반복을 막습니다. 차량 통과음은 실제 움직이는 가까운 차량에 3D로 붙이고, 차량이 멀어졌다 돌아와야 다시 납니다. 경적은 녹색 신호에서 플레이어 가까이 2초 이상 막힌 차량에만 낮은 음량으로 재생하며 40초 재사용 제한을 둡니다.

## 에셋 연결

원본 승인 파일은 사용자가 준비한 서비스용 생활소음 폴더입니다. 생활소음에는 `Assets/Audio/Ambience/Loops`의 OGG 9개와 `Effects`의 OGG 5개를 사용합니다. `audio-manifest.json`은 원본 제작 측정 기록(WAV 기준)이며 런타임 파일 목록이 아닙니다. 카페 후반의 이름 부르는 구간과 인위적인 발자국은 완성 음원에서 제외되어 있습니다.

**Tools → A305 → Audio → Build Service Ambience** (`ServiceAmbienceBuilder.Build`)가 OGG 가져오기 옵션과 `Assets/Resources/Ambience/ServiceAmbience.asset`을 연결·검증합니다. 반복음은 Vorbis·CompressedInMemory·비동기 읽기·시작 시 선로딩 안 함, 짧은 효과음은 모노·DecompressOnLoad입니다. Resources 카탈로그를 런타임 초기화가 읽어 하나의 지속 오디오 오브젝트를 만들기 때문에 배포용 씬·기존 프리팹을 다시 디자인할 필요가 없습니다. 빌드 전에 Builder로 생성된 카탈로그와 Unity가 만든 meta를 함께 커밋합니다.

## 검증

`A305.Ambience.Tests`는 장소 선택·실내 비 차단·택시 우선·대화 감쇠·차량 재사용 제한과 실제 카탈로그 14개 연결·루프 가져오기 옵션을 확인합니다. 외부 AI 호출 없이 실행합니다. 런타임 자동 재생은 Editor batchmode 테스트에서는 꺼서 다른 테스트의 오디오 환경에 영향을 주지 않습니다. 실제 WebGL 청음과 브라우저 오디오 잠금 해제는 배포 검수에서 확인합니다.

## 실제 보행 발걸음

메인 화면 오른쪽 위의 스피커로 생활소음·환경 효과음·발자국을 함께 켜고 끕니다. `BackgroundSoundPreference`가 기기의 PlayerPrefs에 선택을 저장하며, 음소거 중에도 장소 루프의 재생 위치는 유지합니다. NPC 음성에는 적용하지 않습니다. `BackgroundSoundBuilder.Build`가 HUD에 버튼과 켜짐·꺼짐 그림을 연결합니다.

`FootstepPlayer`는 플레이어와 `NpcIdentity`를 가진 NPC의 CharacterController를 관찰합니다. 목록은 1초마다 갱신하며 매 프레임 전수 검색하지 않습니다. 접지한 실제 수평 이동이 쌓일 때만 단발음을 냅니다. 벽을 밀기·서 있기·앉기·공중 이동·플레이어 이동 잠금·택시 탑승·순간이동·일시정지에는 보행음을 내지 않습니다. 걸음은 0.85m 거리와 최소 0.28초 간격을 함께 적용합니다. 멈추면 남은 거리를 버려 재출발 직후 소리가 튀지 않습니다.

사용자는 2D 기본 음량 0.42, NPC는 3D 음량 0.065와 1~12m 거리 감쇠를 적용합니다. 대화 중에는 각각 15%로 낮춥니다. AudioListener의 전역 음량과 일시정지를 그대로 따릅니다. 이동·애니메이션·카메라 코드는 바꾸지 않습니다.

**Tools → A305 → Audio → Build Footsteps** (`FootstepAudioBuilder.Build`)가 `Footsteps.asset`을 생성합니다. `Audio/Ambience/Footsteps`의 단발 WAV 6개(전체 약118KiB)를 모노·Vorbis·DecompressOnLoad로 가져옵니다. 첫 뒤꿈치 충격음을 덜어 내고 남은 밑창 소리를 부드럽게 가공했습니다. 연속 청음 샘플이나 비교 음원은 포함하지 않습니다. 출처는 해당 폴더의 `provenance.json`에 기록합니다. 기존 장소 카탈로그와 독립적으로 연결합니다.

보행 정책 테스트는 정상 걸음, 벽·공중·이동 잠금·순간이동·일시정지·긴 프레임 무음, 정지 시 잔여 거리 초기화를 확인합니다. 실제 캐릭터의 발 착지와 청감은 Unity/WebGL에서 별도 확인합니다.


## UI 효과음과 접속 화면 음악

`InterfaceAudioPlayer`는 기존 생활소음과 별도의 지속 오디오 오브젝트입니다. `UiSoundEvents`로 전달된 실제 화면 동작만 재생하며, 일반 클릭은 활성화된 버튼의 실제 클릭·키보드 제출에서 발생합니다. 마이크·녹음 버튼은 클릭음을 넣지 않습니다. 동적으로 생긴 버튼도 연결하며, 프로그램이 직접 `onClick`을 호출한 경우에는 클릭음을 만들지 않습니다.

| 동작 | 음원 | 재생 조건 |
| --- | --- | --- |
| 일반 클릭 | click.ogg | 누를 수 있는 버튼을 실제로 누름 |
| 휴대폰 열기·닫기 | menu.ogg | 휴대폰 메뉴 버튼을 누름 |
| 코인 획득 | coin.ogg | 이미 알고 있는 잔액이 증가함 |
| 구매·코인 지출 | spend.ogg | 이미 알고 있는 잔액이 감소함. 잔액 감소 표시와 칩 흔들림을 함께 사용 |
| 퀘스트 완료 | quest.ogg | 완료 팡파레를 실제로 표시함 |
| 표현 저장 성공 | saved.ogg | 서버가 새로 저장했다고 응답함. 실패·중복 저장은 제외 |

처음 받은 잔액·동일한 잔액 재수신은 효과음이 없습니다. 연타와 중복 보상은 `UiSoundGate`로 간격을 제한하고, 퀘스트 완료음 직후의 획득음은 겹치지 않게 합니다. UI 효과음과 접속 음악도 기존 스피커의 `BackgroundSoundPreference`를 따르며 NPC 음성은 변경하지 않습니다.

접속 음악은 게임 파일을 받는 로딩 화면과 입구 화면(`EntryScene`)에서 작게 반복됩니다. Web 빌드에서는 로딩 페이지(`WebGLTemplates/A305Responsive/index.html`)가 같은 곡의 MP3(`entry-town-ensemble.mp3`)를 Web Audio로 틀기 시작합니다. Unity가 켜지면 `InterfaceAudioPlayer`가 그 음악을 지금 음량 그대로 이어받아(`LoadingPageMusic`, `Plugins/WebGL/LoadingPageMusic.jslib`) 음량과 재생을 정하므로, 입구 화면에서 끊기거나 처음부터 다시 시작하지 않습니다. 브라우저가 자동재생을 막으면 로딩 화면이나 입구 화면의 첫 클릭·터치·키 입력 뒤에 들리며, 자동재생 제한을 우회하지 않습니다. Google 로그인처럼 페이지를 떠나는 동안에는 끊기고, 돌아와 로딩 화면이 다시 열리면 처음부터 틉니다. 로딩 페이지에 음악이 없으면(Editor, 음원을 받지 못함) Unity가 첫 클릭·터치·키 입력 뒤에 자기 음악을 작게 시작해 키웁니다. 게임 공간으로 이동하면 약 0.5초에 걸쳐 꺼지고 기존 장소별 생활소음이 이어집니다. 언제 틀고 얼마나 크게 낼지는 `EntryMusicFader`가 정합니다. 스피커를 끄면 접속 음악도 꺼지고, 이 선택을 브라우저 저장소(`elsehere.audio.background.enabled`)에도 남겨 다음 로딩 화면에서도 틀지 않습니다.

음악은 외부 곡·샘플 없이 직접 합성한 약 31초 루프이며 원본 합성 도구는 `infra/audio/render-entry-music.py`입니다. 같은 도구가 OGG(Unity용)와 함께 만드는 160kbps MP3를 로딩 페이지에 씁니다. Safari도 재생할 수 있도록 OGG 대신 MP3를 씁니다. 버전·길이·효과음 출처·라이선스는 `Assets/Audio/Interface/audio-manifest.json`과 `CREDITS.txt`에 기록합니다.

`EntryMusicFaderTests`가 로딩 페이지 음악을 입구 화면에서 클릭 없이 잇기, 게임 공간에서 약 0.5초에 걸쳐 끄기, 스피커를 꺼 둔 로딩 화면 뒤에 스피커를 켜면 틀기, 로딩 페이지 음악이 없을 때 첫 클릭 뒤에 시작하기를 확인합니다. 로딩 페이지와의 연결(`.jslib`)은 Editor에서 실행되지 않으므로 Web 빌드를 브라우저에서 열어 확인합니다.

**Tools → A305 → Audio → Build Interface Audio** (`InterfaceAudioBuilder.Build`)가 가져오기 설정과 `Resources/Ambience/InterfaceAudio.asset`을 연결합니다. 음악은 스테레오 Vorbis·CompressedInMemory·비동기 읽기, 짧은 효과음은 모노·DecompressOnLoad입니다. 씬·기존 화면 프리팹을 재생성하지 않습니다.
