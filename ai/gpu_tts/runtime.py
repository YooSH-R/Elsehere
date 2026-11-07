"""공용 GPU 서버와 Windows 개발 PC의 장치 선택을 분리한다."""


def configure_device(environ, platform: str) -> int:
    local = environ.get("TTS_LOCAL_WINDOWS") == "1"
    if local and platform != "nt":
        raise ValueError("TTS_LOCAL_WINDOWS는 Windows 개발 PC에서만 사용합니다.")
    environ["CUDA_DEVICE_ORDER"] = "PCI_BUS_ID"
    environ["CUDA_VISIBLE_DEVICES"] = "0" if local else "5"
    return 2 if local else 8


def require_cuda(providers):
    if "CUDAExecutionProvider" not in providers:
        raise RuntimeError("CUDA 실행 장치가 로드되지 않았습니다. GPU 런타임과 드라이버를 확인하세요.")
