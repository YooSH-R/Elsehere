"""로컬 무례 판정기 (KcELECTRA, CPU) — 무례 판정 단계적 구조의 첫 단계 (docs/ai-plan/README.md 트랙 A).

기능 플래그: GUARD_LOCAL_MODEL이 비어 있으면(기본) 쓰지 않고, 판정은 지금처럼 전부 LLM이 한다.
켜면 한글이 든 입력만 점수 s를 낸다 (한국어 모델). s ≥ t_hi면 무례, s ≤ t_lo면 아님으로 판정용 LLM을 부르지 않고,
그 사이·영어·로컬 실패는 LLM이 판정한다.

점수는 prototype/guard_clf/cascade_eval.py와 같은 방식(softmax의 무례 칸, 같은 토큰화·한글 판별)으로 낸다.
그래야 거기서 고른 t_lo·t_hi를 그대로 쓸 수 있다. 모델 폴더는 train_clf.py가 남긴 best/ (threshold.json의
context가 true면 바로 앞 NPC 대사를 문장 쌍으로 함께 넣는다).
"""
import json
import re
import threading
from pathlib import Path

HANGUL = re.compile(r"[가-힣ㄱ-ㅎㅏ-ㅣ]")   # cascade_eval.py의 라우팅과 같게 (prompts.HANGUL은 번역용이라 모음 자모가 없음)


class LocalGuard:
    """점수는 한 번에 하나 (CPU를 나눠 쓰면 둘 다 느려짐). 한 문장 약 26ms (i7-13700H, v2-mix)."""

    def __init__(self, path: str):
        import torch   # 선택 의존성 (requirements-guard.txt): 없으면 load()가 None을 돌려줌
        from transformers import AutoTokenizer, ElectraForSequenceClassification
        d = Path(path)
        meta = json.loads((d / "threshold.json").read_text(encoding="utf-8")) if (d / "threshold.json").exists() else {}
        self.torch = torch
        self.tok = AutoTokenizer.from_pretrained(d)
        self.model = ElectraForSequenceClassification.from_pretrained(d).eval()
        self.context = bool(meta.get("context"))
        self.name = d.parent.name if d.name == "best" else d.name   # runs/v2-mix/best → v2-mix
        self.lock = threading.Lock()

    @classmethod
    def load(cls, path: str) -> "LocalGuard | None":
        if not path or not Path(path).is_dir():
            return None
        try:
            g = cls(path)
        except ImportError:
            return None
        g.score("준비", "")   # 첫 호출 준비 시간을 미리 써 둔다
        return g

    def score(self, text: str, prev: str) -> float:
        """무례 점수 0~1. 확률이 아니다 (보정 전 softmax)."""
        with self.lock, self.torch.no_grad():
            # 목록으로 넘긴다: tok(text, "")는 빈 대사를 '쌍 없음'으로 봐 [SEP]이 하나뿐인데, 학습·평가는 배치라
            # 빈 대사도 [CLS] 문장 [SEP] [SEP]으로 봤다 (그대로 두면 v2-mix-ctx 점수가 최대 0.17 어긋남)
            enc = (self.tok([text], [prev or ""], truncation="longest_first", max_length=128, return_tensors="pt") if self.context
                   else self.tok(text, truncation=True, max_length=128, return_tensors="pt"))
            return float(self.torch.softmax(self.model(**enc).logits.float(), -1)[0, 1])
