mergeInto(LibraryManager.library, {
  // 같은 탭에서 주소를 옮긴다. Application.OpenURL은 Web 빌드에서 새 탭을 연다.
  A305_RedirectTo: function (urlPointer) {
    window.location.href = UTF8ToString(urlPointer);
  },
});
