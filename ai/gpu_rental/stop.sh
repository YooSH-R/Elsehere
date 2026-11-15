#!/bin/sh
# 지정한 서비스의 감시 스크립트, 서버, 배포 서버로 가는 터널을 끈다. SSAFY GPU 서버와 임대 서버에서 똑같이 쓴다.
# 같은 터널 입구(172.19.0.1·172.30.0.1의 포트)는 한 서버만 잡을 수 있어, 옮기기 전에 원래 서버에서 먼저 실행한다.
# 사용: sh stop.sh [stt] [tts] [guard]   (인자가 없으면 stt tts. guard는 무례 판정·NPC 대답 vLLM과 18432 터널)
set -u
me="$(id -un)"
left=""

# 감시 스크립트를 먼저 꺼야 서버·터널을 다시 띄우지 않는다. vLLM은 내려가는 데 몇 초 걸려 최대 30초 기다린다.
stop_service() {
  name="$1"
  shift
  for pattern in "$@"; do
    pkill -u "$me" -f "$pattern"
  done
  for _ in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15; do
    alive=no
    for pattern in "$@"; do
      pgrep -u "$me" -f "$pattern" > /dev/null && alive=yes
    done
    [ "$alive" = no ] && return
    sleep 2
  done
  left="$left $name"
}

[ $# -eq 0 ] && set -- stt tts
services="$*"
for service in "$@"; do
  case "$service" in
    stt) stop_service stt "gpu_stt/supervise.sh" "gpu_stt/server.py" "ssh .*-R 172\.(19|30)\.0\.1:18431:" ;;
    tts) stop_service tts "gpu_tts/supervise.sh" "gpu_tts/server.py" "ssh .*-R 172\.(19|30)\.0\.1:18433:" ;;
    guard) stop_service guard "guard/supervise.sh" "vllm serve Qwen/Qwen3.8-27B" "ssh .*-R 172\.(19|30)\.0\.1:18432:" ;;
    *)
      echo "모르는 서비스: $service (stt·tts·guard 중에서 고릅니다)"
      exit 2
      ;;
  esac
done

if [ -n "$left" ]; then
  echo "아직 남은 프로세스가 있습니다:$left"
  exit 1
fi
echo "껐습니다: $services"
