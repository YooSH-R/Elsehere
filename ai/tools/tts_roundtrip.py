"""TTS 왕복 평가: 고정 문장을 워커와 같은 방법(조각 나누기 + Kokoro)으로 읽고, 로컬 Whisper로 다시 받아써 틀리게 읽은 단어를 찾는다.

Kokoro와 Whisper는 가상환경이 달라 단계를 나눈다. 외부 API는 부르지 않는다.

  ai/worker/.venv/Scripts/python ai/tools/tts_roundtrip.py synth --model <kokoro onnx> --voices <voices bin>    CPU
  <faster-whisper 환경>/python ai/tools/tts_roundtrip.py asr      GPU (게임 테스트 중에는 돌리지 않는다)
  <faster-whisper 환경>/python ai/tools/tts_roundtrip.py report   태그별 WER·틀린 곳 → report.md

synth   문장을 글자 단위로 app.npc.Splitter에 넣어(NPC 대답 스트림과 같은 조각) app.tts.Kokoro로 합성하고 이어 붙인다.
        목소리는 문장 NPC의 설정(ai/npcs/<npcId>.json voiceId), 속도는 --speed (기본 1.0, 난이도 1·3은 0.85·1.06).
asr     24kHz 음성을 16kHz로 바꿔 faster-whisper(영어 고정, 프롬프트 없음)로 받아쓴다.
report  Whisper 영어 정규화(transformers가 있으면)로 숫자·축약 표기를 맞춘 뒤 비교한다. 곡선 따옴표와 붙여 쓰기·띄어 쓰기
        차이(haircut ↔ hair cut)는 오류로 세지 않는다. Whisper는 어색한 발음도 맞게 받아쓰기 쉬워서, 틀린 곳이 없다는 것은
        "알아들을 수 있다"는 뜻이지 "자연스럽다"는 뜻이 아니다. 틀린 곳이 나온 문장은 wav를 직접 들어 확인한다.
결과: ai/tools/results/tts-roundtrip-<시각>/ (저장소에 올리지 않음)
"""

import argparse
import hashlib
import json
import re
import sys
import unicodedata
import wave
from datetime import datetime
from pathlib import Path

TOOLS = Path(__file__).resolve().parent
AI = TOOLS.parent
DEFAULT_SET = TOOLS / "tts_roundtrip_set.jsonl"
RESULTS = TOOLS / "results"
# 평가 문장 태그. Kokoro가 틀리기 쉬운 경우를 따로 집계한다
TAGS = {
    "plain",  # 특별한 것 없는 문장
    "short",  # 세 단어 이하
    "long",  # 쉼표·접속사로 길게 이어진 문장
    "word",  # 단어 하나 (사물 단어 발음 듣기)
    "price",  # $ 금액
    "number",  # 금액·시각이 아닌 숫자 (방 번호, 층, 온도)
    "time",  # 시각 (3 p.m., 6:30)
    "abbreviation",  # ID, Mr., Dr.
    "markup",  # *제목*, **강조**
    "hangul",  # 한글이 섞임
    "dash",  # —, ‑ 같은 특수 줄표
    "curly",  # ’ “ ” 곡선 따옴표 (LLM 출력에 흔함)
    "informal",  # comin', gonna, y'all 같은 구어 철자
    "slang",  # bet, lowkey, aight
    "foreign",  # 영어가 아닌 단어 (mijo, bibimbap)
    "name",  # 사람 이름 (한국 이름 포함)
    "menu",  # 카페 메뉴 이름
}
CURLY_QUOTES = str.maketrans({"’": "'", "‘": "'", "“": '"', "”": '"'})


def load_set(path: Path) -> list[dict]:
    return read_jsonl(path)


def read_jsonl(path: Path) -> list[dict]:
    return [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]


# ---------------------------------------------------------------- 채점


def words(text: str, normalize=None) -> list[str]:
    """비교할 단어 목록. 곡선 따옴표를 곧은 따옴표로 먼저 바꾼다 (Whisper 정규화는 곧은 아포스트로피의 축약만 푼다)."""
    text = text.translate(CURLY_QUOTES)
    if normalize:
        text = unicodedata.normalize("NFC", normalize(text))  # Whisper 정규화가 NFKD로 한글을 자모로 쪼갬 → 다시 합침
    text = re.sub(r"[^\w\s']", " ", text.lower())
    return [w.strip("'") for w in text.split() if w.strip("'")]


