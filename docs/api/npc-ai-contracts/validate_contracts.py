#!/usr/bin/env python3
"""NPC 계약 파일의 로컬 검증. 실제 HTTP/DB/LLM/TTS를 호출하지 않는다.

설치: python -m pip install -r requirements.txt
실행: python validate_contracts.py
현재 디렉터리와 무관하게 스크립트 옆의 파일을 읽는다.
"""
from __future__ import annotations

import copy
import json
import re
import sys
from pathlib import Path
from typing import Any, Callable
from urllib.parse import urldefrag, urljoin

try:
    import yaml
    from jsonschema import Draft202012Validator, ValidationError
    from referencing import Registry, Resource
    from referencing.jsonschema import DRAFT202012
except ImportError as exc:
    raise SystemExit("필요 패키지를 설치하세요: python -m pip install -r requirements.txt") from exc

BASE = Path(__file__).resolve().parent
MAX_BODY_BYTES = 131072
SHARED_PATH = BASE / "npc_shared_schemas_v2.json"
API_PATH = BASE / "npc_inference_openapi_v2.yaml"
SHARED_URI, API_URI = SHARED_PATH.as_uri(), API_PATH.as_uri()
SHARED: dict[str, Any] = json.loads(SHARED_PATH.read_text(encoding="utf-8"))
API: dict[str, Any] = yaml.safe_load(API_PATH.read_text(encoding="utf-8"))
CONFIGS: list[dict[str, Any]] = json.loads((BASE / "npc_configuration_examples_v2.json").read_text(encoding="utf-8"))
EXAMPLES: dict[str, Any] = json.loads((BASE / "npc_inference_examples_v2.json").read_text(encoding="utf-8"))
DOCUMENTS = {SHARED_URI: SHARED, API_URI: API}
REGISTRY = Registry().with_resources([
    (uri, Resource.from_contents(contents, default_specification=DRAFT202012))
    for uri, contents in DOCUMENTS.items()
])
RESULTS: list[dict[str, Any]] = []


class ContractViolation(ValueError):
    """스키마만으로 표현하지 않은 애플리케이션 계약 위반."""


def validator(name: str, *, shared: bool = False) -> Draft202012Validator:
    uri = SHARED_URI + "#/$defs/" + name if shared else API_URI + "#/components/schemas/" + name
    return Draft202012Validator({"$ref": uri}, registry=REGISTRY)


def check_schema_value(name: str, payload: Any, *, shared: bool = False) -> None:
    validator(name, shared=shared).validate(payload)


def validate_configurations(payload: Any) -> None:
    check_schema_value("NpcConfigurationList", payload, shared=True)
    identifiers = [c["npcId"] for c in payload]
    if len(identifiers) != len(set(identifiers)):
        raise ContractViolation("같은 npcId가 한 배치에서 중복됩니다.")
    # DB의 장소/목소리 존재 및 검토 여부는 이 오프라인 검증기에서 확인할 수 없다.


def validate_request(payload: Any, *, raw_body: bytes | None = None) -> None:
    # 실제 서버에서는 수신 원문 바이트 크기를 검사해야 한다. 여기서는 미지정 시 JSON 직렬화로 계산한다.
    body = raw_body if raw_body is not None else json.dumps(payload, ensure_ascii=False).encode("utf-8")
    if len(body) > MAX_BODY_BYTES:
        raise ContractViolation("요청 본문 128 KiB 상한을 초과했습니다.")
    check_schema_value("NpcRespondRequest", payload)
    if payload["turnType"] == "USER_MESSAGE" and payload["conversationHistory"][-1]["speaker"] != "NPC":
        raise ContractViolation("후속 요청의 마지막 이력 화자는 NPC여야 합니다.")


def validate_response(payload: Any, request: dict[str, Any]) -> None:
    check_schema_value("NpcRespondResponse", payload)
    for key in ("requestId", "sessionId", "npcId"):
        if payload[key] != request[key]:
            raise ContractViolation(f"요청·응답의 {key}가 다릅니다.")
    if request["turnType"] == "NPC_OPENING" and payload["conversationControl"] != {"action": "CONTINUE", "reason": None}:
        raise ContractViolation("첫 대사는 CONTINUE/null이어야 합니다.")


