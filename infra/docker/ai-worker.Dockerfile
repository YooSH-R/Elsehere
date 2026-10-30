FROM python:3.12-slim-bookworm

ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1 \
    KOKORO_MODEL=/opt/kokoro/kokoro-v1.0.int8.onnx \
    KOKORO_VOICES=/opt/kokoro/voices-v1.0.bin \
    TTS_REQUIRE_TIMING=true
WORKDIR /app
COPY ai/worker/requirements.txt ai/worker/requirements.txt
COPY ai/worker/requirements-tts.txt ai/worker/requirements-tts.txt
COPY ai/tools/prepare_kokoro.py /tmp/prepare_kokoro.py
COPY ai/feedback/requirements.txt ai/feedback/requirements.txt
RUN python -m pip install --no-cache-dir \
    -r ai/worker/requirements.txt -r ai/feedback/requirements.txt \
    -r ai/worker/requirements-tts.txt \
    && python /tmp/prepare_kokoro.py --directory /opt/kokoro \
    && useradd --create-home --uid 10001 app

COPY --chown=app:app ai/worker/app ai/worker/app
COPY --chown=app:app ai/worker/llm_profiles.json ai/worker/llm_profiles.json
COPY --chown=app:app ai/feedback/feedback ai/feedback/feedback
COPY --chown=app:app ai/feedback/content ai/feedback/content
COPY --chown=app:app ai/feedback/prompts ai/feedback/prompts
COPY --chown=app:app ai/feedback/schemas ai/feedback/schemas
USER app
WORKDIR /app/ai/worker
RUN python -c "import os; from app.tts import Kokoro; voice = Kokoro.load(os.environ['KOKORO_MODEL'], os.environ['KOKORO_VOICES']); assert voice and voice.phoneme_timing_ready, 'Kokoro phoneme timing warmup failed'"
EXPOSE 8020
CMD ["python", "-m", "uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8020"]