def join_split_words(target: list[str], other: list[str]) -> list[str]:
    """target에서 이웃한 두 단어를 붙인 말이 other에만 있으면 붙인다 (hair cut → haircut, low key → lowkey)."""
    other_words, own_words = set(other), set(target)
    joined = []
    i = 0
    while i < len(target):
        pair = target[i] + target[i + 1] if i + 1 < len(target) else None
        if pair in other_words and pair not in own_words:
            joined.append(pair)
            i += 2
        else:
            joined.append(target[i])
            i += 1
    return joined


def align(ref: list[str], hyp: list[str]) -> list[tuple[str | None, str | None]]:
    """편집 거리가 가장 작은 짝짓기. (원문 단어, 받아쓴 단어)이고, 빠지거나 더해진 쪽은 None."""
    cost = [[i + j if i == 0 or j == 0 else 0 for j in range(len(hyp) + 1)] for i in range(len(ref) + 1)]
    for i in range(1, len(ref) + 1):
        for j in range(1, len(hyp) + 1):
            cost[i][j] = min(cost[i - 1][j] + 1, cost[i][j - 1] + 1, cost[i - 1][j - 1] + (ref[i - 1] != hyp[j - 1]))
    pairs = []
    i, j = len(ref), len(hyp)
    while i or j:
        if i and j and cost[i][j] == cost[i - 1][j - 1] + (ref[i - 1] != hyp[j - 1]):
            pairs.append((ref[i - 1], hyp[j - 1]))
            i, j = i - 1, j - 1
        elif i and cost[i][j] == cost[i - 1][j] + 1:
            pairs.append((ref[i - 1], None))
            i -= 1
        else:
            pairs.append((None, hyp[j - 1]))
            j -= 1
    return pairs[::-1]


def score(ref: str, hyp: str, normalize=None) -> dict:
    """WER과 틀린 곳. 이어서 틀린 단어는 한 덩어리로 묶는다 ({"ref": "comin right", "hyp": "common write"})."""
    ref_words, hyp_words = words(ref, normalize), words(hyp, normalize)
    hyp_words = join_split_words(hyp_words, ref_words)
    ref_words = join_split_words(ref_words, hyp_words)
    pairs = align(ref_words, hyp_words)

    errors, span = [], None
    for ref_word, hyp_word in pairs:
        if ref_word == hyp_word:
            span = None
            continue
        if span is None:
            span = {"ref": [], "hyp": []}
            errors.append(span)
        if ref_word:
            span["ref"].append(ref_word)
        if hyp_word:
            span["hyp"].append(hyp_word)

    edits = sum(ref_word != hyp_word for ref_word, hyp_word in pairs)
    wer = edits / len(ref_words) if ref_words else float(bool(hyp_words))
    return {
        "wer": wer,
        "ref_words": len(ref_words),
        "errors": [{"ref": " ".join(e["ref"]), "hyp": " ".join(e["hyp"])} for e in errors],
    }


def summarize(rows: list[dict]) -> list[dict]:
    """전체와 태그별: 문장 수, WER(원문 단어 수로 가중), 틀린 곳 없는 문장 수, 틀린 곳 있는 문장 수. 태그는 WER 높은 순."""

    def group(name: str, members: list[dict]) -> dict:
        ref_words = sum(r["ref_words"] for r in members)
        edits = sum(r["wer"] * r["ref_words"] for r in members)
        with_errors = sum(bool(r["errors"]) for r in members)
        return {
            "group": name,
            "sentences": len(members),
            "wer": round(edits / ref_words, 3) if ref_words else 0.0,
            "exact": len(members) - with_errors,
            "with_errors": with_errors,
        }

    by_tag = [group(tag, [r for r in rows if tag in r["tags"]]) for tag in sorted(TAGS)]
    by_tag = [g for g in by_tag if g["sentences"]]
    return [group("전체", rows), *sorted(by_tag, key=lambda g: -g["wer"])]


def whisper_normalizer():
    """Whisper 논문의 영어 정규화 (숫자 "seven thirty" = "7:30", 축약 풀기, 영·미 철자). transformers가 있는 환경에만 있다."""
    try:
        from transformers.models.whisper.english_normalizer import EnglishTextNormalizer
    except ImportError:
        return None
    spelling = next(Path.home().glob(".cache/huggingface/hub/models--openai--whisper-*/snapshots/*/normalizer.json"), None)
    return EnglishTextNormalizer(json.loads(spelling.read_text(encoding="utf-8")) if spelling else {})


