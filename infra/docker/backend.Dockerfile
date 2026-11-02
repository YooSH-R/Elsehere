FROM eclipse-temurin:21-jre-alpine

RUN apk add --no-cache curl \
    && addgroup -S app && adduser -S -G app app
WORKDIR /app
RUN mkdir -p /app/data/npc-audio /app/logs && chown app:app /app/data/npc-audio /app/logs

# Consume the JAR produced by the successful backend verification job.
COPY --chown=app:app backend/build/libs/app.jar /app/app.jar
USER app
EXPOSE 8080
ENTRYPOINT ["java", "-jar", "/app/app.jar"]
