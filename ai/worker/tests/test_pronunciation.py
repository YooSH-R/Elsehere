"""Kokoro 발음 사전 (app.pronunciation). 사전 단어는 적어 둔 음소로, 나머지는 espeak 음소화 그대로 Kokoro에 음소로 넘긴다.
espeak·모델 없이 돌도록 음소화는 대문자로 바꾸는 가짜로 대신한다."""

from app.pronunciation import LEXICON, to_phonemes

# kokoro-v1.0 모델의 음소 어휘 (kokoro_onnx Tokenizer.vocab). 이 밖의 기호는 Kokoro가 버린다
KOKORO_SYMBOLS = set(' !"(),.:;?AIOQSTWYabcdefhijklmnopqrstuvwxyzæçðøŋœɐɑɒɔɕɖəɚɛɜɟɡɣɤɥɨɪɯɰɲɳɴɸɹɻɽɾʁʂʃʈʊʋʌʎʒʔʝʣʤʥʦʧʨʰʲˈˌː̃βθχᵊᵝᵻ—“”…→↓↗↘ꭧ')


class FakePhonemizer:
    def __init__(self):
        self.calls = []

    def __call__(self, text: str) -> str:
        self.calls.append(text)
        return text.strip().upper()


def test_lexicon_word_uses_its_phonemes_and_the_rest_is_phonemized():
    phonemize = FakePhonemizer()

    assert to_phonemes("I love kimchi!", phonemize) == "I LOVE kˈɪmtʃi!"
    assert phonemize.calls == ["I love ", "!"]


def test_lexicon_matches_whole_words_in_any_case():
    assert to_phonemes("Croissants, please.", FakePhonemizer()) == "kɹəsˈɑːnts, PLEASE."
    assert to_phonemes("Be honest, hon.", FakePhonemizer()) == "BE HONEST, hˈʌn."


def test_lexicon_entry_can_span_several_words():
    assert to_phonemes("I'll have a café au lait.", FakePhonemizer()) == "I'LL HAVE A kæfˌeɪ oʊ lˈeɪ."


def test_mm_hm_is_hummed_instead_of_spelled_out():
    # espeak는 Mm-hm·Mhm·Mm-hmm을 글자로 읽는다 (ˌɛmˈɛmˌeɪtʃˈɛm, 엠엠에이치엠)
    assert to_phonemes("Mm-hm. Anything else?", FakePhonemizer()) == "ɨmhˈɨm. ANYTHING ELSE?"
    assert to_phonemes("Mhm, sure.", FakePhonemizer()) == "ɨmhˈɨm, SURE."
    assert to_phonemes("Mm-hmm.", FakePhonemizer()) == "ɨmhˈɨm."


def test_text_without_lexicon_words_is_phonemized_as_a_whole():
    phonemize = FakePhonemizer()

    assert to_phonemes("Hi! What can I get for you?", phonemize) == "HI! WHAT CAN I GET FOR YOU?"
    assert phonemize.calls == ["Hi! What can I get for you?"]


def test_lexicon_keys_are_lowercase_and_phonemes_use_kokoro_symbols():
    for word, phonemes in LEXICON.items():
        assert word == word.lower(), word
        assert phonemes and set(phonemes) <= KOKORO_SYMBOLS, word
