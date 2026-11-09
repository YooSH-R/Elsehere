"""공용 GPU 서버, 임대 GPU 서버, Windows 개발 PC의 장치 선택을 분리한다."""


def configure_device(environ, platform: str) -> int:
    local = environ.get("TTS_LOCAL_WINDOWS") == "1"
    if local and platform != "nt":
        raise ValueError("TTS_LOCAL_WINDOWS는 Windows 개발 PC에서만 사용합니다.")
    # 임대 서버는 GPU를 우리만 쓰고 한 장뿐이라 0번이다. 지정하지 않으면 공용 서버 규칙대로 5번만 쓴다.
    dedicated = environ.get("TTS_DEDICATED_GPU") == "1"
    environ["CUDA_DEVICE_ORDER"] = "PCI_BUS_ID"
    environ["CUDA_VISIBLE_DEVICES"] = "0" if local or dedicated else "5"
    return 2 if local else 8


def require_cuda(providers):
    if "CUDAExecutionProvider" not in providers:
        raise RuntimeError("CUDA 실행 장치가 로드되지 않았습니다. GPU 런타임과 드라이버를 확인하세요.")
