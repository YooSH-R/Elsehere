# Voice 모듈

[말하기] 녹음입니다. 한 번 누르면 녹음을 시작하고 다시 누르면 끝냅니다. 마이크 권한을 확인하고, 녹음 중 음량을 재서 목소리가 들리는지와 자동 종료를 판단하고, 끝나면 음성 인식 API(CONV-02)에 보낼 16kHz 모노 16비트 WAV를 만듭니다. 서버 연동과 대화 화면은 참조하지 않는 재사용 모듈입니다. 음성 인식 요청은 대화 기능에서 이 모듈의 결과(`RecordedVoice.WavBytes`)를 받아 보냅니다. `RecordedVoice.HasSpeech`는 무음 기준보다 큰 소리가 합쳐서 0.25초 이상인지 보아, 말하지 않고 끝낸 녹음을 보내기 전에 거를 수 있게 합니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `VoiceRecorder` | 녹음 컴포넌트. 플랫폼에 맞는 마이크를 골라 `VoiceRecordingFlow`에 넘기고 매 프레임 시각을 전달합니다. 녹음 중 다른 탭·창으로 가면 멈춥니다. |
| `VoiceRecordingFlow` | 녹음 진행. 권한 답을 알리고, 매 프레임 음량과 시각을 `RecordingSession`에 넘겨 자동 종료하고, 끝나면 16kHz WAV를 만들어 `RecordingFinished`로 알립니다. 녹음을 시작하며 마이크를 열지 못하면 녹음을 취소합니다. 가짜 마이크로 테스트합니다. |
| `RecordingSession` | 녹음 규칙. 최대 시간, 조용한 상태가 이어지는 시간, 사용자가 끝냄, 중단, 지금 목소리가 들리는지. Unity 객체 없이 테스트합니다. |
| `AudioResampler`, `WavEncoder` | 마이크 샘플레이트(브라우저는 보통 44.1kHz·48kHz)를 16kHz로 줄이고 WAV로 만듭니다. |
| `IMicrophone`, `BrowserMicrophone`, `UnityMicrophone` | Web 빌드는 브라우저 API(`Plugins/WebGL/VoiceMicrophone.jslib`), Editor·데스크톱은 Unity `Microphone`을 씁니다. Unity `Microphone`은 Web 빌드에서 동작하지 않습니다. |
| `BrowserMicrophoneErrors`, `MicrophoneAccess` | 브라우저 오류 이름을 권한 거부·장치 없음·지원 안 함으로 나눕니다. |
| `VoiceRecorderSettings` | 최대 녹음 시간(15초), 조용한 시간(3초), 조용하다고 볼 음량(`Data/Voice/VoiceRecorderSettings.asset`) |
| `VoiceSandboxView` | 확인용 화면(Sandbox 전용). [말하기]를 눌러 시작·끝내기, 음량 막대, 목소리 표시 |

## 동작 규칙

| 상황 | 동작 |
| --- | --- |
| 권한 요청 | 브라우저에 권한을 묻고 마이크를 바로 닫습니다. 권한은 브라우저가 기억합니다. HTTPS(또는 localhost)가 아니면 지원 안 함입니다. |
| 마이크 미리 열기 | 대화가 시작되면 `KeepMicrophoneOpen`으로 마이크를 열어 두고, 대화가 끝나면 `ReleaseMicrophone`으로 닫습니다. 브라우저는 마이크를 여는 데 0.5~1초가 걸려, 누를 때마다 열면 첫 말이 녹음에서 빠지기 때문입니다. 이 페이지에서 마이크를 연 적이 있거나 브라우저가 이미 허용한 경우에만 미리 열어 권한 창을 갑자기 띄우지 않습니다. 열어 둔 동안은 탭에 마이크 표시가 켜져 있습니다. |
| 녹음 | 열어 둔 마이크가 있으면 누르는 즉시 소리를 받고, 없으면 그때 엽니다. 약 0.1초 조각마다 소리를 모으고 음량(RMS)을 잽니다. 녹음 중이 아닐 때는 소리를 모으지 않고, 녹음 중에도 서버로 아무것도 보내지 않습니다. |
| 준비 중 | 녹음을 시작했지만 마이크가 아직 열리는 중이면 `IsPreparing`이 true입니다. 대화창은 버튼에 [준비 중]을 보이고, 소리를 받기 시작하면 [종료]로 바꿉니다. 무음·최대 시간은 소리를 받기 시작한 때부터 셉니다. |
| 목소리 표시 | 녹음 중 이번 프레임 음량이 조용하다고 볼 음량 이상이면 `IsHearingVoice`가 true입니다. 녹음이 끝나면 false입니다. 대화창은 이 값으로 [종료] 버튼의 불을 초록색으로 켭니다. |
| 마이크를 열지 못함 | 권한을 묻지 않고 바로 눌렀다가 거부했거나, 거부한 권한으로 다시 누르면 녹음을 취소하고 `RecordingFinished` 대신 `AccessChanged`로 권한 답을 알립니다. |
| 자동 종료 | 녹음 15초에 닿거나, 음량이 기준보다 작은 상태가 3초 이어지면 끝냅니다. 소리가 들리면 조용한 시간을 처음부터 다시 셉니다. 처음부터 말하지 않아도 3초에 끝납니다. |
| 끝 | 모은 소리를 16kHz로 줄여 WAV로 만듭니다. 15초 녹음이 약 470KB로 서버 제한(3MB)보다 작습니다. |

