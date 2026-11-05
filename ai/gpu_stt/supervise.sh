#!/bin/sh
# GPU STT 시제품 감시: STT 서버(127.0.0.1:18431)와 배포 서버로 거는 역방향 SSH 터널이 죽으면 30초 안에 다시 띄운다.
# 터널 입구는 배포 서버의 도커 게이트웨이(개발 172.19.0.1:18431, 운영 172.20.0.1:18431)라 워커 컨테이너만 닿고 인터넷에는 열리지 않는다.
# 운영 터널은 배포 서버가 운영 입구 전달과 방화벽을 허용한 뒤 만드는 prod_tunnel_enabled 파일이 있을 때만 건다.
# 배포 서버가 tunnel_key.pub를 포트 전달 전용으로 등록하고 GatewayPorts clientspecified를 켠 뒤에만 실행한다.
# 실행: setsid nohup ~/stt/gpu_stt/supervise.sh > ~/stt/gpu_stt/supervise.log 2>&1 &
# Jupyter 세션이 꺼지면 이 스크립트도 같이 꺼진다 (systemd KillMode=control-group). 그동안 워커는 Groq로 넘긴다.
DIR="$HOME/stt/gpu_stt"
DEPLOY="a305-stt-tunnel@k15a305.p.ssafy.io"  # 포트 전달만 되는 전용 계정 (셸·명령·SFTP 차단)
DEV_BIND="172.19.0.1:18431"
PROD_BIND="172.20.0.1:18431"
ROUNDS=0

open_tunnel() {
  if ! pgrep -u "$(id -un)" -f "ssh .*-R $1:" > /dev/null; then
    echo "$(date +%T) $1 터널 다시 걺"
    setsid nohup ssh -N -i "$DIR/tunnel_key" -o IdentitiesOnly=yes -o BatchMode=yes -o ExitOnForwardFailure=yes \
      -o ServerAliveInterval=15 -o ServerAliveCountMax=3 -o UserKnownHostsFile="$DIR/known_hosts" \
      -R "$1:127.0.0.1:18431" "$DEPLOY" >> "$DIR/tunnel.log" 2>&1 &
  fi
}

while true; do
  # JupyterHub는 24시간 활동이 없으면 세션을 끄고(이 스크립트·서버·터널도 같이 꺼짐), STT 요청은 활동으로 치지 않는다.
  # 30분마다 우리 Jupyter 서버의 API를 한 번 불러 활동으로 남긴다 (사용자 결정, 2026-10-28).
  if [ $((ROUNDS % 60)) -eq 0 ] && [ -n "$JUPYTERHUB_API_TOKEN" ]; then
    curl -s -m 5 -o /dev/null -H "Authorization: token $JUPYTERHUB_API_TOKEN" "${JUPYTERHUB_SERVICE_URL%/}/api/contents/stt?content=0" \
      && echo "$(date +%T) 활동 기록"
  fi
  ROUNDS=$((ROUNDS + 1))
  if ! pgrep -u "$(id -un)" -f "gpu_stt/server.py" > /dev/null; then  # 모델을 읽는 중(health 응답 전)에는 건드리지 않는다
    echo "$(date +%T) STT 서버 다시 띄움"
    STT_TOKEN="$(cat "$DIR/token")" setsid nohup "$HOME/stt/gpu5.sh" "$DIR/server.py" >> "$DIR/server.log" 2>&1 &
  fi
  open_tunnel "$DEV_BIND"
  if [ -f "$DIR/prod_tunnel_enabled" ]; then
    open_tunnel "$PROD_BIND"
  fi
  sleep 30
done
