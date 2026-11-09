#!/bin/sh
# STT·음성 합성 감시, 서버, 배포 서버로 가는 두 서비스의 터널을 끈다. SSAFY GPU 서버와 임대 서버에서 똑같이 쓴다.
# 같은 터널 입구(172.19.0.1·172.30.0.1의 18431·18433)는 한 서버만 잡을 수 있어, 옮기기 전에 원래 서버에서 먼저 실행한다.
# 무례 판정(18432)은 건드리지 않는다.
set -u
me="$(id -un)"
pkill -u "$me" -f "gpu_stt/supervise.sh"
pkill -u "$me" -f "gpu_tts/supervise.sh"
pkill -u "$me" -f "gpu_stt/server.py"
pkill -u "$me" -f "gpu_tts/server.py"
pkill -u "$me" -f "ssh .*-R 172\.(19|30)\.0\.1:1843[13]:"
sleep 1
if pgrep -u "$me" -af "gpu_(stt|tts)/(supervise\.sh|server\.py)|-R 172\.(19|30)\.0\.1:1843[13]:"; then
  echo "아직 남은 프로세스가 있습니다."
  exit 1
fi
echo "STT·합성 감시와 터널을 껐습니다."
