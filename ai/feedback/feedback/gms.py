"""SSAFY GMS Chat Completions 어댑터. 자동 재시도와 대체 모델 호출 없음."""
from dataclasses import dataclass, field
from copy import deepcopy
import json
import time
from urllib.parse import urlsplit
from urllib.request import Request, build_opener, HTTPRedirectHandler
from urllib.error import HTTPError, URLError

from feedback.clients import ModelNotConfigured, ProviderCallError
from feedback.models import LLM_OUTPUT


def feedback_response_format(messages):
    # 공급자는 루트 union을 허용하지 않는다. 상태별 의미 제약은 기존 검증기가 유지한다.
    definitions = LLM_OUTPUT.json_schema()["$defs"]
    definitions.pop("LlmUsable")
    definitions.pop("LlmInsufficient")
    fields = definitions["FeedbackData"]["properties"]
    # 외부 v0.3 계약은 유지하고 이번 생성의 선택적 출력만 제한한다.
    fields["review_tags"]["maxItems"] = 2
    fields["vocab_candidates"]["maxItems"] = 2
    definitions["TurnReview"]["properties"]["suggestions"]["maxItems"] = 1
    candidates = []
    try:
        request_data = json.loads(messages[-1]["content"])
        user_ids = request_data.get("required_review_turn_ids", [])
        candidates = request_data.get("spoken_usage_candidates", [])
    except (IndexError, KeyError, TypeError, ValueError, AttributeError):
        user_ids = []
    if user_ids and all(isinstance(turn_id, str) for turn_id in user_ids):
        fields["turn_reviews"]["minItems"] = len(user_ids)
        fields["turn_reviews"]["maxItems"] = len(user_ids)
        user_evidence = deepcopy(definitions["Evidence"])
        user_evidence["properties"]["turn_id"]["enum"] = user_ids
        definitions["UserEvidence"] = user_evidence
        for name in ("Communication", "ReviewTag"):
            definitions[name]["properties"]["evidence"]["items"] = {"$ref": "#/$defs/UserEvidence"}
        definitions["Suggestion"]["properties"]["source"] = {"$ref": "#/$defs/UserEvidence"}
        definitions["TurnReview"]["properties"]["turn_id"]["enum"] = user_ids
        definitions["UserTurnSource"]["properties"]["turn_id"]["enum"] = user_ids
    if candidates:
        selection = definitions["SpokenUsageSelection"]["properties"]
        selection["catalog_id"]["enum"] = list(dict.fromkeys(item["catalog_id"] for item in candidates))
        selection["turn_id"]["enum"] = list(dict.fromkeys(item["source"]["turn_id"] for item in candidates))
        selection_schema = {"anyOf": [{"$ref": "#/$defs/SpokenUsageSelection"}, {"type": "null"}]}
    else:
        definitions.pop("SpokenUsageSelection")
        selection_schema = {"type": "null"}
    schema = {
        "type": "object", "additionalProperties": False,
        "properties": {
            "outcome": {"type": "string", "enum": ["usable", "insufficient_evidence"]},
            "result": {"anyOf": [{"$ref": "#/$defs/FeedbackData"}, {"type": "null"}]},
            "problem": {"anyOf": [{"$ref": "#/$defs/DataIssue"}, {"type": "null"}]},
            "spoken_usage_selection": selection_schema,
        },
        "required": ["outcome", "result", "problem", "spoken_usage_selection"], "$defs": definitions,
    }

    def normalize(node):
        if isinstance(node, list):
            return [normalize(item) for item in node]
        if not isinstance(node, dict):
            return node
        result = {}
        for key, value in node.items():
            if key in ("discriminator", "title", "default"):
                continue
            result["anyOf" if key == "oneOf" else key] = normalize(value)
        return result

    return {"type": "json_schema", "json_schema": {
        "name": "conversation_feedback", "strict": True, "schema": normalize(schema),
    }}


class _NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        # 인증 헤더가 다른 목적지로 전달되거나 요청이 반복되는 것을 막는다.
        return None


@dataclass
class GmsSettings:
    api_key: str = field(repr=False)
    base_url: str = "https://gms.ssafy.io/gmsapi/api.openai.com/v1"
    model: str = "gpt-5.4"
    timeout_s: float = 60.0
    max_output_tokens: int = 6000


