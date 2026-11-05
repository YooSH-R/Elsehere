"""GPU 모델을 로드하지 않고 독립 서버의 로그 설정만 검증한다."""

import json
import logging
import subprocess
import sys
from pathlib import Path

import pytest


@pytest.mark.parametrize("server", ["gpu_stt", "gpu_tts"])
@pytest.mark.parametrize("level", ["INFO", "DEBUG"])
def test_gpu_log_level_does_not_enable_library_info_logs(server, level):
    server_file = Path(__file__).resolve().parents[2] / server / "server.py"
    # 독립 프로세스에서 실행해 pytest의 전역 로그 설정과 GPU 라이브러리·모델 초기화를 피한다.
    script = """
import ast
import json
import logging
import os
import sys
from pathlib import Path

os.environ["GPU_LOG_LEVEL"] = sys.argv[3]
source = ast.parse(Path(sys.argv[1]).read_text(encoding="utf-8"))
configuration = [node for node in source.body if isinstance(node, ast.Expr) and ast.unparse(node).startswith("logging.")]
exec(compile(ast.Module(body=configuration, type_ignores=[]), sys.argv[1], "exec"))
print(json.dumps({
    "server": logging.getLogger(sys.argv[2]).getEffectiveLevel(),
    "library_info": logging.getLogger("runtime_dependency").isEnabledFor(logging.INFO),
    "library_warning": logging.getLogger("runtime_dependency").isEnabledFor(logging.WARNING),
}))
"""
    result = subprocess.run(
        [sys.executable, "-B", "-c", script, str(server_file), server, level], capture_output=True, text=True, check=True
    )
    settings = json.loads(result.stdout)
    assert settings == {"server": getattr(logging, level), "library_info": False, "library_warning": True}
