"""무례 판정 LoRA 학습. 서비스와 같은 판정 프롬프트(워커 guard_messages)에 yes/no 한 단어로 답하게 학습한다.

    CUDA_DEVICE_ORDER=PCI_BUS_ID CUDA_VISIBLE_DEVICES=5 python ai/guard/train_lora.py \\
        --model <Qwen3.8-27B 폴더> --train train.jsonl --dev dev_own_v1_1.jsonl --out runs/lora-r16

학습·개발셋 jsonl 한 줄: text, prev(바로 앞 NPC 대사, 없으면 ""), rude(0/1), npc{id, name, role, facts, scene}, weight(학습셋만, 기본 1).
손실은 답 토큰에만 건다. 에폭마다 개발셋을 첫 답 토큰의 yes·no 점수로 판정해 F1을 찍고, 어댑터를 out/epoch-N에 저장한다.
vLLM에서는 기본 모델에 이 어댑터를 얹어 서비스한다(--enable-lora). 모델 고르기는 개발셋에서만 하고, 잠긴 테스트셋은 최종 평가에 한 번만 쓴다.
"""

import argparse
import json
import random
import sys
import time
from pathlib import Path

import torch
from peft import LoraConfig, get_peft_model
from transformers import AutoModelForImageTextToText, AutoTokenizer

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "ai/worker"))
from app import prompts as P
from app.schemas import Line, Npc

# 언어 모델의 어텐션·MLP·선형 어텐션 입출력에만 붙인다(이미지 처리 부분 제외). vLLM이 LoRA를 받는 묶음과 맞춘다.
TARGETS = (
    r"model\.language_model\.layers\.\d+\."
    r"(self_attn\.(q|k|v|o)_proj|mlp\.(gate|up|down)_proj|linear_attn\.(in_proj_qkv|in_proj_z|out_proj))"
)


def read_jsonl(path: str) -> list[dict]:
    return [json.loads(line) for line in Path(path).expanduser().read_text(encoding="utf-8").splitlines() if line.strip()]


def prompt_ids(tok, item: dict) -> list[int]:
    history = [Line(who="npc", text=item["prev"])] if item["prev"] else []
    messages = P.guard_messages(Npc.model_validate(item["npc"]), history, item["text"])
    # 서비스(vLLM)에서도 생각을 끄고 부른다
    return tok.apply_chat_template(messages, add_generation_prompt=True, enable_thinking=False, tokenize=True, return_dict=False)


def example(tok, item: dict) -> dict:
    prompt = prompt_ids(tok, item)
    answer = tok.encode("yes" if item["rude"] else "no", add_special_tokens=False) + [tok.convert_tokens_to_ids("<|im_end|>")]
    return {"ids": prompt + answer, "labels": [-100] * len(prompt) + answer, "weight": float(item.get("weight", 1.0))}


def batches(examples: list[dict], size: int, pad_id: int):
    for i in range(0, len(examples), size):
        chunk = examples[i : i + size]
        width = max(len(e["ids"]) for e in chunk)
        ids = torch.full((len(chunk), width), pad_id)
        labels = torch.full((len(chunk), width), -100)
        mask = torch.zeros((len(chunk), width), dtype=torch.long)
        for row, e in enumerate(chunk):
            n = len(e["ids"])
            ids[row, :n] = torch.tensor(e["ids"])
            labels[row, :n] = torch.tensor(e["labels"])
            mask[row, :n] = 1
        yield ids, labels, mask, torch.tensor([e["weight"] for e in chunk])


def weighted_loss(logits: torch.Tensor, labels: torch.Tensor, weights: torch.Tensor, total_weight: float) -> torch.Tensor:
    """답 토큰의 교차 엔트로피에 문장 가중치를 곱해 갱신 한 번 분량(total_weight)으로 나눈다. 나눠 넣은 배치를 더하면 가중 평균이 된다."""
    shift_logits, shift_labels = logits[:, :-1].float(), labels[:, 1:]
    per_token = torch.nn.functional.cross_entropy(shift_logits.transpose(1, 2), shift_labels, ignore_index=-100, reduction="none")
    counted = (shift_labels != -100).float()
    per_row = (per_token * counted).sum(1) / counted.sum(1).clamp(min=1)
    return (per_row * weights.to(per_row.device)).sum() / total_weight


