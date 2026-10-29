"""기억 합성 데이터셋의 고정 응답 검사. 키 로딩·모델 호출 기능 없음."""

import argparse
import json
import sys
from pathlib import Path

from feedback.memory_dataset import DatasetError, check_cases, load_cases


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dataset", type=Path, default=Path(__file__).parent / "samples" / "memory_regression" / "cases")
    parser.add_argument("--case", help="검사할 사례 ID. 생략하면 전체 고정 응답을 검사한다.")
    parser.add_argument("--output", type=Path, help="새 결과 JSON 경로. 기존 파일은 덮어쓰지 않는다.")
    args = parser.parse_args(argv)
    try:
        cases = load_cases(args.dataset)
        if args.case:
            cases = [case for case in cases if case["case_id"] == args.case]
            if not cases:
                raise DatasetError("UNKNOWN_CASE")
        report = check_cases(cases)
        content = json.dumps(report, ensure_ascii=False, indent=2) + "\n"
        if args.output:
            with args.output.open("x", encoding="utf-8", newline="\n") as output:
                output.write(content)
    except (DatasetError, OSError) as error:
        message = str(error) if isinstance(error, DatasetError) else type(error).__name__
        print(f"검사 실패: {message}", file=sys.stderr)
        return 2
    print(content, end="")
    return 1 if report["failed"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
