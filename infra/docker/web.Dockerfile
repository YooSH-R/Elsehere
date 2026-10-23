FROM nginx:1.30.6-alpine

COPY infra/nginx/web.conf /etc/nginx/conf.d/default.conf
ARG WEBGL_DIRECTORY=dist/webgl
COPY ${WEBGL_DIRECTORY}/ /usr/share/nginx/html/

# Imported build archives can retain owner-only permissions.
RUN find /usr/share/nginx/html -type d -exec chmod 755 {} + \
    && find /usr/share/nginx/html -type f -exec chmod 644 {} +