@torch.no_grad()
def evaluate(model, tok, items: list[dict], yes_id: int, no_id: int) -> dict:
    """첫 답 토큰에서 yes 점수가 no보다 크면 무례로 본다(서비스의 greedy 첫 토큰과 같은 판정)."""
    model.eval()
    tp = fp = fn = 0
    for item in items:
        ids = torch.tensor([prompt_ids(tok, item)], device=model.device)
        logits = model(input_ids=ids).logits[0, -1]
        pred = int(logits[yes_id] > logits[no_id])
        tp += pred and item["rude"]
        fp += pred and not item["rude"]
        fn += (not pred) and item["rude"]
    model.train()
    precision = tp / (tp + fp) if tp + fp else 0.0
    recall = tp / (tp + fn) if tp + fn else 0.0
    f1 = 2 * precision * recall / (precision + recall) if precision + recall else 0.0
    return {"f1": round(f1, 4), "precision": round(precision, 4), "recall": round(recall, 4), "tp": tp, "fp": fp, "fn": fn}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", required=True)
    ap.add_argument("--train", required=True)
    ap.add_argument("--dev", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--epochs", type=int, default=2)
    ap.add_argument("--lr", type=float, default=1e-4)
    ap.add_argument("--rank", type=int, default=16)
    ap.add_argument("--batch", type=int, default=8, help="갱신 한 번에 쓰는 문장 수")
    ap.add_argument("--micro", type=int, default=1, help="한 번에 GPU에 넣는 문장 수. 선형 어텐션 중간값이 커서 작게 나눠 기울기를 모은다")
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

    yes_id, no_id = (tok.encode(w, add_special_tokens=False)[0] for w in ("yes", "no"))
    train = [example(tok, it) for it in read_jsonl(a.train)]
    dev = read_jsonl(a.dev)
    optimizer = torch.optim.AdamW([p for p in model.parameters() if p.requires_grad], lr=a.lr, weight_decay=0.0)
    steps = a.epochs * ((len(train) + a.batch - 1) // a.batch)
    scheduler = torch.optim.lr_scheduler.LambdaLR(optimizer, lambda s: min(1.0, (s + 1) / 20) * max(0.0, 1 - s / steps))
    log = (out / "log.jsonl").open("a", encoding="utf-8")
    print(json.dumps({"train": len(train), "dev": len(dev), "steps": steps, **vars(a)}, ensure_ascii=False), file=log, flush=True)

    step, started = 0, time.time()
    for epoch in range(1, a.epochs + 1):
        random.shuffle(train)
        for start in range(0, len(train), a.batch):
            group = train[start : start + a.batch]
            total_weight = sum(e["weight"] for e in group)
            loss_sum = 0.0
            for ids, labels, mask, weights in batches(group, a.micro, tok.pad_token_id):
                logits = model(input_ids=ids.to(model.device), attention_mask=mask.to(model.device)).logits
                loss = weighted_loss(logits, labels.to(model.device), weights, total_weight)
                loss.backward()
                loss_sum += loss.item()
            torch.nn.utils.clip_grad_norm_(model.parameters(), 1.0)
            optimizer.step()
            scheduler.step()
            optimizer.zero_grad()
            step += 1
            if step % 10 == 0:
                row = {
                    "epoch": epoch,
                    "step": step,
                    "of": steps,
                    "loss": round(loss_sum, 4),
                    "min": round((time.time() - started) / 60, 1),
                }
                print(json.dumps(row), flush=True)
                print(json.dumps(row), file=log, flush=True)
        scores = evaluate(model, tok, dev, yes_id, no_id)
        model.save_pretrained(out / f"epoch-{epoch}")
        row = {"epoch": epoch, "dev": scores, "min": round((time.time() - started) / 60, 1)}
        print(json.dumps(row), flush=True)
        print(json.dumps(row), file=log, flush=True)
    print("TRAIN_DONE", flush=True)


if __name__ == "__main__":
    main()
