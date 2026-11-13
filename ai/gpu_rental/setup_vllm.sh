#!/bin/sh
# 임대 GPU 서버에 무례 판정·NPC 대답 vLLM(Qwen3.8-27B + LoRA 2개)을 설치한다. 저장소의 ai/ 폴더를 서버에 올린 뒤 실행한다.
# SSAFY GPU 서버 ~/guard와 같은 폴더·vLLM 판·모델 판을 써서 실행·감시 스크립트와 워커 설정을 그대로 쓴다.
# 다시 실행해도 된다. 받은 모델은 다시 받지 않는다.
# 실행: sh ai/gpu_rental/setup_vllm.sh
set -eu

AI="$(cd "$(dirname "$0")/.." && pwd)"
G="$HOME/guard"
# SSAFY 서버에서 서비스 중인 판. vLLM은 PyPI가 아닌 공식 릴리스의 CUDA 12.9 휠, PyTorch도 cu129 판이다.
VLLM_WHEEL="https://github.com/vllm-project/vllm/releases/download/v0.32.0/vllm-0.32.0+cu129-cp38-abi3-manylinux_2_28_x86_64.whl"
MODEL="Qwen/Qwen3.8-27B"
MODEL_REVISION="1d4bf0f2ff6012fd82039f2fa52739d0dd7c60c0"
NEED_DISK_GB=90  # 모델 약 54GB + vLLM 환경 약 15GB + 여유

command -v nvidia-smi > /dev/null || { echo "nvidia-smi가 없습니다. NVIDIA 드라이버가 있는 이미지로 빌려야 합니다."; exit 1; }
nvidia-smi --query-gpu=name,memory.total --format=csv,noheader

free_gb=$(df -Pk "$HOME" | awk 'NR == 2 { print int($4 / 1024 / 1024) }')
if [ "$free_gb" -lt "$NEED_DISK_GB" ]; then
  echo "홈($HOME) 디스크 여유가 ${free_gb}GB입니다. ${NEED_DISK_GB}GB 이상인 디스크를 홈으로 지정합니다(README \"서버 조건\")."
  exit 1
fi

if ! command -v uv > /dev/null; then
  curl -LsSf https://astral.sh/uv/install.sh | sh
  PATH="$HOME/.local/bin:$PATH"
fi

mkdir -p "$G/models"
[ -x "$G/.venv/bin/python" ] || uv venv -q --python 3.12 "$G/.venv"
uv pip install -q --python "$G/.venv/bin/python" --torch-backend cu129 "vllm @ $VLLM_WHEEL"

# serve.sh는 HF_HUB_OFFLINE=1로 refs/main이 가리키는 판을 읽는다. 커밋 해시로 받으면 refs/main이 생기지 않아 직접 적는다.
HF_HOME="$G/hf_cache" "$G/.venv/bin/hf" download "$MODEL" --revision "$MODEL_REVISION"
refs="$G/hf_cache/hub/models--Qwen--Qwen3.8-27B/refs"
mkdir -p "$refs"
printf '%s' "$MODEL_REVISION" > "$refs/main"

install -m 755 "$AI/guard/serve.sh" "$G/serve.sh"
install -m 755 "$AI/guard/supervise.sh" "$G/supervise.sh"

# 저장소에 없는 파일. SSAFY GPU 서버에서 옮겨 온다(README "옮겨 올 파일").
ready=yes
for file in "$G/models/guard-qwen3.8-27b-r16/adapter_model.safetensors" "$G/models/npc-qwen3.8-27b-r16/adapter_model.safetensors" \
  "$G/vllm_token" "$HOME/stt/gpu_stt/tunnel_key" "$HOME/stt/gpu_stt/known_hosts"; do
  if [ -s "$file" ]; then
    case "$file" in *.safetensors) ;; *) chmod 600 "$file" ;; esac
  else
    echo "아직 없음: $file"
    ready=no
  fi
done
[ "$ready" = yes ] && echo "설치 끝. sh ai/gpu_rental/start.sh로 띄웁니다." || echo "설치 끝. 위 파일을 옮겨 온 뒤 start.sh로 띄웁니다."