ERROR_RULES: dict[str, tuple[int, set[bool]]] = {
    "INVALID_JSON": (400, {False}),
    "UNAUTHORIZED": (401, {False}),
    "PAYLOAD_TOO_LARGE": (413, {False}),
    "UNSUPPORTED_MEDIA_TYPE": (415, {False}),
    "INVALID_REQUEST": (422, {False}),
    "AI_RATE_LIMITED": (429, {True}),
    "AI_PROVIDER_ERROR": (502, {True, False}),
    "INVALID_AI_OUTPUT": (502, {True}),
    "AI_UNAVAILABLE": (503, {True}),
    "AI_TIMEOUT": (504, {True}),
    "INTERNAL_ERROR": (500, {False}),
}


def validate_error(payload: Any, status: int) -> None:
    check_schema_value("ErrorResponse", payload)
    expected_status, retry_values = ERROR_RULES[payload["error"]["code"]]
    if status != expected_status or payload["error"]["retryable"] not in retry_values:
        raise ContractViolation("HTTP·오류 코드·retryable 조합이 명세와 다릅니다.")


def check(label: str, action: Callable[[], None], *, reject: bool = False) -> None:
    """예상된 검증 거부만 정상적인 오류 입력 테스트 결과로 취급한다."""
    detail = ""
    try:
        action()
        passed = not reject
        if reject:
            detail = "거부되어야 할 입력을 허용했습니다."
    except (ValidationError, ContractViolation) as exc:
        passed = reject
        if not reject:
            detail = str(exc)
    except Exception as exc:
        passed = False
        detail = f"예기치 않은 오류: {type(exc).__name__}: {exc}"
    RESULTS.append({"test": label, "expected": "reject" if reject else "accept", "passed": passed, "detail": detail})


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def altered(base: Any, path: list[str | int], value: Any, *, delete: bool = False) -> Any:
    result = copy.deepcopy(base)
    node = result
    for key in path[:-1]:
        node = node[key]
    if delete:
        del node[path[-1]]
    else:
        node[path[-1]] = value
    return result


def walk_refs(node: Any):
    if isinstance(node, dict):
        for key, value in node.items():
            if key == "$ref":
                yield value
            else:
                yield from walk_refs(value)
    elif isinstance(node, list):
        for value in node:
            yield from walk_refs(value)


def resolve_local_ref(base_uri: str, ref: str) -> Any:
    doc_uri, fragment = urldefrag(urljoin(base_uri, ref))
    if doc_uri not in DOCUMENTS:
        raise AssertionError(f"패키지 밖/알 수 없는 참조: {ref}")
    node = DOCUMENTS[doc_uri]
    if fragment:
        for token in fragment.lstrip("/").split("/"):
            token = token.replace("~1", "/").replace("~0", "~")
            node = node[int(token)] if isinstance(node, list) else node[token]
    return node


