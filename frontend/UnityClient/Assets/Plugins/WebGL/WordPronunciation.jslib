mergeInto(LibraryManager.library, {
  A305Word_StopPronunciation: function () {
    if (typeof window !== "undefined" && window.speechSynthesis)
      window.speechSynthesis.cancel();
  },
  A305Word_CanPronounce: function () {
    return typeof window !== "undefined" && window.speechSynthesis &&
      typeof SpeechSynthesisUtterance !== "undefined" ? 1 : 0;
  },
  A305Word_Pronounce: function (textPointer) {
    if (typeof window === "undefined" || !window.speechSynthesis ||
        typeof SpeechSynthesisUtterance === "undefined") return 0;
    var text = UTF8ToString(textPointer).trim();
    if (!text) return 0;
    try {
      var speech = window.speechSynthesis;
      var utterance = new SpeechSynthesisUtterance(text);
      var voices = speech.getVoices();
      utterance.lang = "en-US";
      utterance.rate = 0.85;
      utterance.voice = voices.find(function (voice) { return voice.lang === "en-US"; }) ||
        voices.find(function (voice) { return /^en[-_]/i.test(voice.lang); }) || null;
      speech.cancel();
      speech.speak(utterance);
      return 1;
    } catch (error) {
      return 0;
    }
  },
});
