# NPC 대답 모델 학습

게임 NPC 대답(`/v1/npc/reply`)을 SSAFY GPU 서버의 Qwen3.8-27B로 만들 수 있는지 보려고, 서비스와 같은 대답 프롬프트에서 GPT-6.1 SOL이 낸 대답을 정답으로 LoRA를 학습하는 도구입니다. 무례 판정 LoRA([`ai/guard`](../guard/README.md))와 같은 기본 모델·같은 LoRA 위치를 써서, 같은 vLLM에 어댑터를 하나 더 얹는 것을 목표로 합니다. 서비스 연결은 아직 하지 않았습니다(S15P31A305-789).

학습 데이터와 모델 가중치는 저장소에 넣지 않습니다. 학습자 문장과 대답은 모두 모델이 지어낸 것이며 팀원 플레이 기록·AI Hub 데이터는 쓰지 않습니다.

## 순서

| 단계 | 도구 | 하는 일 |
|---|---|---|
| 1 | `build_data.py` | NPC·난이도·학습자 목표를 섞은 대화를 만들고, NPC 턴마다 서비스와 같은 메시지와 SOL 대답(표정 표시 포함)을 남깁니다. 학습자 줄은 GPT-6 Luna가 학습자 흉내로 만듭니다. 형식 문제와 `npc_help_eval` 기준의 지어내기·길 틀림·연락 미룸은 `flags`에 남깁니다 |
| 2 | `make_sets.py` | `flags`가 있는 대답을 빼고 대화 단위로 학습셋·개발셋을 나눕니다 |
| 3 | `train_lora.py` | GPU 서버(5번 GPU)에서 LoRA를 학습합니다. 손실은 대답 토큰에만 겁니다 |
| 4 | `ai/tools/npc_help_eval.py`·`level_eval.py`·`npc_voice_eval.py` | `--reply-gpu-model`로 대답만 학습한 모델로 만들어 SOL과 비교합니다 |

- 평가셋(`npc_help_eval`·`level_eval`·`npc_voice_eval`)의 학습자 문장과 같은 줄은 1단계에서 버립니다.
- 백엔드 지시문(무례 경고·대화 마무리·마지막 턴)은 `build_data.py`에 복사해 두었습니다. 백엔드 문구가 바뀌면 같이 고칩니다.
- 학습에는 `torch`, `transformers`, `peft`, `flash-linear-attention`이 필요하며 CI 의존성에는 넣지 않습니다.
