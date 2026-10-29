"""로컬 저장 기억 응답만 점검한다. 키 로딩·모델 호출·다운로드 기능 없음."""

import argparse
import json
import sys
from pathlib import Path

from feedback.memory_dataset import DatasetError, load_cases
from feedback.memory_review import review_directory


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dataset", type=Path, default=Path(__file__).parent / "samples" / "memory_regression" / "cases")
    parser.add_argument("--responses-dir", type=Path, required=True, help="G01.json 등 저장된 사실별 계약 응답 폴더")
    parser.add_argument("--origin", choices=["saved_model_response", "synthetic_demo"], required=True)
    parser.add_argument("--model", help="저장된 모델 응답의 모델명. 출처를 인증하는 값은 아님")
    parser.add_argument("--output", type=Path, help="새 보고서 JSON. 기존 파일은 덮어쓰지 않음")
    args = parser.parse_args(argv)
    try:
        report = review_directory(load_cases(args.dataset), args.responses_dir, origin=args.origin, model=args.model)
        content = json.dumps(report, ensure_ascii=False, indent=2) + "\n"
        if args.output:
            with args.output.open("x", encoding="utf-8", newline="\n") as output:
                output.write(content)
    except (DatasetError, OSError) as error:
        message = str(error) if isinstance(error, DatasetError) else type(error).__name__
        print(f"점검 실패: {message}", file=sys.stderr)
        return 2
    print(content, end="")
    return 1 if any(report["counts"][status] for status in ("flagged", "rejected", "not_run")) else 0


if __name__ == "__main__":
    raise SystemExit(main())