- 무음 판단은 음량 기준이라, 주변이 시끄러우면 최대 시간까지 갈 수 있고 아주 작게 말하면 일찍 끝날 수 있습니다. 기준 음량은 설정 에셋에서 조정합니다. 서버는 무음 녹음을 `STT_NO_SPEECH`로 한 번 더 거릅니다.
- 브라우저 소리 처리는 `ScriptProcessor`를 씁니다. 표준에서는 오래된 방식이지만 Safari를 포함한 모든 브라우저가 지원하고 별도 스크립트 파일이 필요 없습니다.

## 사용 방법

```csharp
recorder.RecordingFinished += voice => SendToSpeechApi(voice.WavBytes); // audio/wav
talkButton.onClick.AddListener(() =>
{
    if (recorder.IsRecording)
        recorder.StopRecording();
    else
        recorder.StartRecording();
});
// 녹음 중에는 매 프레임 recorder.IsHearingVoice로 목소리가 들리는지 표시한다.
```

녹음 프리팹은 `Prefabs/Voice/VoiceRecorder.prefab`이며, 처음 권한을 묻는 위치(기능 명세 F-08)는 아직 정하지 않았습니다.

## 확인 방법

1. **Tools → A305 → Voice → Build**로 설정·프리팹·`Scenes/Sandbox/VoiceSandbox.unity`를 만듭니다. Editor에서 Play하면 PC 마이크로 녹음을 확인할 수 있습니다.
2. 브라우저 마이크는 **Tools → A305 → Voice → Build Voice Check Web**으로 만든 `Builds/VoiceCheck`를 localhost나 HTTPS 주소로 열어 확인합니다. 권한 허용·거부, [말하기]로 시작·끝내기와 목소리 표시, 3초 무음 종료, 15초 종료, 다시 듣기를 확인합니다.

## 테스트

`Tests/EditMode/Voice/`(`A305.Voice.Tests`)에서 WAV 형식, 16kHz 변환, 녹음 규칙, 브라우저 오류 구분, 가짜 마이크(`FakeMicrophone`)로 녹음 진행(권한 답, 녹음 끝, 자동 종료, 마이크 실패 시 취소, 마이크가 열리기 전 준비 중)을 검증합니다. 브라우저 마이크 연결(`VoiceMicrophone.jslib`)은 Editor 테스트에서 실행되지 않으므로 브라우저에서 확인합니다.

`TextPronunciation`은 학습자 영어 표현의 [듣기]를 담당합니다. WebGL은 기존 브라우저 영어 음성을, Windows Editor·데스크톱은 설치된 영어 음성을 사용합니다. Windows 음성은 숨겨진 별도 프로세스에서 실행하고 문장을 표준 입력으로만 전달합니다. 다음 발화·녹음·NPC 재생·대화 종료 때 중지하며, 기기에서 재생할 수 없으면 대화창에 안내합니다. 다른 Editor 플랫폼은 현재 지원하지 않습니다.