# ---------------------------------------------------------------- 단계


def npc_voices() -> dict[str, str]:
    voices = {}
    for path in (AI / "npcs").glob("*.json"):
        config = json.loads(path.read_text(encoding="utf-8"))
        voices[config["npcId"]] = config["voiceId"]
    return voices


def latest_run() -> Path:
    runs = sorted(RESULTS.glob("tts-roundtrip-*"))
    if not runs:
        sys.exit("synth를 먼저 돌리세요.")
    return runs[-1]


def cmd_synth(args):
    sys.path.insert(0, str(AI / "worker"))
    from app.npc import Splitter
    from app.tts import Kokoro, join_wavs

    rows = load_set(args.set)
    voices = npc_voices()
    kokoro = Kokoro.load(str(args.model), str(args.voices))
    if not kokoro:
        sys.exit(f"Kokoro를 읽지 못했습니다: {args.model}, {args.voices}")

    run = RESULTS / f"tts-roundtrip-{datetime.now():%Y%m%d-%H%M%S}"
    (run / "wav").mkdir(parents=True)
    set_hash = hashlib.sha256(args.set.read_bytes()).hexdigest()[:16]
    meta = {"set": args.set.name, "set_sha256": set_hash, "model": args.model.name, "speed": args.speed, "sentences": len(rows)}
    (run / "meta.json").write_text(json.dumps(meta, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"문장 {len(rows)}개 (평가 문장 {set_hash}) → {run}", flush=True)

    with open(run / "synth.jsonl", "w", encoding="utf-8") as out:
        for n, row in enumerate(rows, 1):
            splitter = Splitter()
            chunks = [chunk for char in row["text"] for chunk in splitter.feed(char)] + splitter.flush()
            voice = voices[row["npc"]]
            record = {**row, "voice": voice, "speed": args.speed, "chunks": chunks}
            try:
                wav, seconds = join_wavs([kokoro.synth(chunk, voice, args.speed)[0] for chunk in chunks])
            except Exception as e:  # 워커에서도 합성 실패는 그 조각만 자막으로 보낸다. 실패한 문장으로 기록하고 이어 간다
                record["synth_error"] = f"{type(e).__name__}: {e}"
            else:
                (run / "wav" / f"{row['id']}.wav").write_bytes(wav)
                record["audio_s"] = round(seconds, 2)
            out.write(json.dumps(record, ensure_ascii=False) + "\n")
            if n % 20 == 0 or n == len(rows):
                print(f"  {n}/{len(rows)}", flush=True)


def read_wav(path: Path):
    import numpy as np

    with wave.open(str(path)) as w:
        if w.getframerate() != 24000:
            sys.exit(f"Kokoro 음성(24kHz)이 아닙니다: {path} {w.getframerate()}Hz")
        return np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float32) / 32768


def to_16k(audio):
    """24kHz → 16kHz (2배로 늘리고 저역 통과한 뒤 3분의 1로). faster-whisper는 16kHz 배열만 받는다."""
    import numpy as np

    up, down, half = 2, 3, 60
    taps = np.arange(-half, half + 1)
    cutoff = 0.5 / max(up, down)
    low_pass = 2 * cutoff * np.sinc(2 * cutoff * taps) * np.hamming(len(taps)) * up
    stretched = np.zeros(len(audio) * up, dtype=np.float32)
    stretched[::up] = audio
    return np.convolve(stretched, low_pass, mode="same")[::down].astype(np.float32)


def cmd_asr(args):
    from faster_whisper import WhisperModel

    run = args.run or latest_run()
    rows = [r for r in read_jsonl(run / "synth.jsonl") if "synth_error" not in r]
    model = WhisperModel(args.whisper, device=args.device, compute_type="float16" if args.device == "cuda" else "int8")
    print(f"{run.name}: 받아쓸 문장 {len(rows)}개, {args.whisper} ({args.device})", flush=True)
    with open(run / "asr.jsonl", "w", encoding="utf-8") as out:
        for n, row in enumerate(rows, 1):
            audio = to_16k(read_wav(run / "wav" / f"{row['id']}.wav"))
            segments, _ = model.transcribe(audio, language="en", beam_size=5, condition_on_previous_text=False)
            hyp = " ".join(s.text.strip() for s in segments).strip()
            out.write(json.dumps({"id": row["id"], "hyp": hyp}, ensure_ascii=False) + "\n")
            if n % 20 == 0 or n == len(rows):
                print(f"  {n}/{len(rows)}", flush=True)
    meta = json.loads((run / "meta.json").read_text(encoding="utf-8"))
    meta.update(whisper=args.whisper, device=args.device, language="en", beam_size=5)
    (run / "meta.json").write_text(json.dumps(meta, ensure_ascii=False, indent=1), encoding="utf-8")


