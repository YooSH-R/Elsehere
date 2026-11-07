# GPU 서버 Jupyter가 시작될 때 STT·합성·판정 감시 스크립트를 띄운다 (S15P31A305-607).
# GPU 서버의 ~/.jupyter/jupyter_server_config.py에 둔다. Jupyter 서버가 시작될 때 이 파일을 실행한다.
#
# 서버의 자동 보안 업데이트(unattended-upgrade) 뒤 needrestart가 우리 Jupyter 서비스를 다시 시작하면,
# 그 안에서 띄운 감시 스크립트·서버·터널이 모두 죽는다(KillMode=control-group). Jupyter 서비스는
# 스스로 다시 뜨므로, 뜰 때 감시 스크립트를 다시 띄우면 사람 손 없이 몇 분 안에 복구된다.
# 이미 떠 있는 감시 스크립트는 건드리지 않는다. 실패해도 Jupyter 시작은 막지 않는다.
import json
import os
import subprocess
import urllib.request
from datetime import datetime
from pathlib import Path

HOME = Path.home()
SUPERVISORS = [
    HOME / "stt" / "gpu_stt" / "supervise.sh",
    HOME / "tts" / "gpu_tts" / "supervise.sh",
    HOME / "guard" / "supervise.sh",
]
AUTOSTART_LOG = HOME / "gpu_autostart.log"
# 이 파일에 Mattermost 수신 웹훅 주소가 있으면 자동으로 다시 띄웠을 때 알린다(선택, 권한 600).
NOTIFY_WEBHOOK_FILE = HOME / ".gpu_autostart_webhook"


def write_log(message):
    with open(AUTOSTART_LOG, "a", encoding="utf-8") as log:
        log.write(f"{datetime.now():%Y-%m-%d %H:%M:%S} {message}\n")


def is_running(script):
    result = subprocess.run(["pgrep", "-u", str(os.getuid()), "-f", str(script)], capture_output=True)
    return result.returncode == 0


def start_supervisor(script):
    log_path = script.with_suffix(".log")
    with open(log_path, "ab") as log:
        subprocess.Popen(
            ["sh", str(script)],
            stdin=subprocess.DEVNULL,
            stdout=log,
            stderr=log,
            start_new_session=True,
        )


def notify(message):
    if not NOTIFY_WEBHOOK_FILE.exists():
        return
    webhook_url = NOTIFY_WEBHOOK_FILE.read_text(encoding="utf-8").strip()
    request = urllib.request.Request(
        webhook_url,
        data=json.dumps({"text": message}).encode("utf-8"),
        headers={"Content-Type": "application/json"},
    )
    urllib.request.urlopen(request, timeout=5).close()


def start_missing_supervisors():
    started = []
    for script in SUPERVISORS:
        if script.exists() and not is_running(script):
            start_supervisor(script)
            started.append(script.parent.name)
    if started:
        write_log(f"Jupyter 시작, 감시 스크립트 띄움: {', '.join(started)}")
        try:
            notify(
                f"GPU 서버 Jupyter가 다시 시작돼 감시 스크립트를 자동으로 띄웠습니다: {', '.join(started)}. "
                "모델을 읽는 몇 분 동안은 API·CPU로 대체됩니다."
            )
        except OSError as error:  # 알림이 실패해도 복구는 이미 끝났다
            write_log(f"알림 실패: {error!r}")


try:
    start_missing_supervisors()
except Exception as error:  # Jupyter 시작을 막지 않도록 기록만 남긴다
    write_log(f"자동 시작 실패: {error!r}")
