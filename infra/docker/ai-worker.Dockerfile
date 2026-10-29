FROM python:3.12-slim-bookworm

ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1
WORKDIR /app

# 음성 합성 모델 (Kokoro-82M 8비트 ONNX, CPU). 받은 파일의 sha256이 다르면 빌드가 실패한다
ARG KOKORO_RELEASE=https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.0
ADD --chmod=644 --checksum=sha256:6e742170d309016e5891a994e1ce1559c702a2ccd0075e67ef7157974f6406cb \
    ${KOKORO_RELEASE}/kokoro-v1.0.int8.onnx /opt/kokoro/kokoro-v1.0.int8.onnx
ADD --chmod=644 --checksum=sha256:bca610b8308e8d99f32e6fe4197e7ec01679264efed0cac9140fe9c29f1fbf7d \
    ${KOKORO_RELEASE}/voices-v1.0.bin /opt/kokoro/voices-v1.0.bin
# ADD --chmod가 새로 만든 폴더에도 644를 적용해 app 사용자가 들어가지 못하므로 폴더만 되돌린다
RUN chmod 755 /opt/kokoro
ENV KOKORO_MODEL=/opt/kokoro/kokoro-v1.0.int8.onnx KOKORO_VOICES=/opt/kokoro/voices-v1.0.bin

COPY ai/worker/requirements.txt ai/worker/requirements.txt
COPY ai/worker/requirements-tts.txt ai/worker/requirements-tts.txt
COPY ai/feedback/requirements.txt ai/feedback/requirements.txt
RUN python -m pip install --no-cache-dir \
    -r ai/worker/requirements.txt -r ai/worker/requirements-tts.txt -r ai/feedback/requirements.txt \
    && useradd --create-home --uid 10001 app

COPY --chown=app:app ai/worker/app ai/worker/app
COPY --chown=app:app ai/worker/llm_profiles.json ai/worker/llm_profiles.json
COPY --chown=app:app ai/feedback/feedback ai/feedback/feedback
COPY --chown=app:app ai/feedback/content ai/feedback/content
COPY --chown=app:app ai/feedback/prompts ai/feedback/prompts
COPY --chown=app:app ai/feedback/schemas ai/feedback/schemas
USER app
WORKDIR /app/ai/worker
EXPOSE 8020
CMD ["python", "-m", "uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8020"]