class GmsClient:
    def __init__(self, settings, opener=None):
        self.settings = settings
        self.opener = opener
        self.last_call = None

    def complete(self, messages):
        self.last_call = None
        endpoint = urlsplit(self.settings.base_url)
        if (not self.settings.api_key.strip() or endpoint.scheme != "https"
                or endpoint.netloc != "gms.ssafy.io" or endpoint.query or endpoint.fragment):
            raise ModelNotConfigured("로컬 GMS 키 또는 HTTPS GMS 주소 설정을 확인하세요.")
        body = {
            "model": self.settings.model, "messages": messages,
            "response_format": feedback_response_format(messages), "stream": False,
        }
        if self.settings.model == "gpt-5.4":
            # 저장소 gms-high 프로필과 같은 설정. 대체 모델은 호출하지 않는다.
            body["max_completion_tokens"] = self.settings.max_output_tokens
            body["reasoning_effort"] = "none"
        else:
            body["max_tokens"] = self.settings.max_output_tokens
        request = Request(
            self.settings.base_url.rstrip("/") + "/chat/completions",
            data=json.dumps(body, ensure_ascii=False).encode("utf-8"),
            headers={"Authorization": "Bearer " + self.settings.api_key,
                     "Content-Type": "application/json"},
            method="POST",
        )
        started = time.monotonic()
        self.last_call = {"requested_model": self.settings.model,
                          "http_status": None, "actual_gms_credits": None}
        try:
            opener = self.opener or build_opener(_NoRedirect()).open
            with opener(request, timeout=self.settings.timeout_s) as response:
                status = response.status
                self.last_call["http_status"] = status
                body = json.load(response)
        except HTTPError as error:
            self.last_call["http_status"] = error.code
            self.last_call["elapsed_s"] = round(time.monotonic() - started, 3)
            raise ProviderCallError(f"GMS HTTP {error.code}") from None
        except TimeoutError:
            self.last_call["elapsed_s"] = round(time.monotonic() - started, 3)
            raise TimeoutError("GMS 응답 시간 초과") from None
        except URLError as error:
            self.last_call["elapsed_s"] = round(time.monotonic() - started, 3)
            if isinstance(error.reason, TimeoutError):
                raise TimeoutError("GMS 응답 시간 초과") from None
            raise ProviderCallError("GMS 연결 실패") from None
        except (json.JSONDecodeError, UnicodeDecodeError):
            self.last_call["elapsed_s"] = round(time.monotonic() - started, 3)
            raise ProviderCallError("GMS 응답 JSON 형식 오류") from None
        self.last_call["elapsed_s"] = round(time.monotonic() - started, 3)
        if not isinstance(body, dict):
            raise ProviderCallError("GMS 응답 형식 오류")
        usage = body.get("usage")
        if usage is None:
            usage = {}
        if not isinstance(usage, dict):
            raise ProviderCallError("GMS 사용량 응답 형식 오류")
        details = usage.get("prompt_tokens_details")
        if details is None:
            details = {}
        if not isinstance(details, dict):
            raise ProviderCallError("GMS 사용량 상세 형식 오류")
        counts = [usage.get(name) for name in ("prompt_tokens", "completion_tokens", "total_tokens")]
        counts.append(details.get("cached_tokens"))
        if any(value is not None and (type(value) is not int or value < 0) for value in counts):
            raise ProviderCallError("GMS 토큰 수 형식 오류")
        choices = body.get("choices")
        choice = choices[0] if isinstance(choices, list) and choices else {}
        if not isinstance(choice, dict):
            choice = {}
        self.last_call = {
            "requested_model": self.settings.model,
            "returned_model": body.get("model"),
            "response_id": body.get("id"),
            "http_status": status,
            "elapsed_s": round(time.monotonic() - started, 3),
            "finish_reason": choice.get("finish_reason"),
            "usage": {
                "input_tokens": usage.get("prompt_tokens"),
                "output_tokens": usage.get("completion_tokens"),
                "total_tokens": usage.get("total_tokens"),
                "cached_input_tokens": details.get("cached_tokens"),
            },
            "actual_gms_credits": None,
        }
        if choice.get("finish_reason") != "stop":
            raise ProviderCallError("GMS 응답이 정상 완료되지 않았습니다.")
        message = choice.get("message")
        if isinstance(message, dict) and message.get("refusal"):
            raise ProviderCallError("GMS가 피드백 생성을 거절했습니다.")
        content = message.get("content") if isinstance(message, dict) else None
        if not isinstance(content, str) or not content.strip():
            raise ProviderCallError("GMS 응답 본문이 없습니다.")
        return content
