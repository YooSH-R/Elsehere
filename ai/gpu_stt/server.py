"""GPU 서버용 STT 시제품. 워커가 Groq를 부르는 OpenAI 호환 형식(POST /v1/audio/transcriptions, verbose_json)을 그대로 받는다.

- 127.0.0.1에서만 받는다. 바깥(배포 서버)에서는 GPU 서버가 먼저 거는 역방향 SSH 터널로만 들어온다.
- Authorization: Bearer <STT_TOKEN>이 맞아야 응답한다.
- 받아 적은 문장은 로그에 남기지 않는다. 시간·길이만 남긴다.
실행: STT_TOKEN=... ~/stt/gpu5.sh ~/stt/gpu_stt/server.py   (gpu5.sh가 5번 GPU만 보이게 한다)
"""

import hmac
import io
import json
import os
import threading
import time
from email.parser import BytesParser
from email.policy import HTTP
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

import av
import numpy as np
from faster_whisper import WhisperModel

MODEL_DIR = os.environ.get("STT_MODEL_DIR", str(Path.home() / "stt/stt_finetune/checkpoints/turbo-lr1e4/ct2"))
LANGUAGE = os.environ.get("STT_LANGUAGE", "ko") or None  # 게임 녹음 평가에서 가장 좋았던 설정: ko 고정 + 혼합 프롬프트
PORT = int(os.environ.get("STT_PORT", "18431"))
TOKEN = os.environ["STT_TOKEN"]
MAX_BYTES = 3 * 1024 * 1024 + 64 * 1024  # 워커 녹음 상한 3MB + multipart 머리

model = WhisperModel(MODEL_DIR, device="cuda", compute_type="float16")
model_lock = threading.Lock()  # 시제품: 한 번에 하나씩 (H200에서 한 건 0.2초 안팎)


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


def transcribe(audio: np.ndarray, prompt: str) -> dict:
    with model_lock:
        segments, info = model.transcribe(
            audio, language=LANGUAGE, beam_size=5, temperature=0, condition_on_previous_text=False, initial_prompt=prompt or None
        )
        segments = list(segments)
    return {
        "task": "transcribe",
        "language": info.language,
        "duration": round(info.duration, 3),
        "text": " ".join(s.text.strip() for s in segments).strip(),
        "segments": [
            {"start": round(s.start, 2), "end": round(s.end, 2), "text": s.text, "no_speech_prob": round(s.no_speech_prob, 3)}
            for s in segments
        ],
    }


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
        result = transcribe(audio, form.get("prompt", b"").decode("utf-8", "replace"))
        done = time.perf_counter()
        log(
            {
                "event": "stt",
                "audio_s": result["duration"],
                "decode_s": round(decoded - started, 3),
                "infer_s": round(done - decoded, 3),
                "words": len(result["text"].split()),
            }
        )
        self.reply(200, result)

    def log_message(self, *args):  # 기본 접근 로그 끔 (주소·경로만 남는 로그는 위 log로 충분)
        pass


class Server(ThreadingHTTPServer):
    # 기본 접속 대기열(5)로는 여러 사람이 동시에 말할 때 넘친 연결이 끊겼다(동시 40건 중 2건 ConnectionReset).
    # 받아 적기는 여전히 한 번에 하나라 동시 20건이면 마지막 사람은 몇 초 기다린다 (S15P31A305-511)
    request_queue_size = 64


if __name__ == "__main__":
    log({"event": "start", "model": MODEL_DIR, "language": LANGUAGE, "port": PORT})
    Server(("127.0.0.1", PORT), Handler).serve_forever()
