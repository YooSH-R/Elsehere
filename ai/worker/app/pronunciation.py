"""Kokoro 발음 사전. espeak 음소화가 틀리게 읽는 단어만 미국식 음소로 적어 둔다.

사전 단어는 적어 둔 음소로, 나머지 글은 지금처럼 espeak로 음소화해 Kokoro에 음소로 넘긴다. 사전 단어가 없는 글은
글 전체를 한 번에 음소화하므로 Kokoro에 글을 넘길 때와 같은 음소가 된다.
항목은 실제 espeak 출력을 보고 틀린 것만 넣었다 (S15P31A305-349): 예) croissant → 프랑스식 kwˈɑːsɑ̃,
Busan → bjˈuːsən, barista → bˈɑːɹɹɪstə. 음소는 espeak 출력과 같은 기호·강세 표기이고 Kokoro 어휘 안의 기호만 쓴다.
학습자 닉네임 같은 임의의 한국 이름은 사전으로 막을 수 없어 넣지 않는다.
"""

import re
from collections.abc import Callable

LEXICON = {
    # 카페
    "croissant": "kɹəsˈɑːnt",
    "croissants": "kɹəsˈɑːnts",
    "barista": "bɑːɹˈiːstə",
    "baristas": "bɑːɹˈiːstəz",
    "café au lait": "kæfˌeɪ oʊ lˈeɪ",
    "cafe au lait": "kæfˌeɪ oʊ lˈeɪ",
    # 한국 음식·명절 (학습자가 꺼내면 NPC가 따라 말함)
    "kimchi": "kˈɪmtʃi",
    "bibimbap": "bˈiːbɪmbˌɑːp",
    "tteokbokki": "tˈʌkbˌoʊki",
    "tteok": "tˈʌk",
    "kimbap": "kˈɪmbɑːp",
    "gimbap": "ɡˈɪmbɑːp",
    "japchae": "dʒˈɑːptʃeɪ",
    "galbi": "ɡˈɑːlbi",
    "samgyeopsal": "sˈɑːmɡjʌpsˌɑːl",
    "ramyeon": "ɹˈɑːmjʌn",
    "soju": "sˈoʊdʒuː",
    "makgeolli": "mˈɑːkɡʌli",
    "hanbok": "hˈɑːnboʊk",
    "chuseok": "tʃˈuːsʌk",
    # 한국 지명
    "busan": "bˈuːsɑːn",
    "daegu": "dˈeɪɡuː",
    "daejeon": "dˈeɪdʒʌn",
    "ulsan": "ˈuːlsɑːn",
    "jeju": "dʒˈeɪdʒuː",
    "itaewon": "ˈiːteɪwʌn",
    "hongdae": "hˈɑːŋdeɪ",
    # 구어 철자
    "aight": "ˈaɪt",
    "tryna": "tɹˈaɪnə",
    "hon": "hˈʌn",
    "comin": "kˈʌmɪn",
}
# 긴 항목부터 (café au lait가 café보다 먼저). 앞뒤가 글자가 아닐 때만 (honest의 hon은 아님)
LEXICON_WORD = re.compile(
    r"(?<![\w'’])(" + "|".join(re.escape(w) for w in sorted(LEXICON, key=len, reverse=True)) + r")(?!\w)",
    re.IGNORECASE,
)
MARK_AFTER_SPACE = re.compile(r"\s+([,.!?;:])")


def to_phonemes(text: str, phonemize: Callable[[str], str]) -> str:
    """Kokoro에 넘길 음소. phonemize는 espeak 음소화(kokoro_onnx Tokenizer.phonemize)."""
    parts = []
    last = 0
    for match in LEXICON_WORD.finditer(text):
        if text[last : match.start()].strip():
            parts.append(phonemize(text[last : match.start()]))
        parts.append(LEXICON[match.group(0).lower()])
        last = match.end()
    if text[last:].strip():
        parts.append(phonemize(text[last:]))
    return MARK_AFTER_SPACE.sub(r"\1", " ".join(p for p in parts if p))
