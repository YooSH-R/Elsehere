import io
import json
import unittest
from urllib.error import HTTPError, URLError

from feedback.clients import ModelNotConfigured, ProviderCallError
from feedback.gms import GmsClient, GmsSettings, feedback_response_format


class FakeResponse(io.BytesIO):
    def __init__(self, body, headers=None):
        super().__init__(json.dumps(body).encode())
        self.status = 200
        self.headers = headers or {}


class GmsTests(unittest.TestCase):
    def test_refusal_never_becomes_usable_model_json(self):
        client = GmsClient(
            GmsSettings(api_key="unit-test-only"),
            opener=lambda *a, **kw: FakeResponse(
                {
                    "choices": [{"finish_reason": "stop", "message": {"content": "{}", "refusal": "unit-test-only"}}],
                    "usage": {"prompt_tokens": 10, "completion_tokens": 2},
                }
            ),
        )
        with self.assertRaises(ProviderCallError) as error:
            client.complete([])
        self.assertNotIn("unit-test-only", str(error.exception))
        self.assertEqual(client.last_call["usage"]["input_tokens"], 10)

    def test_invalid_token_counts_are_rejected_not_displayed_as_cost(self):
        for value in (-1, True, "100", 2.5):
            with self.subTest(value=value):
                client = GmsClient(
                    GmsSettings(api_key="unit-test-only"),
                    opener=lambda *a, **kw: FakeResponse(
                        {
                            "choices": [{"finish_reason": "stop", "message": {"content": "{}"}}],
                            "usage": {"prompt_tokens": value},
                        }
                    ),
                )
                with self.assertRaises(ProviderCallError):
                    client.complete([])

    def test_wrapped_timeout_remains_timeout_without_retry(self):
        calls = []

        def opener(request, timeout):
            calls.append(request)
            raise URLError(TimeoutError("unit-test-only"))

        client = GmsClient(GmsSettings(api_key="unit-test-only"), opener=opener)
        with self.assertRaises(TimeoutError) as error:
            client.complete([])
        self.assertNotIn("unit-test-only", str(error.exception))
        self.assertEqual(len(calls), 1)

    def test_malformed_usage_details_cannot_crash_feedback(self):
        client = GmsClient(
            GmsSettings(api_key="unit-test-only"),
            opener=lambda *a, **kw: FakeResponse(
                {
                    "choices": [{"finish_reason": "stop", "message": {"content": "{}"}}],
                    "usage": {"prompt_tokens": 10, "completion_tokens": 2, "prompt_tokens_details": ["bad"]},
                }
            ),
        )
        with self.assertRaises(ProviderCallError):
            client.complete([])
        self.assertIsNotNone(client.last_call.get("elapsed_s"))

    def test_user_assessment_schema_only_accepts_user_turn_ids(self):
        requests = []

        def opener(request, timeout):
            requests.append(json.loads(request.data))
            return FakeResponse({"choices": [{"finish_reason": "stop", "message": {"content": "{}"}}]})

        messages = [{"role": "user", "content": json.dumps({"required_review_turn_ids": ["u1", "u2"]})}]
        GmsClient(GmsSettings(api_key="unit-test-only"), opener=opener).complete(messages)
        definitions = requests[0]["response_format"]["json_schema"]["schema"]["$defs"]
        self.assertEqual(definitions["UserEvidence"]["properties"]["turn_id"]["enum"], ["u1", "u2"])
        for name in ("Communication", "ReviewTag"):
            self.assertEqual(definitions[name]["properties"]["evidence"]["items"]["$ref"], "#/$defs/UserEvidence")
        self.assertEqual(definitions["Suggestion"]["properties"]["source"]["$ref"], "#/$defs/UserEvidence")
        self.assertNotIn("enum", definitions["Evidence"]["properties"]["turn_id"])

    def test_gpt54_uses_planned_profile_parameters(self):
        requests = []

        def opener(request, timeout):
            requests.append(json.loads(request.data))
            return FakeResponse({"choices": [{"finish_reason": "stop", "message": {"content": "{}"}}]})

        GmsClient(GmsSettings(api_key="unit-test-only", model="gpt-5.4"), opener=opener).complete([])
        body = requests[0]
        self.assertEqual(body["model"], "gpt-5.4")
        self.assertNotIn("max_tokens", body)
        self.assertNotIn("temperature", body)
        self.assertEqual(body["max_completion_tokens"], 6000)
        self.assertEqual(body["reasoning_effort"], "none")

    def test_non_json_gateway_body_is_sanitized(self):
        def opener(request, timeout):
            response = io.BytesIO(b"unit-test-only: not JSON")
            response.status = 200
            return response

        client = GmsClient(GmsSettings(api_key="unit-test-only"), opener=opener)
        with self.assertRaises(ProviderCallError) as error:
            client.complete([])
        self.assertNotIn("unit-test-only", str(error.exception))
        self.assertEqual(client.last_call["http_status"], 200)

    def test_timeout_records_elapsed_and_does_not_retry(self):
        calls = []

        def opener(request, timeout):
            calls.append(request)
            raise TimeoutError("unit-test-only")

        client = GmsClient(GmsSettings(api_key="unit-test-only"), opener=opener)
        with self.assertRaises(TimeoutError) as error:
            client.complete([])
        self.assertNotIn("unit-test-only", str(error.exception))
        self.assertEqual(len(calls), 1)
        self.assertIsNotNone(client.last_call.get("elapsed_s"))

    def test_api_receives_strict_feedback_schema(self):
        requests = []

        def opener(request, timeout):
            requests.append(json.loads(request.data))
            return FakeResponse({"choices": [{"finish_reason": "stop", "message": {"content": "{}"}}]})

        GmsClient(GmsSettings(api_key="unit-test-only"), opener=opener).complete([])
        output_format = requests[0]["response_format"]
        self.assertEqual(output_format["type"], "json_schema")
        schema = output_format["json_schema"]
        self.assertTrue(schema["strict"])
        self.assertEqual(schema["schema"]["type"], "object")
        self.assertFalse(schema["schema"]["additionalProperties"])
        evidence = schema["schema"]["$defs"]["Evidence"]
        self.assertEqual(set(evidence["properties"]), {"turn_id", "quote"})

    def test_model_must_return_two_or_three_situation_expressions(self):
        schema = feedback_response_format([{"role": "user", "content": "{}"}])["json_schema"]["schema"]
        data = schema["$defs"]["FeedbackData"]
        self.assertIn("situation_expressions", data["required"])
        field = data["properties"]["situation_expressions"]
        self.assertEqual((field["minItems"], field["maxItems"]), (2, 3))
        item = schema["$defs"]["SituationExpression"]
        self.assertEqual(set(item["properties"]), {"expression", "meaning_ko", "usage_ko", "example_en", "example_ko", "speaker_role"})
        self.assertEqual(item["properties"]["speaker_role"]["enum"], ["learner", "npc", "both"])
        self.assertEqual(set(item["required"]), set(item["properties"]))

    def test_invalid_http_body_is_sanitized(self):
        for body in [
            {},
            {"choices": []},
            {"choices": [{"finish_reason": "stop", "message": {}}]},
            {"choices": [{"finish_reason": "stop", "message": {"content": None}}]},
            {"choices": [{"finish_reason": "stop", "message": {"content": "{}"}}], "usage": []},
        ]:
            with self.subTest(body=body):
                client = GmsClient(GmsSettings(api_key="unit-test-only"), opener=lambda *a, **kw: FakeResponse(body))
                with self.assertRaises(ProviderCallError):
                    client.complete([])

    def test_truncated_response_keeps_usage_but_is_not_success(self):
        client = GmsClient(
            GmsSettings(api_key="unit-test-only"),
            opener=lambda *a, **kw: FakeResponse(
                {
                    "choices": [{"finish_reason": "length", "message": {"content": "{}"}}],
                    "usage": {"prompt_tokens": 50, "completion_tokens": 2000, "total_tokens": 2050},
                }
            ),
        )
        with self.assertRaises(ProviderCallError):
            client.complete([])
        self.assertEqual(client.last_call["usage"]["total_tokens"], 2050)
        self.assertIsNone(client.last_call["usage"]["cached_input_tokens"])

    def test_connection_failure_is_sanitized_without_retry(self):
        calls = []

        def opener(request, timeout):
            calls.append(request)
            raise URLError("unit-test-only")

        client = GmsClient(GmsSettings(api_key="unit-test-only"), opener=opener)
        with self.assertRaises(ProviderCallError) as error:
            client.complete([])
        self.assertNotIn("unit-test-only", str(error.exception))
        self.assertEqual(len(calls), 1)

    def test_invalid_endpoint_or_missing_key_is_rejected_before_network(self):
        for key, base in [
            ("", "https://gms.ssafy.io/gmsapi/api.openai.com/v1"),
            ("unit-test-only", "https://other.example/v1"),
            ("unit-test-only", "http://gms.ssafy.io/v1"),
            ("unit-test-only", "https://gms.ssafy.io/v1?key=secret"),
        ]:
            with self.subTest(base=base):
                with self.assertRaises(ModelNotConfigured):
                    GmsClient(GmsSettings(api_key=key, base_url=base), opener=lambda *a, **kw: self.fail("network must not run")).complete(
                        []
                    )

    def test_http_error_is_sanitized_and_not_retried(self):
        calls = []

        def opener(request, timeout):
            calls.append(request)
            raise HTTPError(request.full_url, 429, "unit-test-only", {}, None)

        client = GmsClient(GmsSettings(api_key="unit-test-only"), opener=opener)
        with self.assertRaises(ProviderCallError) as error:
            client.complete([])
        self.assertNotIn("unit-test-only", str(error.exception))
        self.assertEqual(len(calls), 1)
        self.assertEqual(client.last_call["http_status"], 429)

    def test_one_request_preserves_proxy_path_and_records_tokens(self):
        captured = []

        def opener(request, timeout):
            captured.append(request)
            self.assertGreater(timeout, 0)
            return FakeResponse(
                {
                    "id": "test-response",
                    "model": "gpt-4o-mini-test",
                    "choices": [{"finish_reason": "stop", "message": {"content": '{"outcome":"usable"}'}}],
                    "usage": {
                        "prompt_tokens": 100,
                        "completion_tokens": 20,
                        "total_tokens": 120,
                        "prompt_tokens_details": {"cached_tokens": 0},
                    },
                }
            )

        client = GmsClient(GmsSettings(api_key="unit-test-only"), opener=opener)
        self.assertEqual(json.loads(client.complete([{"role": "user", "content": "JSON"}]))["outcome"], "usable")
        self.assertEqual(len(captured), 1)
        self.assertEqual(captured[0].full_url, "https://gms.ssafy.io/gmsapi/api.openai.com/v1/chat/completions")
        self.assertEqual(client.last_call["usage"]["input_tokens"], 100)
        self.assertEqual(client.last_call["usage"]["output_tokens"], 20)
        self.assertIsNone(client.last_call["actual_gms_credits"])
        self.assertNotIn("unit-test-only", json.dumps(client.last_call))


if __name__ == "__main__":
    unittest.main()
