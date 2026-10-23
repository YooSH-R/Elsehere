"""가상 샘플 한 건으로 GMS를 한 번 호출하는 수동 검증 도구."""

import argparse
import hashlib
import json
import os
from datetime import datetime, timezone
from pathlib import Path

from feedback.clients import ModelNotConfigured
from feedback.gms import GmsClient, GmsSettings
from feedback.models import INPUT
from feedback.prompt import PROMPT_VERSION, build_messages
from feedback.service import generate_feedback

ROOT = Path(__file__).resolve().parent


def load_settings(env_file):
    allowed = {"GMS_API_KEY", "GMS_KEY", "GMS_BASE_URL", "GMS_MODEL"}
    values = {}
    if env_file.is_file():
        for line in env_file.read_text(encoding="utf-8-sig").splitlines():
            name, separator, value = line.strip().partition("=")
            if separator and name in allowed:
                values[name] = value.strip().strip("\"'")
    values.update({name: os.environ[name] for name in allowed if name in os.environ})
    return GmsSettings(
        api_key=values.get("GMS_API_KEY") or values.get("GMS_KEY", ""),
        base_url=values.get("GMS_BASE_URL", GmsSettings.base_url),
        model=values.get("GMS_MODEL", GmsSettings.model),
    )


class SampleRecorder:
    """수동 검증의 가상 데이터만 저장. 운영 대화 로그로 사용하지 않는다."""

    def __init__(self, client):
        self.client = client
        self.content = None

    def complete(self, messages):
        self.content = self.client.complete(messages)
        return self.content


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--case", default="free_grammar")
    parser.add_argument("--env-file", type=Path, default=ROOT.parents[1] / ".env.local")
    parser.add_argument("--output", type=Path)
    parser.add_argument("--execute-real", action="store_true", help="가상 샘플로 실제 과금 호출 1회")
    args = parser.parse_args()
    cases = json.loads((ROOT / "samples/index.json").read_text(encoding="utf-8"))
    case = next((item for item in cases if item["case_id"] == args.case), None)
    if case is None:
        parser.error("samples/index.json에 있는 case_id 하나를 지정하세요.")
    settings = load_settings(args.env_file)
    payload = json.loads((ROOT / case["input_file"]).read_text(encoding="utf-8"))
    payload["analysis_config_version"] = f"{PROMPT_VERSION}:{settings.model}"
    payload = INPUT.validate_python(payload).model_dump(mode="json")
    messages = build_messages(payload)
    if not args.execute_real:
        print(
            json.dumps(
                {
                    "network_called": False,
                    "key_configured": bool(settings.api_key.strip()),
                    "model": settings.model,
                    "case_id": args.case,
                    "prompt_characters": sum(len(m["content"]) for m in messages),
                },
                ensure_ascii=False,
            )
        )
        return 0
    from run_evaluation import require_local_checks

    require_local_checks()
    client = GmsClient(settings)
    recorder = SampleRecorder(client)
    started_at = datetime.now(timezone.utc).isoformat()
    try:
        response = generate_feedback(payload, recorder, ai_allowed=True)
    except ModelNotConfigured as error:
        parser.error(str(error))
    report = {
        "execution_mode": "real",
        "synthetic_sample": True,
        "case_id": args.case,
        "started_at_utc": started_at,
        "finished_at_utc": datetime.now(timezone.utc).isoformat(),
        "automatic_retries": 0,
        "prompt_version": PROMPT_VERSION,
        "prompt_sha256": hashlib.sha256(json.dumps(messages, ensure_ascii=False).encode()).hexdigest(),
        "call": client.last_call,
        "response": response,
        "raw_model_output": recorder.content,
        "credit_note": "토큰 수와 GMS 크레딧은 다릅니다. 실제 차감량은 GMS 사용량 화면 등에서 별도로 확인합니다.",
        "semantic_review": "pending",
    }
    # 키가 모델 본문 등에 혼입해도 결과 파일에는 남기지 않는다.
    content = json.dumps(report, ensure_ascii=False, indent=2).replace(settings.api_key, "[REDACTED]")
    output = args.output or ROOT / "local_results" / ("gms_" + args.case + "_" + datetime.now().strftime("%Y%m%d_%H%M%S") + ".json")
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(content + "\n", encoding="utf-8")
    summary = json.dumps({"saved": str(output.resolve()), "status": response["status"], "call": client.last_call}, ensure_ascii=False)
    print(summary.replace(settings.api_key, "[REDACTED]"))
    return 0 if response["status"] == "completed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
