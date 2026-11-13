#!/bin/bash
# 무례 판정·NPC 대답 vLLM 서버 (S15P31A305-327). 키는 ~/guard/vllm_token (명령줄에 남기지 않음)
# GPU는 SSAFY 공용 서버 규칙대로 5번만 쓴다. GPU를 우리만 쓰는 임대 서버는 GUARD_GPU_DEVICE=0으로 띄운다.
# 사용: ~/guard/serve.sh <모델> [vllm 옵션...]
export CUDA_DEVICE_ORDER=PCI_BUS_ID CUDA_VISIBLE_DEVICES="${GUARD_GPU_DEVICE:-5}" HF_HOME=$HOME/guard/hf_cache HF_HUB_OFFLINE=1
# FlashInfer 샘플러는 실행 중에 ninja·nvcc로 컴파일하려 해서 이 서버에선 못 쓴다
export VLLM_USE_FLASHINFER_SAMPLER=0
export VLLM_API_KEY="$(cat $HOME/guard/vllm_token)"
model="$1"; shift
exec $HOME/guard/.venv/bin/vllm serve "$model" --host 0.0.0.0 --port 18432 --gpu-memory-utilization 0.75 --max-model-len 4096 "$@"
