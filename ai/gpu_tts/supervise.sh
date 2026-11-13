#!/bin/sh
# GPU 서버 음성 합성 감시 (S15P31A305-371): 합성 서버(127.0.0.1:18433)와 배포 서버로 거는 역방향 SSH 터널이 죽으면 30초 안에 다시 띄운다.
# 터널은 배포 서버가 a305-stt-tunnel 계정에 입구 전달과 방화벽을 허용한 뒤에만 건다.
# 개발 172.19.0.1:18433은 ~/tts/gpu_tts/tunnel_enabled, 운영 172.30.0.1:18433은 ~/tts/gpu_tts/prod_tunnel_enabled 파일이 있을 때.
# 예비 입구(18443)는 주 GPU가 꺼졌을 때 워커가 다음으로 부르는 자리다. 개발은 dev_backup_tunnel_enabled, 운영은 prod_backup_tunnel_enabled 파일이 있을 때.
# Jupyter 활동 기록은 STT 감시(~/stt/gpu_stt/supervise.sh)가 한다. Jupyter 세션이 꺼지면 같이 꺼지고, 그동안 워커는 CPU 합성으로 넘긴다.
# 실행: setsid nohup ~/tts/gpu_tts/supervise.sh > ~/tts/gpu_tts/supervise.log 2>&1 &
DIR="$HOME/tts/gpu_tts"
STT="$HOME/stt/gpu_stt"
DEPLOY="a305-stt-tunnel@k15a305.p.ssafy.io"  # 포트 전달만 되는 전용 계정 (셸·명령·SFTP 차단)
DEV_BIND="172.19.0.1:18433"
PROD_BIND="172.30.0.1:18433"
DEV_BACKUP_BIND="172.19.0.1:18443"
PROD_BACKUP_BIND="172.30.0.1:18443"

open_tunnel() {
  if ! pgrep -u "$(id -un)" -f "ssh .*-R $1:" > /dev/null; then
    echo "$(date +%T) $1 합성 터널 다시 걺"
    setsid nohup ssh -N -i "$STT/tunnel_key" -o IdentitiesOnly=yes -o BatchMode=yes -o ExitOnForwardFailure=yes \
      -o ServerAliveInterval=15 -o ServerAliveCountMax=3 -o UserKnownHostsFile="$STT/known_hosts" \
      -R "$1:127.0.0.1:18433" "$DEPLOY" >> "$DIR/tunnel.log" 2>&1 &
  fi
}

while true; do
  if ! pgrep -u "$(id -un)" -f "gpu_tts/server.py" > /dev/null; then  # 모델을 읽는 중(health 응답 전)에는 건드리지 않는다
    echo "$(date +%T) 합성 서버 다시 띄움"
    TTS_TOKEN="$(cat "$DIR/token")" setsid nohup "$HOME/tts/.venv/bin/python" "$DIR/server.py" >> "$DIR/server.log" 2>&1 &
  fi
  if [ -f "$DIR/tunnel_enabled" ]; then
    open_tunnel "$DEV_BIND"
  fi
  if [ -f "$DIR/prod_tunnel_enabled" ]; then
    open_tunnel "$PROD_BIND"
  fi
  if [ -f "$DIR/dev_backup_tunnel_enabled" ]; then
    open_tunnel "$DEV_BACKUP_BIND"
  fi
  if [ -f "$DIR/prod_backup_tunnel_enabled" ]; then
    open_tunnel "$PROD_BACKUP_BIND"
  fi
  sleep 30
done