def table(head: list[str], rows: list[list]) -> list[str]:
    lines = ["| " + " | ".join(head) + " |", "|" + "---|" * len(head)]
    return lines + ["| " + " | ".join(str(cell).replace("|", "/") for cell in row) + " |" for row in rows]


def cmd_report(args):
    run = args.run or latest_run()
    meta = json.loads((run / "meta.json").read_text(encoding="utf-8"))
    hyps = {r["id"]: r["hyp"] for r in read_jsonl(run / "asr.jsonl")}
    normalize = whisper_normalizer()
    if not normalize:
        print("transformers가 없어 Whisper 정규화 없이 비교합니다: 숫자 표기 차이(20 ↔ twenty)도 틀린 곳으로 셉니다.", file=sys.stderr)

    rows, failed = [], []
    for row in read_jsonl(run / "synth.jsonl"):
        if row["id"] in hyps:
            rows.append({**row, "hyp": hyps[row["id"]], **score(row["text"], hyps[row["id"]], normalize)})
        else:
            failed.append(row)
    with open(run / "scores.jsonl", "w", encoding="utf-8") as out:
        for row in rows:
            out.write(json.dumps(row, ensure_ascii=False) + "\n")

    md = [
        f"# TTS 왕복 평가 {run.name}",
        "",
        f"평가 문장 `{meta['set']}` ({meta['set_sha256']}), 모델 `{meta['model']}`, 속도 {meta['speed']}, "
        f"받아쓰기 `{meta.get('whisper')}`, Whisper 정규화 {'있음' if normalize else '없음'}.",
        "",
        "## 묶음별",
        "",
        *table(
            ["묶음", "문장", "WER", "틀린 곳 없음", "틀린 곳 있음"],
            [[g["group"], g["sentences"], g["wer"], g["exact"], g["with_errors"]] for g in summarize(rows)],
        ),
        "",
    ]
    if failed:
        md += [
            f"## 합성 실패 {len(failed)}개",
            "",
            *table(["id", "원문", "오류"], [[r["id"], r["text"], r["synth_error"]] for r in failed]),
            "",
        ]
    wrong = sorted((r for r in rows if r["errors"]), key=lambda r: -r["wer"])
    md += [
        f"## 틀린 곳이 있는 문장 {len(wrong)}개 (wav/<id>.wav를 들어 확인)",
        "",
        *table(
            ["id", "태그", "WER", "원문", "받아쓴 것", "틀린 곳 (원문 → 받아쓴 것)"],
            [
                [
                    r["id"],
                    ",".join(r["tags"]),
                    round(r["wer"], 2),
                    r["text"],
                    r["hyp"],
                    "; ".join(f"{e['ref'] or '∅'} → {e['hyp'] or '∅'}" for e in r["errors"]),
                ]
                for r in wrong
            ],
        ),
        "",
    ]
    text = "\n".join(md)
    (run / "report.md").write_text(text, encoding="utf-8")
    print(text)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    steps = parser.add_subparsers(dest="step", required=True)
    synth = steps.add_parser("synth")
    synth.add_argument("--set", type=Path, default=DEFAULT_SET)
    synth.add_argument("--model", type=Path, required=True, help="kokoro-v1.0.int8.onnx")
    synth.add_argument("--voices", type=Path, required=True, help="voices-v1.0.bin")
    synth.add_argument("--speed", type=float, default=1.0)
    asr = steps.add_parser("asr")
    asr.add_argument("--run", type=Path)
    asr.add_argument("--whisper", default="mobiuslabsgmbh/faster-whisper-large-v3-turbo")
    asr.add_argument("--device", default="cuda", choices=["cuda", "cpu"])
    report = steps.add_parser("report")
    report.add_argument("--run", type=Path)
    args = parser.parse_args()
    {"synth": cmd_synth, "asr": cmd_asr, "report": cmd_report}[args.step](args)


if __name__ == "__main__":
    main()
