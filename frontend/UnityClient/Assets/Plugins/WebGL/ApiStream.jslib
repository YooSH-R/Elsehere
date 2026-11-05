// 응답 본문을 받는 대로 읽는 HTTP 요청(서버가 보내는 이벤트 스트림). Unity의 UnityWebRequest는 Web 빌드에서 응답이 끝나야 본문을 넘겨줘
// fetch와 ReadableStream으로 직접 받는다. 받은 글자는 모아 두고 C#이 매 프레임 꺼내 간다(BrowserStreamTransport).
var ApiStream = {
  $apiStreams: {
    next: 1,
    items: {},

    // 0 받는 중, 1 다 받음, 2 응답을 받지 못함·도중에 끊김
    create: function () {
      var id = apiStreams.next++;
      apiStreams.items[id] = { state: 0, status: 0, pieces: [], errorBody: "", controller: null };
      return id;
    },

    copy: function (text) {
      var size = lengthBytesUTF8(text) + 1;
      var buffer = _malloc(size);
      stringToUTF8(text, buffer, size);
      return buffer;
    },
  },

  A305ApiStream_Start__deps: ["$apiStreams"],
  A305ApiStream_Start: function (urlPointer, methodPointer, bodyPointer, headersPointer) {
    var id = apiStreams.create();
    var item = apiStreams.items[id];
    var headers = JSON.parse(UTF8ToString(headersPointer));
    var body = bodyPointer ? UTF8ToString(bodyPointer) : null;
    if (body !== null) headers["Content-Type"] = "application/json";
    item.controller = typeof AbortController !== "undefined" ? new AbortController() : null;

    fetch(UTF8ToString(urlPointer), {
      method: UTF8ToString(methodPointer),
      headers: headers,
      body: body,
      credentials: "same-origin",
      cache: "no-store",
      signal: item.controller ? item.controller.signal : undefined,
    })
      .then(function (response) {
        item.status = response.status;
        if (!response.ok || !response.body) {
          // 오류 응답(JSON)은 다 받아 한 번에 넘긴다.
          return response.text().then(function (text) {
            item.errorBody = text;
            item.state = 1;
          });
        }
        var reader = response.body.getReader();
        var decoder = new TextDecoder("utf-8");
        function read() {
          return reader.read().then(function (result) {
            if (result.done) {
              var rest = decoder.decode();
              if (rest) item.pieces.push(rest);
              item.state = 1;
              return;
            }
            item.pieces.push(decoder.decode(result.value, { stream: true }));
            return read();
          });
        }
        return read();
      })
      .catch(function () {
        item.state = 2;
      });
    return id;
  },

  // 받은 글자를 꺼낸다. 새로 받은 것이 없으면 null이다.
  A305ApiStream_TakeText__deps: ["$apiStreams"],
  A305ApiStream_TakeText: function (id) {
    var item = apiStreams.items[id];
    if (!item || item.pieces.length === 0) return null;
    var text = item.pieces.join("");
    item.pieces = [];
    return apiStreams.copy(text);
  },

  A305ApiStream_GetState__deps: ["$apiStreams"],
  A305ApiStream_GetState: function (id) {
    var item = apiStreams.items[id];
    return item ? item.state : 2;
  },

  A305ApiStream_GetStatus__deps: ["$apiStreams"],
  A305ApiStream_GetStatus: function (id) {
    var item = apiStreams.items[id];
    return item ? item.status : 0;
  },

  A305ApiStream_GetErrorBody__deps: ["$apiStreams"],
  A305ApiStream_GetErrorBody: function (id) {
    var item = apiStreams.items[id];
    return apiStreams.copy(item ? item.errorBody : "");
  },

  // 시간 초과 등으로 그만 받을 때와 다 받은 뒤 정리할 때 부른다.
  A305ApiStream_Release__deps: ["$apiStreams"],
  A305ApiStream_Release: function (id) {
    var item = apiStreams.items[id];
    if (!item) return;
    if (item.state === 0 && item.controller) item.controller.abort();
    delete apiStreams.items[id];
  },
};

mergeInto(LibraryManager.library, ApiStream);
