FROM nginx:1.30.6-alpine

COPY infra/nginx/web.conf /etc/nginx/conf.d/default.conf
ARG WEBGL_DIRECTORY=dist/webgl
COPY ${WEBGL_DIRECTORY}/ /usr/share/nginx/html/
