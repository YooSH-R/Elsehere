#!/bin/sh
# 무례 판정 GPU 서비스 감시 (S15P31A305-343): vLLM(127.0.0.1:18432, Qwen3.8-27B + LoRA)과 배포 서버로 거는 역방향 SSH 터널이 죽으면 30초 안에 다시 띄운다.
# 터널은 배포 서버가 a305-stt-tunnel 계정에 입구 전달과 방화벽을 허용한 뒤에만 건다 (S15P31A305-601).
# 개발 172.19.0.1:18432는 ~/guard/tunnel_enabled, 운영 172.30.0.1:18432는 ~/guard/prod_tunnel_enabled 파일이 있을 때.
# Jupyter 활동 기록은 STT 감시(~/stt/gpu_stt/supervise.sh)가 한다. Jupyter 세션이 꺼지면 같이 꺼지고, 그동안 워커는 GMS 판정 모델로 넘긴다.
# 실행: setsid nohup ~/guard/supervise.sh > ~/guard/supervise.log 2>&1 &
G="$HOME/guard"
STT="$HOME/stt/gpu_stt"
DEPLOY="a305-stt-tunnel@k15a305.p.ssafy.io"
DEV_BIND="172.19.0.1:18432"
PROD_BIND="172.30.0.1:18432"
ADAPTER="guard-qwen3.8-27b-r16=$G/models/guard-qwen3.8-27b-r16"
# NPC 대답 LoRA(S15P31A305-789)는 폴더가 있을 때만 같이 얹는다. NPC 대답 프롬프트가 4천 토큰에 가까워 길이를 늘린다.
NPC_ADAPTER="npc-qwen3.8-27b-r16=$G/models/npc-qwen3.8-27b-r16"

open_tunnel() {
  if ! pgrep -u "$(id -un)" -f "ssh .*-R $1:" > /dev/null; then
    echo "$(date +%T) $1 판정 터널 다시 걺"
    setsid nohup ssh -N -i "$STT/tunnel_key" -o IdentitiesOnly=yes -o BatchMode=yes -o ExitOnForwardFailure=yes \
      -o ServerAliveInterval=15 -o ServerAliveCountMax=3 -o UserKnownHostsFile="$STT/known_hosts" \
      -R "$1:127.0.0.1:18432" "$DEPLOY" >> "$G/tunnel.log" 2>&1 &
  fi
}

while true; do
  if ! pgrep -u "$(id -un)" -f "vllm serve Qwen/Qwen3.8-27B" > /dev/null; then  # 읽는 중(health 응답 전)에는 건드리지 않는다
    echo "$(date +%T) 판정 vLLM 다시 띄움"
    if [ -d "$G/models/npc-qwen3.8-27b-r16" ]; then set -- "$ADAPTER" "$NPC_ADAPTER"; else set -- "$ADAPTER"; fi
    setsid nohup "$G/serve.sh" Qwen/Qwen3.8-27B --host 127.0.0.1 --gpu-memory-utilization 0.5 --max-num-seqs 32 \
      --compilation-config '{"cudagraph_capture_sizes": [1, 2, 4, 8, 16, 32]}' \
      --enable-lora --max-loras 2 --max-lora-rank 16 --max-model-len 8192 --lora-modules "$@" >> "$G/serve-service.log" 2>&1 &
  fi
  if [ -f "$G/tunnel_enabled" ]; then
    open_tunnel "$DEV_BIND"
  fi
  if [ -f "$G/prod_tunnel_enabled" ]; then
    open_tunnel "$PROD_BIND"
  fi
  sleep 30
done
