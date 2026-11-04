"""GPU 서버용 STT 시제품. 워커가 Groq를 부르는 OpenAI 호환 형식(POST /v1/audio/transcriptions, verbose_json)을 그대로 받는다.

- 127.0.0.1에서만 받는다. 바깥(배포 서버)에서는 GPU 서버가 먼저 거는 역방향 SSH 터널로만 들어온다.
- Authorization: Bearer <STT_TOKEN>이 맞아야 응답한다.
- 받아 적은 문장은 로그에 남기지 않는다. 시간·길이만 남긴다.
- 동시에 들어온 요청은 한 묶음으로 받아 적는다 (transcribe_batch).
실행: STT_TOKEN=... ~/stt/gpu5.sh ~/stt/gpu_stt/server.py   (gpu5.sh가 5번 GPU만 보이게 한다)
"""

import hmac
import io
import json
import os
import queue
import threading
import time
from dataclasses import dataclass, field
from email.parser import BytesParser
from email.policy import HTTP
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

import av
import numpy as np
from faster_whisper import WhisperModel
from faster_whisper.audio import pad_or_trim
from faster_whisper.tokenizer import Tokenizer
from faster_whisper.transcribe import get_suppressed_tokens

MODEL_DIR = os.environ.get("STT_MODEL_DIR", str(Path.home() / "stt/stt_finetune/checkpoints/turbo-lr1e4/ct2"))
LANGUAGE = os.environ.get("STT_LANGUAGE", "ko") or None  # 게임 녹음 평가에서 가장 좋았던 설정: ko 고정 + 혼합 프롬프트
PORT = int(os.environ.get("STT_PORT", "18431"))
TOKEN = os.environ["STT_TOKEN"]
MAX_BYTES = 3 * 1024 * 1024 + 64 * 1024  # 워커 녹음 상한 3MB + multipart 머리
BATCH_MAX = 16  # 인코더가 건당 약 0.035초로 GPU 계산 한계라 4건부터 처리량이 초당 약 28건으로 같다. 16이면 40명이 몰려도 묶음 세 번

# CTranslate2 기본 할당기(cudaMallocAsync)는 큰 블록을 요청마다 드라이버에서 다시 받는다. 공용 서버에서 다른 팀이
# 드라이버를 바쁘게 쓰면 그 호출이 드라이버 잠금에서 몇 초씩 멈췄다(1건 중앙 0.08초인데 p90 1.2초, 16건 중앙 4.8초).
# 한 번 받은 블록을 계속 쓰는 cub 캐시 할당기로 바꾸면 1건 p90 0.05초, 16건 0.56초로 안정된다.
# 형식: 블록 크기 배수, 최소·최대 블록(배수의 거듭제곱), 캐시 상한 바이트. 최대 블록 4^16(4GB)이 16건 인코더의 가장 큰 텐서보다 크다.
os.environ.setdefault("CT2_CUDA_ALLOCATOR", "cub_caching")
os.environ.setdefault("CT2_CUDA_CACHING_ALLOCATOR_CONFIG", "4,3,16,8589934592")

model = WhisperModel(MODEL_DIR, device="cuda", compute_type="float16")
# 아래 값은 model.transcribe(beam_size=5, temperature=0)가 쓰는 기본값과 같다
PROMPT_TOKENIZER = Tokenizer(model.hf_tokenizer, True, task="transcribe", language="en")  # 언어와 상관없이 토큰은 같다
SUPPRESS_TOKENS = get_suppressed_tokens(PROMPT_TOKENIZER, [-1])
NO_SPEECH_THRESHOLD, LOG_PROB_THRESHOLD = 0.6, -1.0


def log(event: dict) -> None:
    print(json.dumps({"t": time.strftime("%H:%M:%S"), **event}, ensure_ascii=False), flush=True)


def load_audio(data: bytes) -> np.ndarray:
    """webm·wav·ogg 등 → 16kHz 모노 float32. av 19에서 faster_whisper의 decode_audio가 깨져서 직접 해독한다."""
    resampler = av.AudioResampler(format="s16", layout="mono", rate=16000)
    chunks = []
    with av.open(io.BytesIO(data)) as container:
        for frame in container.decode(audio=0):
            chunks += [f.to_ndarray().reshape(-1) for f in resampler.resample(frame)]
    chunks += [f.to_ndarray().reshape(-1) for f in resampler.resample(None)]
    if not chunks:
        raise ValueError("소리가 없는 파일")
    return np.concatenate(chunks).astype(np.float32) / 32768.0


