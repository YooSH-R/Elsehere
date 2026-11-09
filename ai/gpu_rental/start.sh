#!/bin/sh
# 임대 GPU 서버에서 STT·음성 합성 감시 스크립트를 띄운다. 감시 스크립트가 서버와 배포 서버 터널을 30초마다 살핀다.
# 임대 서버(파드)가 다시 시작되면 이 스크립트를 다시 실행한다. 이미 떠 있는 감시 스크립트는 건드리지 않는다.
# 터널은 각 폴더의 tunnel_enabled·prod_tunnel_enabled 파일이 있을 때만 건다(README "전환").
set -eu

if pgrep -u "$(id -un)" -f "gpu_stt/supervise.sh" > /dev/null; then
  echo "STT 감시 이미 실행 중"
else
  STT_RUNNER="$HOME/stt/run.sh" setsid nohup "$HOME/stt/gpu_stt/supervise.sh" >> "$HOME/stt/gpu_stt/supervise.log" 2>&1 &
  echo "STT 감시 띄움"
fi

if pgrep -u "$(id -un)" -f "gpu_tts/supervise.sh" > /dev/null; then
  echo "합성 감시 이미 실행 중"
else
  TTS_DEDICATED_GPU=1 setsid nohup "$HOME/tts/gpu_tts/supervise.sh" >> "$HOME/tts/gpu_tts/supervise.log" 2>&1 &
  echo "합성 감시 띄움"
fi