def run_checks() -> None:
    check("공통 스키마 문서 문법", lambda: Draft202012Validator.check_schema(SHARED))
    for name, definition in SHARED["$defs"].items():
        check(f"공통 정의 문법: {name}", lambda d=definition: Draft202012Validator.check_schema(d))
    for name, definition in API["components"]["schemas"].items():
        check(f"OpenAPI 내부 스키마 문법: {name}", lambda d=definition: Draft202012Validator.check_schema(d))
    for uri, document in DOCUMENTS.items():
        for ref in sorted(set(walk_refs(document))):
            check(f"참조 연결: {Path(urldefrag(uri)[0]).name} → {ref}", lambda u=uri, r=ref: resolve_local_ref(u, r))
    check("추론 경로는 하나", lambda: require(list(API["paths"]) == ["/internal/ai/npc/respond"], "경로 변경"))
    check("프로필 스키마 단일 참조", lambda: require(API["components"]["schemas"]["NpcProfile"] == {"$ref": "./npc_shared_schemas_v2.json#/$defs/NpcProfile"}, "프로필이 별도 복제됨"))
    check("제공 스키마에 설정 HTTP 경로 없음", lambda: require(all("config" not in p for p in API["paths"]), "불필요한 설정 API"))
    check("설정 배치 3명 정상", lambda: validate_configurations(CONFIGS))
    for c in CONFIGS:
        check(f"설정 정상: {c['npcId']}", lambda x=c: check_schema_value("NpcConfiguration", x, shared=True))
        check(f"프로필 정상: {c['npcId']}", lambda x=c["npcProfile"]: check_schema_value("NpcProfile", x, shared=True))
    active = copy.deepcopy(CONFIGS[0]); active.update(isActive=True, voiceId="voice_example_001")
    check("목소리 ID가 있는 활성 설정의 형식", lambda: check_schema_value("NpcConfiguration", active, shared=True))
    minimal = {k: copy.deepcopy(CONFIGS[0]["npcProfile"][k]) for k in SHARED["$defs"]["NpcProfile"]["required"]}
    minimal["personality"].pop("dimensions", None)
    for k in ["humor", "directness", "catchphrases"]:
        minimal["speechStyle"].pop(k, None)
    check("선택 정보 없는 최소 프로필", lambda: check_schema_value("NpcProfile", minimal, shared=True))
    dimensions = {k: 0.5 for k in SHARED["$defs"]["PersonalityDimensions"]["required"]}
    rich = altered(minimal, ["personality", "dimensions"], dimensions)
    check("선택 수치 5개 정상", lambda: check_schema_value("NpcProfile", rich, shared=True))
    nullable = copy.deepcopy(minimal); nullable.update(ageRange=None, socialBehavior=None, background=None)
    check("선택 객체/문자열 null 정상", lambda: check_schema_value("NpcProfile", nullable, shared=True))
    for key, request in EXAMPLES["requests"].items():
        check(f"요청 정상: {key}", lambda r=request: validate_request(r))
        check(f"응답 교차 검증 정상: {key}", lambda r=request, s=EXAMPLES["responses"][key]: validate_response(s, r))
        check(f"예시 프로필 동일성: {key}", lambda r=request: require(r["npcProfile"] == CONFIGS[0]["npcProfile"], "설정과 요청 예시 프로필이 다름"))
    for c in CONFIGS[1:]:
        req = copy.deepcopy(EXAMPLES["requests"]["opening"])
        req.update(npcId=c["npcId"], npcProfile=c["npcProfile"])
        req["scenario"].update(scenarioId=None, locationId=c["defaultLocationId"], situation="현재 장소에서 사용자가 대화를 시작하기로 했다.", facts=[])
        check(f"다른 NPC도 같은 요청 계약: {c['npcId']}", lambda r=req: validate_request(r))
    for code, (status, options) in ERROR_RULES.items():
        for retry in sorted(options):
            error = {"requestId": None, "error": {"code": code, "message": "검증용 오류 예시", "retryable": retry}}
            check(f"오류 매핑 정상: {code}/{retry}", lambda e=error, s=status: validate_error(e, s))

    profile = CONFIGS[0]["npcProfile"]
    profile_invalid = [
        ("구조 버전 누락", ["schemaVersion"], None, True),
        ("구조 버전 불일치", ["schemaVersion"], "1.2", False),
        ("내용 버전 0", ["profileVersion"], 0, False),
        ("공백 이름", ["name"], "   ", False),
        ("없는 MBTI 필드", ["mbti"], "ENFP", False),
        ("프로필에 음성 ID 혼입", ["voiceId"], "voice_001", False),
        ("구형 최상위 traits 혼입", ["traits"], ["friendly"], False),
        ("성격 특성 빈 목록", ["personality", "traits"], [], False),
        ("성격 특성 문자열 목록", ["personality", "traits"], ["friendly"], False),
        ("성격 특성 설명 누락", ["personality", "traits", 0, "description"], None, True),
        ("불완전 수치 객체", ["personality", "dimensions"], {"openness": 0.5}, False),
        ("수치 상한 초과", ["personality", "dimensions"], {**dimensions, "openness": 1.1}, False),
        ("수치 하한 미달", ["personality", "dimensions"], {**dimensions, "openness": -0.1}, False),
        ("수치가 문자열", ["personality", "dimensions"], {**dimensions, "openness": "0.5"}, False),
        ("가치관 완전 중복", ["values"], ["친절", "친절"], False),
        ("사회적 반응 부분 객체", ["socialBehavior"], {"firstMeeting": "친근하게 인사"}, False),
        ("말투 enum 오류", ["speechStyle", "formality"], "polite", False),
        ("직접성 enum 오류", ["speechStyle", "directness"], "medium", False),
        ("배경 구형 문자열", ["background"], "기존 배경 문자열", False),
        ("경험 영향 누락", ["background", "experiences", 0, "influence"], None, True),
        ("습관 경향 누락", ["habits", 0, "tendency"], None, True),
        ("선택 배열에 null", ["fears"], None, False),
    ]
    for label, path, value, delete in profile_invalid:
        payload = altered(profile, path, value, delete=delete)
        check(f"프로필 거부: {label}", lambda p=payload: check_schema_value("NpcProfile", p, shared=True), reject=True)
    config_invalid = [
        ("음성 없는 활성화", ["isActive"], True, False),
        ("장소 누락", ["defaultLocationId"], None, True),
        ("ID 공백", ["npcId"], "  ", False),
        ("개별 사용자 기억 혼입", ["userMemories"], [], False),
    ]
    for label, path, value, delete in config_invalid:
        payload = altered(CONFIGS[0], path, value, delete=delete)
        check(f"설정 거부: {label}", lambda p=payload: check_schema_value("NpcConfiguration", p, shared=True), reject=True)
    dup = copy.deepcopy(CONFIGS); dup.append(copy.deepcopy(CONFIGS[0])); dup[-1]["npcProfile"]["name"] = "다른 이름"
    check("배치 거부: 내용이 달라도 동일 NPC ID 중복", lambda: validate_configurations(dup), reject=True)

    opening, follow = EXAMPLES["requests"]["opening"], EXAMPLES["requests"]["followup"]
    request_invalid = [
        ("첫 대사에 사용자 텍스트", opening, ["userText"], "Hello", False),
        ("첫 대사에 이력", opening, ["conversationHistory"], [{"speaker": "NPC", "text": "Hi"}], False),
        ("후속에 null 발화", follow, ["userText"], None, False),
        ("후속에 공백 발화", follow, ["userText"], "\n ", False),
        ("후속에 빈 이력", follow, ["conversationHistory"], [], False),
        ("후속 마지막 화자 USER", follow, ["conversationHistory", 0, "speaker"], "USER", False),
        ("음성 필드 추가", follow, ["audioUrl"], "/not-used.mp3", False),
        ("학습 수준 enum 오류", follow, ["userLevel"], "beginner", False),
        ("프로필 대신 전체 설정", follow, ["npcProfile"], CONFIGS[0], False),
        ("사용자 발화 2001자", follow, ["userText"], "x" * 2001, False),
        ("이력 21개", follow, ["conversationHistory"], [{"speaker":"NPC", "text":"Hi"}] * 21, False),
        ("사실 31개", follow, ["scenario", "facts"], [f"fact{i}" for i in range(31)], False),
        ("상황 2001자", follow, ["scenario", "situation"], "x"*2001, False),
        ("필수 requestId 누락", follow, ["requestId"], None, True),
    ]
    for label, base, path, value, delete in request_invalid:
        payload = altered(base, path, value, delete=delete)
        check(f"요청 거부: {label}", lambda p=payload: validate_request(p), reject=True)
    oversized_body = json.dumps(opening, ensure_ascii=False).encode("utf-8") + b" " * MAX_BODY_BYTES
    check("요청 거부: 수신 원문 바이트 상한", lambda: validate_request(opening, raw_body=oversized_body), reject=True)
    successful = EXAMPLES["responses"]["followup"]
    for key in ("requestId", "sessionId", "npcId"):
        payload = altered(successful, [key], "mismatch")
        check(f"응답 거부: 요청 ID 불일치 {key}", lambda p=payload: validate_response(p, follow), reject=True)
    response_invalid = [
        ("대사 공백", ["replyText"], " "),
        ("대사 길이 상한", ["replyText"], "x"*2001),
        ("CONTINUE에 종료 사유", ["conversationControl"], {"action":"CONTINUE", "reason":"USER_END_INTENT"}),
        ("OFFER_END에 사유 없음", ["conversationControl"], {"action":"OFFER_END", "reason":None}),
        ("새 행동 명령 필드", ["animation"], "wave"),
    ]
    for label, path, value in response_invalid:
        payload = altered(successful, path, value)
        check(f"응답 거부: {label}", lambda p=payload: validate_response(p, follow), reject=True)
    end_opening = copy.deepcopy(EXAMPLES["responses"]["opening"])
    end_opening["conversationControl"] = {"action":"OFFER_END", "reason":"NATURAL_WRAP_UP"}
    check("응답 거부: 첫 대사에 마무리 신호", lambda: validate_response(end_opening, opening), reject=True)
    invalid_error = altered(EXAMPLES["error"], ["error", "retryable"], False)
    check("오류 거부: TIMEOUT retryable 불일치", lambda: validate_error(invalid_error, 504), reject=True)
    check("오류 거부: TIMEOUT HTTP 불일치", lambda: validate_error(EXAMPLES["error"], 500), reject=True)

    operation = API["paths"]["/internal/ai/npc/respond"]["post"]
    for key, e in operation["requestBody"]["content"]["application/json"]["examples"].items():
        check(f"OpenAPI 요청 예제: {key}", lambda v=e["value"]: validate_request(v))
    for key, e in operation["responses"]["200"]["content"]["application/json"]["examples"].items():
        check(f"OpenAPI 응답 예제: {key}", lambda k=key, v=e["value"]: validate_response(v, EXAMPLES["requests"][k]))
    for status, response in operation["responses"].items():
        if status == "200": continue
        media = response["content"]["application/json"]
        if "example" in media:
            check(f"OpenAPI 오류 예제: {status}", lambda v=media["example"], s=int(status): validate_error(v, s))
        for key, value in media.get("examples", {}).items():
            check(f"OpenAPI 오류 예제: {status}/{key}", lambda v=value["value"], s=int(status): validate_error(v, s))
    for name in ("01_npc_configuration_spec_v2.md", "02_npc_inference_api_spec_v2.md", "README.md"):
        doc = (BASE/name).read_text(encoding="utf-8")
        check(f"문서 미치환 표시 없음: {name}", lambda d=doc: require("@@" not in d, "미치환 템플릿"))
        for i, block in enumerate(re.findall(r"```json\s*\n(.*?)\n```", doc, flags=re.S), 1):
            check(f"문서 JSON 문법: {name}/{i}", lambda b=block: json.loads(b))
        for target in sorted(set(re.findall(r"\]\(([^)]+)\)", doc))):
            if "://" in target or target.startswith("#"): continue
            path = target.split("#", 1)[0]
            check(f"문서 링크: {name} → {path}", lambda p=path: require((BASE/p).is_file(), f"없는 파일: {p}"))


