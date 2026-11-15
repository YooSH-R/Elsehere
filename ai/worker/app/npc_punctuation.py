"""NPC 대사에서 세미콜론을 문장 경계로 바꾼다."""

import re

SEMICOLONS = re.compile(r"[;；]+")


class NpcSentencePunctuation:
    def __init__(self):
        self._after_semicolon = False

    def feed(self, text: str) -> str:
        normalized = []
        for index, sentence in enumerate(SEMICOLONS.split(text)):
            if index and not self._after_semicolon:
                normalized.append(".")
                self._after_semicolon = True
            if self._after_semicolon:
                # 다음 문장이 다른 대사 조각이나 표정 표시 뒤에서 시작할 수 있다.
                sentence = sentence.lstrip()
                if not sentence:
                    continue
                sentence = " " + sentence[0].upper() + sentence[1:]
                self._after_semicolon = False
            normalized.append(sentence)
        return "".join(normalized)
