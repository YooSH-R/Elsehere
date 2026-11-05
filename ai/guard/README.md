# 무례 판정 모델 학습

AI 워커 `/v1/guard`가 쓰는 무례 판정 모델을 학습하고 평가하는 도구입니다. 서비스와 같은 판정 프롬프트(`ai/worker/app/prompts.py`의 `guard_messages`)에 `yes`/`no` 한 단어로 답하도록 LoRA를 학습하고, vLLM에 어댑터로 올려 서비스합니다. 워커 연결은 [AI API 명세](../../docs/api/ai-worker.md) 2.2.1절(`GUARD_GPU_MODEL`)을 따릅니다.

학습·평가 데이터와 모델 가중치는 저장소에 넣지 않습니다. 팀원 플레이 기록은 외부 LLM(Claude·GMS 등)에 보내지 않고, 라벨은 사람이 답니다.

## 순서

| 단계 | 도구 | 하는 일 |
|---|---|---|
| 1 | `build_train.py` | 합성 문장과 실제 플레이 문장을 학습셋 jsonl로 합칩니다. 실제 플레이는 가이드만 본 에이전트 라벨과 게임 당시 판정이 같으면 그 값, 갈리면 사람의 블라인드 판정을 씁니다. 개발셋·테스트셋과 같은 문장은 뺍니다 |
| 2 | `train_lora.py` | GPU 서버(5번 GPU)에서 LoRA를 학습합니다. 손실은 답 토큰에만 걸고, 에폭마다 개발셋 F1을 찍고 어댑터를 저장합니다 |
| 3 | vLLM | 기본 모델에 어댑터를 얹어 띄웁니다(`--enable-lora --lora-modules <이름>=<어댑터>`) |
| 4 | `ai/tools/guard_eval.py` | 서비스와 같은 요청으로 개발셋을 재고 모델을 고릅니다. 잠긴 테스트셋은 마지막에 한 번만 씁니다 |

jsonl 한 줄은 `text`, `prev`(바로 앞 NPC 대사), `rude`(0/1), `npc`(`id`·`name`·`role`·`facts`·`scene`)이고, 학습셋에는 문장 가중치 `weight`가 더 붙습니다. 서비스는 판정 요청에 앞 대화 여러 줄(학습자·NPC, 최근 4줄)을 넣으므로, 그렇게 재고 학습하려면 `history`(`[{"who": "me"|"npc", "text": ...}]`)를 붙입니다. 있으면 `prev` 대신 씁니다.

## 실행 예

```sh
CUDA_DEVICE_ORDER=PCI_BUS_ID CUDA_VISIBLE_DEVICES=5 python ai/guard/train_lora.py \
    --model <Qwen3.8-27B 폴더> --train train.jsonl --dev dev.jsonl --out runs/lora-r16 --epochs 2 --micro 1
python ai/tools/guard_eval.py --data dev.jsonl --base-url http://<vLLM 주소>/v1 --key-env GUARD_VLLM_KEY \
    --model guard-qwen3.8-27b-r16 --extra-body '{"chat_template_kwargs": {"enable_thinking": false}}' --out result.jsonl
```

- 서비스 중인 판정 vLLM과 같은 5번 GPU를 쓰므로, 학습하는 동안에는 판정 vLLM을 내려야 메모리가 남습니다(27B 가중치만 약 54GB). 그동안 워커는 GPU 판정이 실패해 구성의 판정 모델(GMS)로 넘깁니다.
- `--micro`는 한 번에 GPU에 넣는 문장 수입니다. Qwen3.8의 선형 어텐션은 중간값이 커서 한 번에 여러 문장을 넣으면 메모리가 부족합니다. 기울기를 모아 `--batch`(기본 8) 문장마다 갱신합니다.
- 학습에는 `torch`, `transformers`, `peft`, `flash-linear-attention`이 필요합니다. 자동 테스트와 CI 의존성에는 넣지 않습니다.
- `guard_eval.py`는 화면에 집계만 찍고, 문장별 결과는 문장 없이 `id`·정답·예측만 `--out`에 남깁니다.
