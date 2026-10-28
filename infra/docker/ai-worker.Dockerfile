FROM python:3.12-slim-bookworm

ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1
WORKDIR /app
COPY ai/worker/requirements.txt ai/worker/requirements.txt
COPY ai/feedback/requirements.txt ai/feedback/requirements.txt
RUN python -m pip install --no-cache-dir \
    -r ai/worker/requirements.txt -r ai/feedback/requirements.txt \
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
