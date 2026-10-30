"""SSAFY GPU 서버용 Kokoro 합성 서버 (S15P31A305-371). AI 워커가 다듬고 음소로 바꾼 글을 받아 WAV를 돌려준다.

- 읽을 글 다듬기·발음 사전은 워커(app.speech_text, app.pronunciation)에만 있다. 이 서버는 음소 → 소리만 한다.
- 127.0.0.1에서만 받는다. 배포 서버에서는 GPU 서버가 먼저 거는 역방향 SSH 터널로만 들어온다 (supervise.sh).
- Authorization: Bearer <TTS_TOKEN>이 맞아야 응답한다.
- 받은 음소는 로그에 남기지 않는다. 시간·길이만 남긴다.
실행: TTS_TOKEN=... ~/tts/.venv/bin/python ~/tts/gpu_tts/server.py
"""

import os

# 공용 서버 규칙: 우리 팀은 5번 GPU만 쓴다. onnxruntime을 불러오기 전에 정해야 한다
os.environ["CUDA_DEVICE_ORDER"] = "PCI_BUS_ID"
os.environ["CUDA_VISIBLE_DEVICES"] = "5"

import hmac
import io
import json
import threading
import time
import wave
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

import numpy as np
import onnxruntime as ort
from kokoro_onnx import Kokoro

MODEL_DIR = Path(os.environ.get("TTS_MODEL_DIR", str(Path.home() / "tts/models")))
PORT = int(os.environ.get("TTS_PORT", "18433"))
TOKEN = os.environ["TTS_TOKEN"]
MAX_PHONEMES = 2000  # 워커 /v1/tts 글 상한 400자의 음소보다 넉넉히
MAX_BODY_BYTES = 16 * 1024


def load_engine() -> Kokoro:
    """fp32 모델을 CUDA로. int8 모델은 양자화 연산이 GPU로 가지 않아 몇 배 느리다 (S15P31A305-370).
    cuDNN 알고리즘 탐색은 HEURISTIC: 기본(EXHAUSTIVE)은 문장 길이마다 탐색을 다시 해서 대사 하나 3초가 걸렸다."""
    ort.preload_dlls()  # onnxruntime-gpu[cuda,cudnn]가 함께 설치한 CUDA·cuDNN 라이브러리
    options = ort.SessionOptions()
    options.log_severity_level = 3  # CPU로 도는 일부 노드(Memcpy) 경고는 매번 같아서 끔
    session = ort.InferenceSession(
        str(MODEL_DIR / "kokoro-v1.0.onnx"),
        options,
        providers=[("CUDAExecutionProvider", {"device_id": 0, "cudnn_conv_algo_search": "HEURISTIC"}), "CPUExecutionProvider"],
    )
    return Kokoro.from_session(session, str(MODEL_DIR / "voices-v1.0.bin"))


engine = load_engine()
engine_lock = threading.Lock()  # 시제품: 한 번에 하나씩 (H200에서 대사 하나 0.4~0.6초)
VOICES = set(engine.get_voices())


def to_wav(audio: np.ndarray, sample_rate: int) -> bytes:
    """워커 app.tts.to_wav와 같은 형식 (16비트 모노). 워커가 조각을 이어 붙일 수 있어야 한다."""
    pcm = (np.clip(audio, -1, 1) * 32767).astype("<i2").tobytes()
    buf = io.BytesIO()
    with wave.open(buf, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sample_rate)
        w.writeframes(pcm)
    return buf.getvalue()


def synth(phonemes: str, voice: str, speed: float) -> tuple[bytes, float]:
    with engine_lock:
        audio, sample_rate = engine.create(phonemes, voice=voice, speed=speed, lang="en-us", is_phonemes=True)
    return to_wav(audio, sample_rate), len(audio) / sample_rate


def read_request(body: bytes) -> tuple[str, str, float]:
    """{"phonemes", "voice", "speed"} 검사. 틀리면 ValueError (메시지는 응답에 그대로)."""
    request = json.loads(body)
    phonemes, voice, speed = request.get("phonemes"), request.get("voice"), request.get("speed", 1.0)
    if not isinstance(phonemes, str) or not phonemes.strip() or len(phonemes) > MAX_PHONEMES:
        raise ValueError(f"phonemes는 1~{MAX_PHONEMES}자 문자열이어야 함")
    if voice not in VOICES:
        raise ValueError("없는 목소리")
    if not isinstance(speed, (int, float)) or not 0.5 <= speed <= 2.0:
        raise ValueError("speed는 0.5~2.0")
    return phonemes, voice, float(speed)


def log(event: dict) -> None:
    print(json.dumps({"t": time.strftime("%H:%M:%S"), **event}, ensure_ascii=False), flush=True)


class Handler(BaseHTTPRequestHandler):
    def reply_json(self, status: int, payload: dict) -> None:
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
            return self.reply_json(404, {"error": {"message": "없는 주소"}})
        self.reply_json(200, {"ok": True, "model": "kokoro-v1.0.onnx", "device": ort.get_device()})

    def do_POST(self):
        if self.path.rstrip("/") != "/v1/synth":
            return self.reply_json(404, {"error": {"message": "없는 주소"}})
        if not self.authorized():
            return self.reply_json(401, {"error": {"message": "토큰이 맞지 않음"}})
        size = int(self.headers.get("Content-Length") or 0)
        if size > MAX_BODY_BYTES:
            return self.reply_json(413, {"error": {"message": "요청이 너무 큼"}})
        try:
            phonemes, voice, speed = read_request(self.rfile.read(size))
        except ValueError as e:  # json.JSONDecodeError도 ValueError
            return self.reply_json(400, {"error": {"message": str(e)}})
        started = time.perf_counter()
        wav, seconds = synth(phonemes, voice, speed)
        log({"event": "synth", "audio_s": round(seconds, 2), "synth_s": round(time.perf_counter() - started, 3), "phonemes": len(phonemes)})
        self.send_response(200)
        self.send_header("Content-Type", "audio/wav")
        self.send_header("Content-Length", str(len(wav)))
        self.end_headers()
        self.wfile.write(wav)

    def log_message(self, *args):  # 기본 접근 로그 끔 (주소·경로만 남는 로그는 위 log로 충분)
        pass


if __name__ == "__main__":
    for line in ("Ready.", "Hi there, welcome in.", "That will be five dollars, and here is your receipt."):
        synth(engine.tokenizer.phonemize(line, "en-us"), "af_heart", 1.0)  # 첫 호출 준비(약 3초)와 길이별 준비를 미리
    log({"event": "start", "model": str(MODEL_DIR), "port": PORT, "device": ort.get_device()})
    ThreadingHTTPServer(("127.0.0.1", PORT), Handler).serve_forever()
