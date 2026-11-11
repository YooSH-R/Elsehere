"""GPU 모델을 로드하지 않고 STT 서버의 묶음 상한 설정만 검증한다."""

import subprocess
import sys
from pathlib import Path

SERVER_FILE = Path(__file__).resolve().parents[2] / "gpu_stt" / "server.py"

# 독립 프로세스에서 BATCH_MAX 대입문과 범위 확인만 실행해 GPU 라이브러리·모델 초기화를 피한다.
SCRIPT = """
import ast
import os
import sys
from pathlib import Path

source = ast.parse(Path(sys.argv[1]).read_text(encoding="utf-8"))
setting = [
    node
    for node in source.body
    if (isinstance(node, ast.Assign) and ast.unparse(node.targets[0]) == "BATCH_MAX")
    or (isinstance(node, ast.If) and "BATCH_MAX" in ast.unparse(node.test))
]
namespace = {"os": os}
exec(compile(ast.Module(body=setting, type_ignores=[]), sys.argv[1], "exec"), namespace)
print(namespace["BATCH_MAX"])
"""


def run_batch_max_setting(env_value: str | None) -> subprocess.CompletedProcess:
    env = {"PATH": "", "SYSTEMROOT": ""}
    if env_value is not None:
        env["STT_BATCH_MAX"] = env_value
    return subprocess.run([sys.executable, "-B", "-c", SCRIPT, str(SERVER_FILE)], capture_output=True, text=True, env=env)


def test_batch_max_defaults_to_16_for_the_ssafy_server():
    result = run_batch_max_setting(None)

    assert result.returncode == 0, result.stderr
    assert result.stdout.strip() == "16"


def test_batch_max_follows_stt_batch_max_for_smaller_gpus():
    result = run_batch_max_setting("4")

    assert result.returncode == 0, result.stderr
    assert result.stdout.strip() == "4"


def test_batch_max_below_one_stops_the_server_at_startup():
    result = run_batch_max_setting("0")

    assert result.returncode != 0
    assert "STT_BATCH_MAX" in result.stderr
