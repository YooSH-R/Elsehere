"""형식을 변경하면 스키마를 함께 다시 생성한다."""

import json
from pathlib import Path

from feedback.models import INPUT, LLM_OUTPUT, OUTPUT

root = Path(__file__).resolve().parent / "schemas"
root.mkdir(exist_ok=True)
for kind, adapter in (("input", INPUT), ("output", OUTPUT), ("llm-output", LLM_OUTPUT)):
    (root / f"feedback.{kind}.schema.json").write_text(
        json.dumps(adapter.json_schema(), ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
print("Exported 3 standalone feedback schemas.")
