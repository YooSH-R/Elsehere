// 브라우저 마이크 연결. 소리를 모으고 음량을 재는 일만 하고, 녹음 규칙·WAV 변환은 Unity(C#)가 맡는다.
// Unity의 Microphone 클래스는 Web 빌드에서 동작하지 않아 Web Audio로 직접 받는다.
var VoiceMicrophone = {
  $voice: {
    state: 0, // 0 요청 전·답 기다림, 1 허용, 2 실패(errorName)
    errorName: "",
    recording: false,
    // 대화 동안 마이크를 열어 둘지. 열어 둔 마이크는 녹음이 끝나도 닫지 않아 다음 [말하기]가 바로 소리를 받는다.
    keepOpen: false,
    // 이 페이지에서 마이크를 연 적이 있는지. 있으면 미리 열어도 권한 창이 뜨지 않는다.
    granted: false,
    opening: false,
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

    // 마이크를 열어 소리 처리를 연결한다. 소리 조각은 녹음 중일 때만 모은다.
    open: function () {
      if (voice.stream || voice.opening) return;
      voice.opening = true;
      navigator.mediaDevices
        .getUserMedia(voice.constraints)
        .then(function (stream) {
          voice.opening = false;
          voice.granted = true;
          // 마이크가 열리기 전에 녹음을 끝냈고 열어 둘 필요도 없으면 바로 닫는다.
          if (!voice.recording && !voice.keepOpen) {
            stream.getTracks().forEach(function (track) { track.stop(); });
            return;
          }
          voice.state = 1;
          voice.stream = stream;
          // 마이크를 뽑는 등으로 장치가 끊기면 닫고, 녹음 중이었으면 실패로 알린다. 다음 녹음은 마이크를 다시 연다.
          stream.getTracks().forEach(function (track) {
            track.onended = function () {
              var wasRecording = voice.recording;
              voice.closeStream();
              if (wasRecording) voice.fail("NotReadableError");
            };
          });
          voice.context = voice.context || new (window.AudioContext || window.webkitAudioContext)();
          voice.context.resume();
          voice.source = voice.context.createMediaStreamSource(stream);
          // ScriptProcessor는 오래된 방식이지만 Safari를 포함한 모든 브라우저가 지원하고 별도 스크립트 파일이 필요 없다.
          voice.processor = voice.context.createScriptProcessor(4096, 1, 1);
          voice.processor.onaudioprocess = function (event) {
            if (!voice.recording) return;
            var chunk = new Float32Array(event.inputBuffer.getChannelData(0));
            voice.chunks.push(chunk);
            var sum = 0;
            for (var i = 0; i < chunk.length; i++) sum += chunk[i] * chunk[i];
            voice.level = Math.sqrt(sum / chunk.length);
          };
          voice.source.connect(voice.processor);
          voice.processor.connect(voice.context.destination);
        })
        .catch(function (error) {
          voice.opening = false;
          // 미리 열다 실패한 것은 녹음 중이 아니면 알리지 않는다. [말하기]를 누르면 다시 열며 그때 알린다.
          if (voice.recording) voice.fail(error.name);
        });
    },

    closeStream: function () {
      if (voice.processor) {
        voice.processor.onaudioprocess = null;
        voice.processor.disconnect();
      }
      if (voice.source) voice.source.disconnect();
      if (voice.stream) voice.stream.getTracks().forEach(function (track) { track.onended = null; track.stop(); });
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
    // 권한만 받고 바로 닫는다. 마이크는 녹음하거나 대화하는 동안만 열어 탭의 마이크 표시가 계속 켜져 있지 않게 한다.
    navigator.mediaDevices
      .getUserMedia(voice.constraints)
      .then(function (stream) {
        stream.getTracks().forEach(function (track) { track.stop(); });
        voice.granted = true;
        voice.state = 1;
      })
      .catch(function (error) { voice.fail(error.name); });
  },

  // 대화를 시작할 때 마이크를 미리 연다. 권한 창을 갑자기 띄우지 않도록 이미 허용된 경우에만 연다.
  A305Voice_KeepOpen__deps: ["$voice"],
  A305Voice_KeepOpen: function () {
    voice.keepOpen = true;
    if (!voice.isSupported() || voice.stream || voice.opening) return;
    if (voice.granted) {
      voice.open();
      return;
    }
    // 권한 조회를 지원하지 않는 브라우저는 미리 열지 않고, 처음 [말하기]를 누를 때 연다.
    if (!navigator.permissions || !navigator.permissions.query) return;
    navigator.permissions
      .query({ name: "microphone" })
      .then(function (status) {
        if (status.state === "granted" && voice.keepOpen) voice.open();
      })
      .catch(function () {});
  },

  // 대화가 끝나면 마이크를 닫는다. 녹음 중이면 녹음이 끝날 때 닫는다.
  A305Voice_Release__deps: ["$voice"],
  A305Voice_Release: function () {
    voice.keepOpen = false;
    if (!voice.recording) voice.closeStream();
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
    // 미리 열어 둔 마이크면 바로 소리를 받는다. 누른 동작으로 멈춰 있던 소리 처리를 다시 깨운다.
    if (voice.stream) {
      voice.state = 1;
      voice.context.resume();
      return;
    }
    // 마이크를 새로 열므로 권한 답도 새로 받는다. 전에 거부했어도 설정을 바꿨으면 이번에는 열릴 수 있다.
    voice.state = 0;
    voice.open();
  },

  // 녹음 중이고 마이크가 열려 소리를 받고 있는지. 열리는 동안 한 말은 녹음되지 않는다.
  A305Voice_IsCapturing__deps: ["$voice"],
  A305Voice_IsCapturing: function () {
    return voice.recording && voice.stream ? 1 : 0;
  },

  A305Voice_GetLevel__deps: ["$voice"],
  A305Voice_GetLevel: function () {
    return voice.level;
  },

  A305Voice_StopRecording__deps: ["$voice"],
  A305Voice_StopRecording: function () {
    voice.recording = false;
    if (!voice.keepOpen) voice.closeStream();
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
