# GPU 서버 자동 시작

SSAFY GPU 서버(JupyterHub)에서 도는 STT([`ai/gpu_stt`](../gpu_stt/README.md)), 음성 합성([`ai/gpu_tts`](../gpu_tts/README.md)), 무례 판정 vLLM([`ai/guard`](../guard/README.md))을 Jupyter 서버가 시작될 때 자동으로 띄웁니다. 세 서비스와 배포 서버로 가는 터널은 각 감시 스크립트(`supervise.sh`)가 30초마다 살피고 다시 띄우며, 이 훅은 그 감시 스크립트를 띄웁니다.

## 왜 필요한가

우리 프로세스는 모두 Jupyter 서비스(`jupyter-k15a305.service`) 안에서 돕니다. 이 서비스는 `KillMode=control-group`이라 서비스가 다시 시작되면 그 안의 프로세스가 모두 꺼집니다.

GPU 서버는 매일 아침 자동 보안 업데이트(`unattended-upgrade`)를 돌리고, 라이브러리가 바뀌면 `needrestart`가 그 라이브러리를 쓰는 서비스를 다시 시작합니다. 우리 Jupyter 서비스도 여기에 포함됩니다. 서비스는 스스로 다시 뜨지만 감시 스크립트는 다시 뜨지 않아 STT·합성·판정과 터널이 모두 꺼진 채 남았습니다. 그동안 워커는 STT를 API로, 판정을 프로필 모델로, 합성을 CPU로 대체합니다.

`~/.jupyter/jupyter_server_config.py`는 Jupyter 서버가 시작될 때마다 실행되므로, 여기서 감시 스크립트를 띄우면 서비스가 다시 시작돼도 몇 분 안에 저절로 복구됩니다. 모델을 다시 읽는 데 STT·합성은 1분 안, 판정 vLLM은 약 3분이 걸립니다.

## 설치

GPU 서버 터미널에서 실행합니다. 기존 `~/.jupyter/jupyter_server_config.py`가 있으면 내용을 먼저 확인하고 합칩니다.

```bash
install -m 644 jupyter_server_config.py ~/.jupyter/jupyter_server_config.py
```

- 이미 떠 있는 감시 스크립트는 건드리지 않습니다. 설치 직후에는 아무 일도 하지 않고 다음 Jupyter 시작부터 동작합니다.
- 띄운 기록은 `~/gpu_autostart.log`에 남습니다. 실패해도 Jupyter 시작은 막지 않고 실패 내용을 같은 파일에 적습니다.
- `~/.gpu_autostart_webhook`(권한 600)에 Mattermost 수신 웹훅 주소를 넣으면 자동으로 다시 띄울 때 알림을 보냅니다. 주소는 저장소에 넣지 않습니다.

## 이 훅으로 복구되지 않는 경우

- GPU 서버 재부팅, JupyterHub 관리자의 서버 정지처럼 Jupyter 서버 자체가 다시 뜨지 않는 경우입니다. 누군가 JupyterHub에 로그인하면 Jupyter 서버가 뜨면서 이 훅이 감시 스크립트를 띄웁니다.
- 감시 스크립트가 30분마다 Jupyter 활동을 남겨 24시간 유휴 종료는 막습니다([`ai/gpu_stt`](../gpu_stt/README.md)).
