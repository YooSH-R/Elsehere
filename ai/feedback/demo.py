"""GMS 없이 실행하는 고정 응답 데모. python demo.py --help"""
import argparse
import hashlib
import json
from pathlib import Path
import sys

from feedback.clients import FixtureClient
from feedback.prompt import build_messages
from feedback.service import generate_feedback

ROOT = Path(__file__).resolve().parent


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8"))


def canonical_hash(value):
    raw = json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    return hashlib.sha256(raw.encode("utf-8")).hexdigest()


def main():
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description="직접 작성한 대화·응답으로 피드백 처리 흐름을 확인합니다.")
    parser.add_argument("--case", default="tutorial_assisted", help="samples/index.json의 case_id 또는 all")
    parser.add_argument("--mode", choices=("stub", "real"), default="stub")
    parser.add_argument("--show-prompt", action="store_true", help="모델 호출 없이 구성한 메시지만 출력")
    parser.add_argument("--quota-exhausted", action="store_true", help="서버가 새 호출을 거절한 상황 재현")
    parser.add_argument("--output", type=Path, help="실행 결과 JSON 저장 위치")
    args = parser.parse_args()
    cases = read_json(ROOT / "samples" / "index.json")
    selected = cases if args.case == "all" else [c for c in cases if c["case_id"] == args.case]
    if not selected:
        parser.error("없는 case_id입니다. samples/index.json을 확인하세요.")
    if args.mode == "real" and not args.show_prompt:
        parser.error("실제 GMS 호출은 run_gms.py --execute-real로 한 건씩 실행하세요. 이 데모는 고정 응답 전용입니다.")
    records = []
    for case in selected:
        request = read_json(ROOT / case["input_file"])
        if args.show_prompt:
            records.append({"case_id": case["case_id"], "messages": build_messages(request)})
            continue
        if canonical_hash(request) != case["fixture_input_sha256"]:
            parser.error(f"{case['case_id']}: 입력이 변경됐습니다. 고정 응답은 원래 샘플 전용입니다.")
        client = FixtureClient(read_json(ROOT / case["fixture_file"]))
        response = generate_feedback(request, client, ai_allowed=not args.quota_exhausted)
        records.append({
            "case_id": case["case_id"], "execution_mode": "stub",
            "model_called": False, "semantic_quality_verified": False,
            "fixture_client_calls": client.calls, "response": response,
        })
    result = records if args.case == "all" else records[0]
    content = json.dumps(result, ensure_ascii=False, indent=2)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(content + "\n", encoding="utf-8")
        print(f"저장: {args.output.resolve()}")
    else:
        print(content)


if __name__ == "__main__":
    main()
