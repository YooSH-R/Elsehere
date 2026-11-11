#!/bin/sh
# 임대 GPU 서버에 STT·음성 합성 서버를 설치한다. 저장소의 ai/ 폴더를 서버에 올린 뒤 이 스크립트를 실행한다.
# 폴더 배치는 SSAFY GPU 서버와 같다(~/stt, ~/tts). 그래서 감시 스크립트·터널·워커 설정을 그대로 쓴다.
# 다시 실행해도 된다. 이미 설치된 가상환경과 모델은 버전만 맞춘다.
# 실행: sh ai/gpu_rental/setup.sh
set -eu

AI="$(cd "$(dirname "$0")/.." && pwd)"
STT="$HOME/stt"
TTS="$HOME/tts"
STT_MODEL="$STT/stt_finetune/checkpoints/turbo-lr1e4/ct2"
KOKORO=https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.0

command -v nvidia-smi > /dev/null || { echo "nvidia-smi가 없습니다. NVIDIA 드라이버가 있는 이미지로 빌려야 합니다."; exit 1; }
nvidia-smi --query-gpu=name,memory.total --format=csv,noheader

missing=""
for tool in curl ssh setsid pgrep sha256sum; do
  command -v "$tool" > /dev/null || missing="$missing $tool"
done
if [ -n "$missing" ]; then
  echo "설치할 도구:$missing"
  [ "$(id -u)" -eq 0 ] && SUDO="" || SUDO="sudo"
  $SUDO apt-get update -qq
  $SUDO apt-get install -y -qq curl openssh-client util-linux procps coreutils
fi

if ! command -v uv > /dev/null; then
  curl -LsSf https://astral.sh/uv/install.sh | sh
  PATH="$HOME/.local/bin:$PATH"
fi

# STT: SSAFY 서버 ~/stt/.venv와 같은 버전. CUDA 라이브러리는 pip 패키지로 받고 run.sh가 경로를 알려 준다.
mkdir -p "$STT/gpu_stt" "$STT_MODEL"
[ -x "$STT/.venv/bin/python" ] || uv venv -q --python 3.12 "$STT/.venv"
uv pip install -q --python "$STT/.venv/bin/python" \
  ctranslate2==4.8.3 faster-whisper==1.2.1 av==19.0.1 numpy==2.5.4 \
  nvidia-cublas-cu12==12.8.4.1 nvidia-cudnn-cu12==9.19.0.56
install -m 644 "$AI/gpu_stt/server.py" "$STT/gpu_stt/server.py"
install -m 644 "$AI/gpu_stt/speech_gate.py" "$STT/gpu_stt/speech_gate.py"
install -m 755 "$AI/gpu_stt/supervise.sh" "$STT/gpu_stt/supervise.sh"
install -m 755 "$AI/gpu_rental/stt_run.sh" "$STT/run.sh"

# 음성 합성: ai/gpu_tts/README.md 설치 순서와 같다.
mkdir -p "$TTS/gpu_tts" "$TTS/models"
[ -x "$TTS/.venv/bin/python" ] || uv venv -q --python 3.12 "$TTS/.venv"
uv pip install -q --python "$TTS/.venv/bin/python" kokoro-onnx==0.6.1
uv pip uninstall -q --python "$TTS/.venv/bin/python" onnxruntime 2> /dev/null || true  # GPU용과 같은 폴더를 써서 지운다
# 다시 실행하면 kokoro-onnx가 CPU용을 다시 끌어오고, 위에서 지울 때 GPU용 파일도 지워진다. 그래서 GPU용은 매번 다시 깐다.
uv pip install -q --python "$TTS/.venv/bin/python" --reinstall-package onnxruntime-gpu 'onnxruntime-gpu[cuda,cudnn]==1.23.2'
for file in kokoro-v1.0.onnx voices-v1.0.bin; do
  [ -s "$TTS/models/$file" ] || curl -sSL -o "$TTS/models/$file" "$KOKORO/$file"
done
(cd "$TTS/models" && sha256sum -c - << 'EOF'
7d5df8ecf7d4b1878015a32686053fd0eebe2bc377234608764cc0ef3636a6c5  kokoro-v1.0.onnx
bca610b8308e8d99f32e6fe4197e7ec01679264efed0cac9140fe9c29f1fbf7d  voices-v1.0.bin
EOF
)
install -m 644 "$AI/gpu_tts/server.py" "$TTS/gpu_tts/server.py"
install -m 644 "$AI/gpu_tts/runtime.py" "$TTS/gpu_tts/runtime.py"
install -m 755 "$AI/gpu_tts/supervise.sh" "$TTS/gpu_tts/supervise.sh"

# 저장소에 없는 파일. SSAFY GPU 서버에서 옮겨 온다(README "옮겨 올 파일").
ready=yes
for file in "$STT_MODEL/model.bin" "$STT/gpu_stt/token" "$STT/gpu_stt/tunnel_key" "$STT/gpu_stt/known_hosts" "$TTS/gpu_tts/token"; do
  if [ -s "$file" ]; then
    case "$file" in */model.bin) ;; *) chmod 600 "$file" ;; esac
  else
    echo "아직 없음: $file"
    ready=no
  fi
done
[ "$ready" = yes ] && echo "설치 끝. sh ai/gpu_rental/start.sh로 띄웁니다." || echo "설치 끝. 위 파일을 옮겨 온 뒤 start.sh로 띄웁니다."
