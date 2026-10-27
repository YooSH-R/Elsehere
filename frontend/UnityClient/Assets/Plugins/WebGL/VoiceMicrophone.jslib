// 브라우저 마이크 연결. 소리를 모으고 음량을 재는 일만 하고, 녹음 규칙·WAV 변환은 Unity(C#)가 맡는다.
// Unity의 Microphone 클래스는 Web 빌드에서 동작하지 않아 Web Audio로 직접 받는다.
var VoiceMicrophone = {
  $voice: {
    state: 0, // 0 요청 전·답 기다림, 1 허용, 2 실패(errorName)
    errorName: "",
    recording: false,
    stream: null,
    context: null,
    source: null,
    processor: null,
    chunks: [],
    level: 0,
    samples: null,
    constraints: { audio: { channelCount: 1, echoCancellation: true, noiseSuppression: true } },

    isSupported: function () {
      return window.isSecureContext && navigator.mediaDevices && navigator.mediaDevices.getUserMedia;
    },

    fail: function (name) {
      voice.errorName = name || "Error";
      voice.state = 2;
      voice.recording = false;
    },

    closeStream: function () {
      if (voice.processor) {
        voice.processor.onaudioprocess = null;
        voice.processor.disconnect();
      }
      if (voice.source) voice.source.disconnect();
      if (voice.stream) voice.stream.getTracks().forEach(function (track) { track.stop(); });
      voice.processor = null;
      voice.source = null;
      voice.stream = null;
    },
  },

  A305Voice_RequestAccess__deps: ["$voice"],
  A305Voice_RequestAccess: function () {
    if (!voice.isSupported()) {
      voice.fail("Unsupported");
      return;
    }
    voice.state = 0;
    // 권한만 받고 바로 닫는다. 마이크는 녹음할 때만 열어 탭의 마이크 표시가 계속 켜져 있지 않게 한다.
    navigator.mediaDevices
      .getUserMedia(voice.constraints)
      .then(function (stream) {
        stream.getTracks().forEach(function (track) { track.stop(); });
        voice.state = 1;
      })
      .catch(function (error) { voice.fail(error.name); });
  },

  A305Voice_GetAccessState__deps: ["$voice"],
  A305Voice_GetAccessState: function () {
    return voice.state;
  },

  A305Voice_GetErrorName__deps: ["$voice"],
  A305Voice_GetErrorName: function () {
    var size = lengthBytesUTF8(voice.errorName) + 1;
    var buffer = _malloc(size);
    stringToUTF8(voice.errorName, buffer, size);
    return buffer;
  },

  A305Voice_StartRecording__deps: ["$voice"],
  A305Voice_StartRecording: function () {
    if (!voice.isSupported()) {
      voice.fail("Unsupported");
      return;
    }
    voice.chunks = [];
    voice.level = 0;
    voice.recording = true;
    // 녹음할 때마다 마이크를 다시 열므로 권한 답도 새로 받는다. 전에 거부했어도 설정을 바꿨으면 이번에는 열릴 수 있다.
    voice.state = 0;
    navigator.mediaDevices
      .getUserMedia(voice.constraints)
      .then(function (stream) {
        // 마이크가 열리기 전에 녹음을 끝냈으면 바로 닫는다.
        if (!voice.recording) {
          stream.getTracks().forEach(function (track) { track.stop(); });
          return;
        }
        voice.state = 1;
        voice.stream = stream;
        voice.context = voice.context || new (window.AudioContext || window.webkitAudioContext)();
        voice.context.resume();
        voice.source = voice.context.createMediaStreamSource(stream);
        // ScriptProcessor는 오래된 방식이지만 Safari를 포함한 모든 브라우저가 지원하고 별도 스크립트 파일이 필요 없다.
        voice.processor = voice.context.createScriptProcessor(4096, 1, 1);
        voice.processor.onaudioprocess = function (event) {
          var chunk = new Float32Array(event.inputBuffer.getChannelData(0));
          voice.chunks.push(chunk);
          var sum = 0;
          for (var i = 0; i < chunk.length; i++) sum += chunk[i] * chunk[i];
          voice.level = Math.sqrt(sum / chunk.length);
        };
        voice.source.connect(voice.processor);
        voice.processor.connect(voice.context.destination);
      })
      .catch(function (error) { voice.fail(error.name); });
  },

  A305Voice_GetLevel__deps: ["$voice"],
  A305Voice_GetLevel: function () {
    return voice.level;
  },

  A305Voice_StopRecording__deps: ["$voice"],
  A305Voice_StopRecording: function () {
    voice.recording = false;
    voice.closeStream();
    var total = 0;
    voice.chunks.forEach(function (chunk) { total += chunk.length; });
    voice.samples = new Float32Array(total);
    var offset = 0;
    voice.chunks.forEach(function (chunk) {
      voice.samples.set(chunk, offset);
      offset += chunk.length;
    });
    voice.chunks = [];
    voice.level = 0;
    return total;
  },

  A305Voice_GetSampleRate__deps: ["$voice"],
  A305Voice_GetSampleRate: function () {
    return voice.context ? voice.context.sampleRate : 0;
  },

  // Unity가 만든 float 배열의 메모리에 모아 둔 소리를 복사한다.
  A305Voice_CopySamples__deps: ["$voice"],
  A305Voice_CopySamples: function (buffer, length) {
    HEAPF32.set(voice.samples.subarray(0, length), buffer >> 2);
    voice.samples = null;
  },
};

mergeInto(LibraryManager.library, VoiceMicrophone);