def write_report() -> None:
    failed = [r for r in RESULTS if not r["passed"]]
    data = {
        "scope": "로컬 파일·JSON Schema·참조·예시·계약 규칙 검증. HTTP/DB/LLM/TTS 통합 테스트가 아님.",
        "passed": len(RESULTS)-len(failed), "failed": len(failed), "total": len(RESULTS), "results": RESULTS,
    }
    (BASE/"validation_results.json").write_text(json.dumps(data, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
    lines = ["# 로컬 계약 검증 결과", "", f"- 통과: **{data['passed']} / {data['total']}**", f"- 실패: **{data['failed']}**", "", data["scope"], "", "## 포함한 검사", "", "공통 정의와 OpenAPI 내부 JSON 스키마 문법, 내부/외부 참조 연결, 설정·요청·응답·오류 예시, 선택값 생략, 구형 프로필 거부, 버전·길이·enum·중복 ID·대화 유형 조건, 요청–응답 식별자, 문서 JSON 예제와 파일 링크를 검사했다.", "", "## 포함하지 않은 검사", "", "OpenAPI 전체 문서에 대한 별도 OAS 전용 검증기 인증, 실제 서버의 HTTP 상태·인증·취소 처리, DB 트랜잭션·참조 존재·프로필 스냅샷, 모델 응답 품질·인격 다양성, TTS 생성·재생, 실측 지연·비용은 검사하지 않았다. 선택한 스키마/예제 검사 통과를 서버 구현 완료로 해석하지 않는다.", "", "## 재실행", "", "```bash", "python -m pip install -r requirements.txt", "python validate_contracts.py", "```", "", "상세 사례는 `validation_results.json`을 참조한다. 검사 수에는 스키마·참조·문서 링크 검사도 포함된다.", ""]
    if failed:
        lines += ["## 실패 내역", ""]
        lines += [f"- {r['test']}: {r['detail']}" for r in failed]
    (BASE/"validation_report.md").write_text("\n".join(lines), encoding="utf-8")
    print(f"검증 결과: {data['passed']}/{data['total']} 통과, 실패 {data['failed']}")
    for r in failed:
        print(f"FAIL {r['test']}: {r['detail']}", file=sys.stderr)
    if failed:
        raise SystemExit(1)


if __name__ == "__main__":
    run_checks()
    write_report()
