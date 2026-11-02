FROM python:3.12-slim-bookworm

ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1 \
    KOKORO_MODEL=/opt/kokoro/kokoro-v1.0.int8.onnx \
    KOKORO_VOICES=/opt/kokoro/voices-v1.0.bin \
    TTS_REQUIRE_TIMING=true
WORKDIR /app

# 음성 합성 모델 (Kokoro-82M 8비트 ONNX, CPU). 받은 파일의 sha256이 다르면 빌드가 실패한다
ARG KOKORO_RELEASE=https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.1
ADD --chmod=644 --checksum=sha256:ae315a79b623f244700e4afb9246c46a26066782e049ba174bf3ba433970ee9c \
    ${KOKORO_RELEASE}/kokoro-v1.0.int8.onnx /opt/kokoro/kokoro-v1.0.int8.onnx
ADD --chmod=644 --checksum=sha256:bca610b8308e8d99f32e6fe4197e7ec01679264efed0cac9140fe9c29f1fbf7d \
    ${KOKORO_RELEASE}/voices-v1.0.bin /opt/kokoro/voices-v1.0.bin
# ADD --chmod가 새로 만든 폴더에도 644를 적용해 app 사용자가 들어가지 못하므로 폴더만 되돌린다
RUN chmod 755 /opt/kokoro

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
# NPC 인격 카드 조합 규칙 데이터 (행동 조각·생성 재료). app.npc_persona가 ai/npcs/rules에서 읽는다
COPY --chown=app:app ai/npcs/rules ai/npcs/rules
COPY --chown=app:app ai/feedback/feedback ai/feedback/feedback
COPY --chown=app:app ai/feedback/content ai/feedback/content
COPY --chown=app:app ai/feedback/prompts ai/feedback/prompts
COPY --chown=app:app ai/feedback/schemas ai/feedback/schemas
USER app
WORKDIR /app/ai/worker
RUN python -c "import os; from app.tts import Kokoro; voice = Kokoro.load(os.environ['KOKORO_MODEL'], os.environ['KOKORO_VOICES']); assert voice and voice.phoneme_timing_ready, 'Kokoro phoneme timing warmup failed'"
EXPOSE 8020
CMD ["python", "-m", "uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8020"]
