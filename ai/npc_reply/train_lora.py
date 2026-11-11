"""NPC 대답 LoRA 학습. 게임 대답 경로와 같은 메시지(build_data.py의 messages)에 SOL 대답(target, 표정 표시 포함)을 그대로 내게 학습한다.

    CUDA_DEVICE_ORDER=PCI_BUS_ID CUDA_VISIBLE_DEVICES=5 python ai/npc_reply/train_lora.py \\
        --model <Qwen3.8-27B 폴더> --train train.jsonl --dev dev.jsonl --out runs/npc-r16 --epochs 2

손실은 대답 토큰에만 건다. 에폭마다 개발셋 평균 손실을 찍고 어댑터를 out/epoch-N에 저장한다.
무례 판정 LoRA(ai/guard/train_lora.py)와 같은 기본 모델·같은 LoRA 위치라, 같은 vLLM에 어댑터를 하나 더 얹어 서비스한다.
품질은 손실이 아니라 ai/tools의 npc_help_eval·level_eval·npc_voice_eval(--reply-gpu-model)로 고른다.
"""

import argparse
import json
import random
import time
from pathlib import Path

import torch
from peft import LoraConfig, get_peft_model
from transformers import AutoModelForImageTextToText, AutoTokenizer

# 무례 판정 LoRA와 같은 자리 (vLLM이 LoRA를 받는 묶음)
TARGETS = (
    r"model\.language_model\.layers\.\d+\."
    r"(self_attn\.(q|k|v|o)_proj|mlp\.(gate|up|down)_proj|linear_attn\.(in_proj_qkv|in_proj_z|out_proj))"
)


def read_jsonl(path: str) -> list[dict]:
    return [json.loads(line) for line in Path(path).expanduser().read_text(encoding="utf-8").splitlines() if line.strip()]


def example(tok, item: dict) -> dict:
    # 서비스(vLLM)에서도 생각을 끄고 부른다
    prompt = tok.apply_chat_template(item["messages"], add_generation_prompt=True, enable_thinking=False, tokenize=True, return_dict=False)
    answer = tok.encode(item["target"], add_special_tokens=False) + [tok.convert_tokens_to_ids("<|im_end|>")]
    return {"ids": prompt + answer, "labels": [-100] * len(prompt) + answer}


def answer_loss(logits: torch.Tensor, labels: torch.Tensor) -> torch.Tensor:
    shift_logits, shift_labels = logits[:, :-1].float(), labels[:, 1:]
    return torch.nn.functional.cross_entropy(shift_logits.transpose(1, 2), shift_labels, ignore_index=-100)


@torch.no_grad()
def dev_loss(model, examples: list[dict]) -> float:
    model.eval()
    losses = []
    for e in examples:
        ids = torch.tensor([e["ids"]], device=model.device)
        labels = torch.tensor([e["labels"]], device=model.device)
        losses.append(answer_loss(model(input_ids=ids).logits, labels).item())
    model.train()
    return round(sum(losses) / len(losses), 4)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", required=True)
    ap.add_argument("--train", required=True)
    ap.add_argument("--dev", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--epochs", type=int, default=2)
    ap.add_argument("--lr", type=float, default=1e-4)
    ap.add_argument("--rank", type=int, default=16)
    ap.add_argument("--batch", type=int, default=8, help="갱신 한 번에 쓰는 예시 수 (한 번에 하나씩 넣고 기울기를 모은다)")
    ap.add_argument("--seed", type=int, default=0)
    a = ap.parse_args()

    random.seed(a.seed)
    torch.manual_seed(a.seed)
    out = Path(a.out).expanduser()
    out.mkdir(parents=True, exist_ok=True)
    tok = AutoTokenizer.from_pretrained(a.model)
    model = AutoModelForImageTextToText.from_pretrained(a.model, dtype=torch.bfloat16, device_map={"": 0})
    model.gradient_checkpointing_enable(gradient_checkpointing_kwargs={"use_reentrant": False})
    model.enable_input_require_grads()
    model = get_peft_model(model, LoraConfig(r=a.rank, lora_alpha=2 * a.rank, lora_dropout=0.05, target_modules=TARGETS))
    model.print_trainable_parameters()

    train = [example(tok, it) for it in read_jsonl(a.train)]
    dev = [example(tok, it) for it in read_jsonl(a.dev)]
    optimizer = torch.optim.AdamW([p for p in model.parameters() if p.requires_grad], lr=a.lr, weight_decay=0.0)
    steps = a.epochs * ((len(train) + a.batch - 1) // a.batch)
    scheduler = torch.optim.lr_scheduler.LambdaLR(optimizer, lambda s: min(1.0, (s + 1) / 20) * max(0.0, 1 - s / steps))
    log = (out / "log.jsonl").open("a", encoding="utf-8")
    tokens = sum(len(e["ids"]) for e in train)
    print(
        json.dumps({"train": len(train), "dev": len(dev), "tokens": tokens, "steps": steps, **vars(a)}, ensure_ascii=False),
        file=log,
        flush=True,
    )
    row = {"epoch": 0, "dev_loss": dev_loss(model, dev)}
    print(json.dumps(row), flush=True)
    print(json.dumps(row), file=log, flush=True)

    step, started = 0, time.time()
    for epoch in range(1, a.epochs + 1):
        random.shuffle(train)
        for start in range(0, len(train), a.batch):
            group = train[start : start + a.batch]
            loss_sum = 0.0
            for e in group:
                ids = torch.tensor([e["ids"]], device=model.device)
                labels = torch.tensor([e["labels"]], device=model.device)
                loss = answer_loss(model(input_ids=ids).logits, labels) / len(group)
                loss.backward()
                loss_sum += loss.item()
            torch.nn.utils.clip_grad_norm_(model.parameters(), 1.0)
            optimizer.step()
            scheduler.step()
            optimizer.zero_grad()
            step += 1
            if step % 10 == 0:
                row = {"epoch": epoch, "step": step, "of": steps, "loss": round(loss_sum, 4), "min": round((time.time() - started) / 60, 1)}
                print(json.dumps(row), flush=True)
                print(json.dumps(row), file=log, flush=True)
        model.save_pretrained(out / f"epoch-{epoch}")
        row = {"epoch": epoch, "dev_loss": dev_loss(model, dev), "min": round((time.time() - started) / 60, 1)}
        print(json.dumps(row), flush=True)
        print(json.dumps(row), file=log, flush=True)
    print("TRAIN_DONE", flush=True)


if __name__ == "__main__":
    main()
