import runpy
from pathlib import Path

import pytest

runtime = runpy.run_path(str(Path(__file__).resolve().parents[2] / "gpu_tts/runtime.py"))
configure_device = runtime["configure_device"]
require_cuda = runtime["require_cuda"]


def test_shared_server_keeps_gpu_five_even_if_inherited_environment_selects_zero():
    environ = {"CUDA_VISIBLE_DEVICES": "0"}
    assert configure_device(environ, "posix") == 8
    assert environ["CUDA_VISIBLE_DEVICES"] == "5"
    assert environ["CUDA_DEVICE_ORDER"] == "PCI_BUS_ID"


def test_explicit_windows_local_mode_uses_the_laptop_gpu_and_two_synthesis_slots():
    environ = {"TTS_LOCAL_WINDOWS": "1"}
    assert configure_device(environ, "nt") == 2
    assert environ["CUDA_VISIBLE_DEVICES"] == "0"


def test_windows_local_flag_cannot_change_the_shared_linux_gpu_selection():
    with pytest.raises(ValueError, match="Windows"):
        configure_device({"TTS_LOCAL_WINDOWS": "1"}, "posix")


def test_cpu_fallback_is_not_reported_as_a_working_gpu():
    with pytest.raises(RuntimeError, match="CUDA"):
        require_cuda(["CPUExecutionProvider"])


def test_loaded_cuda_session_is_accepted():
    require_cuda(["CUDAExecutionProvider", "CPUExecutionProvider"])