def parse_form(content_type: str, body: bytes) -> dict:
    """multipart/form-data → {이름: bytes}."""
    message = BytesParser(policy=HTTP).parsebytes(f"Content-Type: {content_type}\r\n\r\n".encode() + body)
    if not message.is_multipart():
        raise ValueError("multipart/form-data가 아님")
    return {part.get_param("name", header="content-disposition"): part.get_payload(decode=True) or b"" for part in message.iter_parts()}


def verbose_json(language: str, audio: np.ndarray, segments: list[dict]) -> dict:
    return {
        "task": "transcribe",
        "language": language,
        "duration": round(len(audio) / 16000, 3),
        "text": " ".join(s["text"].strip() for s in segments).strip(),
        "segments": [
            {"start": round(s["start"], 2), "end": round(s["end"], 2), "text": s["text"], "no_speech_prob": round(s["no_speech_prob"], 3)}
            for s in segments
        ],
    }


def transcribe_one(audio: np.ndarray, prompt: str) -> dict:
    """묶기 전 방식 그대로 한 건을 받아 적는다. 30초 창 하나로 끝나지 않는 녹음만 여기로 온다."""
    segments, info = model.transcribe(
        audio, language=LANGUAGE, beam_size=5, temperature=0, condition_on_previous_text=False, initial_prompt=prompt or None
    )
    return verbose_json(info.language, audio, [s._asdict() for s in segments])


def transcribe_batch(items: list[tuple[np.ndarray, str]]) -> list[dict]:
    """여러 녹음을 한 번에 받아 적는다. CTranslate2는 프롬프트 토큰 수가 같은 것끼리만 함께 생성할 수 있어
    (장면마다 단어 프롬프트가 다르다) 프롬프트 길이별로 나눠 transcribe_window를 부른다."""
    groups: dict[int, list[int]] = {}
    for i, (_, prompt) in enumerate(items):
        groups.setdefault(len(PROMPT_TOKENIZER.encode(" " + prompt.strip())) if prompt else 0, []).append(i)
    replies: list[dict] = [{}] * len(items)
    for indexes in groups.values():
        for i, reply in zip(indexes, transcribe_window([items[i] for i in indexes])):
            replies[i] = reply
    return replies


def transcribe_window(items: list[tuple[np.ndarray, str]]) -> list[dict]:
    """프롬프트 길이가 같은 녹음 여러 개를 한 번에 받아 적는다.

    model.transcribe가 녹음 하나에 하는 30초 창 계산(인코더 → beam 5 생성)을 여러 녹음에 한꺼번에 한다.
    설정·프롬프트·말소리 없음 판정은 model.transcribe와 같고, 타임스탬프만 끈다(without_timestamps).
    CTranslate2의 타임스탬프 규칙은 묶음에서 크게 느려져(16건 생성 3.05초 → 끄면 0.035초) 묶는 이득이 사라진다.
    워커는 text·duration만 쓰므로 segments는 녹음 전체를 한 구간으로 돌려준다.
    30초가 넘는 녹음만 transcribe_one으로 따로 받아 적는다.
    """
    features = [model.feature_extractor(audio) for audio, _ in items]
    content_frames = [f.shape[-1] - 1 for f in features]
    window_frames = [min(model.feature_extractor.nb_max_frames, frames) for frames in content_frames]
    encoder_output = model.encode(np.stack([pad_or_trim(f[:, :size]) for f, size in zip(features, window_frames)]))
    if LANGUAGE:
        languages = [LANGUAGE] * len(items)
    else:
        languages = [found[0][0][2:-2] for found in model.model.detect_language(encoder_output)]
    tokenizers = [Tokenizer(model.hf_tokenizer, True, task="transcribe", language=language) for language in languages]
    prompts = [
        model.get_prompt(tokenizer, tokenizer.encode(" " + prompt.strip()) if prompt else [], without_timestamps=True)
        for tokenizer, (_, prompt) in zip(tokenizers, items)
    ]
    results = model.model.generate(
        encoder_output,
        prompts,
        beam_size=5,
        max_length=model.max_length,
        return_scores=True,
        return_no_speech_prob=True,
        suppress_blank=True,
        suppress_tokens=SUPPRESS_TOKENS,
    )

    replies = []
    for (audio, prompt), tokenizer, language, frames, size, result in zip(
        items, tokenizers, languages, content_frames, window_frames, results
    ):
        if frames > model.feature_extractor.nb_max_frames:
            replies.append(transcribe_one(audio, prompt))
            continue
        tokens = result.sequences_ids[0]
        avg_logprob = result.scores[0] * len(tokens) / (len(tokens) + 1)
        text = tokenizer.decode(tokens)
        no_speech = result.no_speech_prob > NO_SPEECH_THRESHOLD and avg_logprob <= LOG_PROB_THRESHOLD
        segment = {
            "start": 0.0,
            "end": size * model.feature_extractor.time_per_frame,
            "text": text,
            "no_speech_prob": result.no_speech_prob,
        }
        replies.append(verbose_json(language, audio, [] if no_speech or not text.strip() else [segment]))
    return replies


