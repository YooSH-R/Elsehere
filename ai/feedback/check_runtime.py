"""배포 자원 예비 검사. 읽기 전용이며 실제 모델·키·사용자 기록을 사용하지 않는다."""

import argparse
import json
from pathlib import Path

from feedback.runtime_resources import check_runtime_resources


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parent, help="검사할 ai/feedback 폴더")
    args = parser.parse_args(argv)
    report = check_runtime_resources(args.root)
    print(json.dumps(report, ensure_ascii=False))
    return 0 if report["ready"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
