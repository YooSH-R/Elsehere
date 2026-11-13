#!/bin/sh
# 임대 GPU 서버에 설치된 서비스(STT·음성 합성·판정 vLLM)의 감시 스크립트를 띄운다. 감시 스크립트가 서버와 배포 서버 터널을 30초마다 살핀다.
# 임대 서버(파드)가 다시 시작되면 이 스크립트를 다시 실행한다. 이미 떠 있는 감시 스크립트는 건드리지 않는다.
# 터널은 각 폴더의 tunnel_enabled·prod_tunnel_enabled 파일이 있을 때만 건다(README "전환").
set -eu

running() { pgrep -u "$(id -un)" -f "$1" > /dev/null; }

if [ -x "$HOME/stt/gpu_stt/supervise.sh" ]; then
  if running "gpu_stt/supervise.sh"; then
    echo "STT 감시 이미 실행 중"
  else
    STT_RUNNER="$HOME/stt/run.sh" setsid nohup "$HOME/stt/gpu_stt/supervise.sh" >> "$HOME/stt/gpu_stt/supervise.log" 2>&1 &
    echo "STT 감시 띄움"
  fi
fi

if [ -x "$HOME/tts/gpu_tts/supervise.sh" ]; then
  if running "gpu_tts/supervise.sh"; then
    echo "합성 감시 이미 실행 중"
  else
    TTS_DEDICATED_GPU=1 setsid nohup "$HOME/tts/gpu_tts/supervise.sh" >> "$HOME/tts/gpu_tts/supervise.log" 2>&1 &
    echo "합성 감시 띄움"
  fi
fi

if [ -x "$HOME/guard/supervise.sh" ]; then
  if running "guard/supervise.sh"; then
    echo "판정 감시 이미 실행 중"
  else
    # 임대 서버 GPU는 0번 한 장이다. 지금 서비스와 같은 약 71GB를 쓰도록 141GB 카드는 0.5, 100GB 미만 카드(H100 80GB 등)는 0.9로 띄운다.
    total_mib=$(nvidia-smi --query-gpu=memory.total --format=csv,noheader,nounits | head -n 1)
    if [ "$total_mib" -lt 100000 ]; then memory=0.9; else memory=0.5; fi
    GUARD_GPU_DEVICE=0 GUARD_GPU_MEMORY="${GUARD_GPU_MEMORY:-$memory}" setsid nohup "$HOME/guard/supervise.sh" >> "$HOME/guard/supervise.log" 2>&1 &
    echo "판정 감시 띄움 (메모리 비율 ${GUARD_GPU_MEMORY:-$memory})"
  fi
fi