@dataclass
class Job:
    audio: np.ndarray
    prompt: str
    done: threading.Event = field(default_factory=threading.Event)
    result: dict | None = None
    error: Exception | None = None


jobs: queue.Queue[Job] = queue.Queue()


def batch_loop() -> None:
    """GPU 쪽 일꾼 하나. 앞 묶음을 받아 적는 동안 쌓인 요청을 다음 묶음으로 한꺼번에 가져간다."""
    while True:
        batch = [jobs.get()]
        while len(batch) < BATCH_MAX and not jobs.empty():
            batch.append(jobs.get())
        started = time.perf_counter()
        try:
            for job, result in zip(batch, transcribe_batch([(job.audio, job.prompt) for job in batch])):
                job.result = result
        except Exception as e:  # 묶음이 통째로 실패하면 묶인 요청 모두 500으로 돌려준다
            for job in batch:
                job.error = e
        log({"event": "batch", "size": len(batch), "infer_s": round(time.perf_counter() - started, 3), "waiting": jobs.qsize()})
        for job in batch:
            job.done.set()


def transcribe(audio: np.ndarray, prompt: str) -> dict:
    job = Job(audio, prompt)
    jobs.put(job)
    job.done.wait()
    if job.error:
        raise job.error
    return job.result


class Handler(BaseHTTPRequestHandler):
    def reply(self, status: int, payload: dict) -> None:
        body = json.dumps(payload, ensure_ascii=False).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def authorized(self) -> bool:
        given = self.headers.get("Authorization", "").removeprefix("Bearer ").strip()
        return hmac.compare_digest(given.encode(), TOKEN.encode())

    def do_GET(self):
        if self.path != "/health":
            return self.reply(404, {"error": {"message": "없는 주소"}})
        self.reply(200, {"ok": True, "model": Path(MODEL_DIR).parent.name, "language": LANGUAGE})

    def do_POST(self):
        if self.path.rstrip("/") not in ("/v1/audio/transcriptions", "/audio/transcriptions"):
            return self.reply(404, {"error": {"message": "없는 주소"}})
        if not self.authorized():
            return self.reply(401, {"error": {"message": "토큰이 맞지 않음"}})
        size = int(self.headers.get("Content-Length") or 0)
        if size > MAX_BYTES:
            return self.reply(413, {"error": {"message": "녹음이 너무 큼"}})
        started = time.perf_counter()
        try:
            form = parse_form(self.headers.get("Content-Type", ""), self.rfile.read(size))
            audio = load_audio(form["file"])
        except (KeyError, ValueError, av.error.FFmpegError) as e:
            log({"event": "rejected", "why": type(e).__name__, "bytes": size})
            return self.reply(400, {"error": {"message": f"녹음을 읽을 수 없음: {type(e).__name__}"}})
        decoded = time.perf_counter()
        try:
            result = transcribe(audio, form.get("prompt", b"").decode("utf-8", "replace"))
        except Exception as e:
            log({"event": "failed", "why": type(e).__name__})
            return self.reply(500, {"error": {"message": f"받아 적기 실패: {type(e).__name__}"}})
        done = time.perf_counter()
        log(
            {
                "event": "stt",
                "audio_s": result["duration"],
                "decode_s": round(decoded - started, 3),
                "wait_infer_s": round(done - decoded, 3),
                "words": len(result["text"].split()),
            }
        )
        self.reply(200, result)

    def log_message(self, *args):  # 기본 접근 로그 끔 (주소·경로만 남는 로그는 위 log로 충분)
        pass


class Server(ThreadingHTTPServer):
    # 기본 접속 대기열(5)로는 여러 사람이 동시에 말할 때 넘친 연결이 끊겼다(동시 40건 중 2건 ConnectionReset, S15P31A305-511)
    request_queue_size = 64


def warm_up() -> None:
    """묶음 크기마다 처음 한 번은 GPU 메모리를 새로 받고 커널을 골라 수 초가 걸린다. 받기 전에 크기별로 한 번씩 돌린다."""
    started = time.perf_counter()
    silence = np.zeros(16000, dtype=np.float32)
    for size in range(1, BATCH_MAX + 1):
        transcribe_batch([(silence, "")] * size)
    log({"event": "warm", "s": round(time.perf_counter() - started, 1)})


if __name__ == "__main__":
    log({"event": "start", "model": MODEL_DIR, "language": LANGUAGE, "port": PORT, "batch_max": BATCH_MAX})
    warm_up()
    threading.Thread(target=batch_loop, daemon=True).start()
    Server(("127.0.0.1", PORT), Handler).serve_forever()
