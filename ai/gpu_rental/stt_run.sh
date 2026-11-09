#!/bin/sh
# 임대 GPU 서버의 STT 실행기. SSAFY 서버의 ~/stt/gpu5.sh 대신 쓴다(setup.sh가 ~/stt/run.sh로 설치).
# GPU를 우리만 쓰고 한 장뿐이라 0번을 쓰고, pip로 받은 cuBLAS·cuDNN 경로를 CTranslate2에 알려 준다.
set -e
export CUDA_DEVICE_ORDER=PCI_BUS_ID
export CUDA_VISIBLE_DEVICES=0
PYTHON="$HOME/stt/.venv/bin/python"
CUDA_LIBS="$("$PYTHON" -c 'import nvidia.cublas.lib as b, nvidia.cudnn.lib as d; print(b.__path__[0] + ":" + d.__path__[0])')"
export LD_LIBRARY_PATH="$CUDA_LIBS${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
exec "$PYTHON" "$@"
