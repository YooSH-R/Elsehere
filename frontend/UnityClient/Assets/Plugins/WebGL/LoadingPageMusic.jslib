mergeInto(LibraryManager.library, {
  // 로딩 페이지(index.html의 entry-music)가 틀던 접속 음악의 지금 음량. 페이지에 음악이 없으면 -1.
  A305_LoadingMusicVolume: function () {
    var music = window.elsehereEntryMusic;
    return music ? music.volume() : -1;
  },
  // Unity(InterfaceAudioPlayer)가 정한 음악 음량·재생 여부와 스피커 선택(켬·끔, 고른 크기)을 페이지 음악에 넘긴다.
  A305_SetLoadingMusic: function (volume, playing, soundEnabled, level) {
    var music = window.elsehereEntryMusic;
    if (music) {
      music.set(volume, !!playing, !!soundEnabled, level);
    }
  },
});
