import copy
import json
import subprocess
import sys
import unittest
from pathlib import Path

from pydantic import ValidationError

from feedback.clients import CallableClient, FixtureClient, ModelNotConfigured, ProviderCallError, UnconfiguredClient
from feedback.models import INPUT, OUTPUT, validate_pair
from feedback.prompt import build_messages
from feedback.service import generate_feedback
from feedback.situation_catalog import situation_candidates

ROOT = Path(__file__).resolve().parents[1]


def read(path):
    return json.loads((ROOT / path).read_text(encoding="utf-8"))


def sample(name="free_grammar"):
    return read(f"samples/inputs/{name}.json"), read(f"samples/fixtures/{name}.llm.json")


# 장면에서 자주 쓰는 표현. 사용자 발화 평가와 별개로 이 상황에서 써 볼 표현을 알려 준다.
SITUATION = [
    {
        "expression": "Could I get that to go?",
        "meaning_ko": "포장해 주실 수 있나요?",
        "usage_ko": "카페에서 음료를 가지고 나가고 싶을 때 써요.",
        "example_en": "Could I get that to go? I'm in a bit of a hurry.",
        "example_ko": "포장해 주실 수 있나요? 좀 급해서요.",
    },
    {
        "expression": "I'll have the same.",
        "meaning_ko": "같은 걸로 할게요.",
        "usage_ko": "앞사람과 같은 메뉴를 주문할 때 짧게 써요.",
        "example_en": "That sounds good. I'll have the same.",
        "example_ko": "좋네요. 저도 같은 걸로 할게요.",
    },
    {
        "expression": "Can I get a refill?",
        "meaning_ko": "리필해 주실 수 있나요?",
        "usage_ko": "다 마신 음료를 다시 채워 달라고 할 때 써요.",
        "example_en": "Excuse me, can I get a refill on my coffee?",
        "example_ko": "실례지만 커피 리필해 주실 수 있나요?",
    },
]


class FeedbackTests(unittest.TestCase):
    def test_quest_summary_instruction_excludes_pending_objectives_and_unobserved_actions(self):
        request, _ = sample("free_order")
        request["context"].update(
            mode="quest",
            scene_description="은행 · 이번 대화의 할 일: ATM 위치 묻기, 인출 방법과 금액 문의.",
        )

        system = build_messages(request)[0]["content"]

        self.assertIn("실제로 주고받은 요청·답변·결정만", system)
        self.assertIn("남은 목표나 미완료 여부를 추정하지 않는다", system)
        self.assertIn("대화 밖에서 실제로 수행했는지 평가하지 않는다", system)

    def test_improvement_tag_comes_from_an_actual_sentence_correction(self):
        request, body = sample()
        result = body["result"]
        review = result["turn_reviews"][0]
        tag = result["review_tags"][0]
        tag.update(kind="improvement", label_ko="자연스러운 영어로 말하려고 했어요.")

        response = self.run_body(body, request)

        self.assertEqual(response["status"], "completed")
        improvements = [tag for tag in response["result"]["review_tags"] if tag["kind"] == "improvement"]
        self.assertEqual([tag["label_ko"] for tag in improvements], [review["explanation_ko"]])
        self.assertEqual(improvements[0]["evidence"], [review["suggestions"][0]["source"]])

    def test_improvement_without_sentence_correction_is_omitted(self):
        request, body = sample("free_order")
        body["result"]["review_tags"][0].update(
            kind="improvement",
            label_ko="자연스러운 영어로 주문하려고 했어요.",
        )

        response = self.run_body(body, request)

        self.assertEqual(response["status"], "completed")
        self.assertEqual(response["result"]["review_tags"], [])

    def run_body(self, body, request=None):
        return generate_feedback(request or sample()[0], FixtureClient(body), ai_allowed=True)

    def assert_invalid(self, mutation, case="free_grammar"):
        request, body = sample(case)
        mutation(body["result"])
        response = self.run_body(body, request)
        self.assertEqual(response["status"], "failed")
        self.assertEqual(response["problem"]["code"], "INVALID_MODEL_OUTPUT")
        self.assertIsNone(response["result"])

    def test_all_13_cases_go_through_service(self):
        cases = read("samples/index.json")
        self.assertEqual(len(cases), 13)
        for case in cases:
            with self.subTest(case=case["case_id"]):
                req, body = read(case["input_file"]), read(case["fixture_file"])
                rubric = read(case["rubric_file"])
                client = FixtureClient(body)
                response = generate_feedback(req, client, ai_allowed=True)
                validate_pair(req, response)
                self.assertIn(response["status"], rubric["accepted_statuses"])
                self.assertEqual(client.calls, 0 if case["case_id"] in ("no_user_turns", "unconfirmed_only") else 1)
                if response["status"] == "completed":
                    result = response["result"]
                    user_ids = [t["turn_id"] for t in req["turns"] if t["speaker"] == "user" and t["text_status"] == "final"]
                    self.assertEqual([r["turn_id"] for r in result["turn_reviews"]], user_ids)
                    if rubric["accepted_judgements"]:
                        self.assertIn(result["communication"]["judgement"], rubric["accepted_judgements"])
                    kinds = {s["kind"] for r in result["turn_reviews"] for s in r["suggestions"]}
                    self.assertFalse(set(rubric["forbidden_item_kinds"]) & kinds)
                    self.assertNotIn("next_practice", result)

    def test_mixed_five_turns_keeps_correct_unclear_unassessable(self):
        req, body = sample("mixed_five_turns")
        result = self.run_body(body, req)["result"]
        self.assertEqual(len(result["turn_reviews"]), 5)
        self.assertEqual(
            [r["verdict"] for r in result["turn_reviews"]],
            ["appropriate", "appropriate", "improvement_suggested", "needs_clarification", "not_assessable"],
        )
        for index in (0, 1, 3, 4):
            self.assertEqual(result["turn_reviews"][index]["suggestions"], [])

    def test_multi_sentence_utterance_is_one_review(self):
        req, body = sample("free_order")
        req["turns"][1]["final_text"] += " Thank you."
        body["result"]["turn_reviews"][0]["original_text"] = req["turns"][1]["final_text"]
        self.assertEqual(len(self.run_body(body, req)["result"]["turn_reviews"]), 2)

    def test_missing_review_rejected(self):
        self.assert_invalid(lambda r: r["turn_reviews"].pop(), "free_order")

    def test_duplicate_review_rejected(self):
        def mutate(result):
            row = copy.deepcopy(result["turn_reviews"][0])
            row["review_id"] = "different_review_id"
            result["turn_reviews"].append(row)

        self.assert_invalid(mutate, "free_order")

    def test_wrong_review_order_rejected(self):
        self.assert_invalid(lambda r: r["turn_reviews"].reverse(), "free_order")

    def test_npc_review_rejected(self):
        def mutate(result):
            row = result["turn_reviews"][0]
            row["turn_id"], row["original_text"] = "t01", "Where would you like to go?"

        self.assert_invalid(mutate)

    def test_unconfirmed_review_rejected(self):
        req, body = sample("free_order")
        req["turns"][3]["text_status"] = "needs_review"
        self.assertEqual(self.run_body(body, req)["status"], "failed")

    def test_changed_or_partial_original_text_rejected(self):
        for value in ("I never said this.", "I want go"):
            with self.subTest(value=value):
                self.assert_invalid(lambda r: r["turn_reviews"][0].update(original_text=value))

    def test_suggestion_cannot_reference_another_turn(self):
        def mutate(result):
            result["turn_reviews"][0]["suggestions"][0]["source"] = {"turn_id": "t01", "quote": "Where would you like to go?"}

        self.assert_invalid(mutate)

    def test_suggestion_quote_must_exist(self):
        self.assert_invalid(lambda r: r["turn_reviews"][0]["suggestions"][0]["source"].update(quote="Invented quote"))

    def test_review_assistance_must_match(self):
        self.assert_invalid(lambda r: r["turn_reviews"][0].update(assisted=False), "tutorial_assisted")

    def test_tag_assistance_must_match(self):
        self.assert_invalid(lambda r: r["review_tags"][0].update(assisted=False), "tutorial_assisted")

    def test_tag_requires_actual_user_evidence(self):
        def mutate(result):
            result["review_tags"][0]["evidence"] = [{"turn_id": "t01", "quote": "Where would you like to go?"}]

        self.assert_invalid(mutate)

    def test_empty_or_duplicate_tags_rejected(self):
        self.assert_invalid(lambda r: r["review_tags"][0].update(evidence=[]))
        self.assert_invalid(lambda r: r["review_tags"].append(copy.deepcopy(r["review_tags"][0])))

    def test_summary_can_use_npc_context(self):
        req, body = sample()
        body["result"]["conversation_summary"]["evidence"] = [{"turn_id": "t03", "quote": "The station is straight ahead."}]
        self.assertEqual(self.run_body(body, req)["status"], "completed")

    def test_summary_cannot_use_missing_turn(self):
        self.assert_invalid(lambda r: r["conversation_summary"]["evidence"][0].update(turn_id="missing"))

    def test_summary_cannot_use_unconfirmed_turn(self):
        req, body = sample("free_order")
        req["turns"][0]["text_status"] = "needs_review"
        body["result"]["conversation_summary"]["evidence"] = [{"turn_id": "t01", "quote": req["turns"][0]["final_text"]}]
        self.assertEqual(self.run_body(body, req)["status"], "failed")

    def test_summary_cannot_use_system_notice(self):
        req, body = sample()
        turn = copy.deepcopy(req["turns"][-1])
        turn.update(
            turn_id="system01", sequence=4, speaker="system", input_type="system", text_status="system_notice", final_text="System message"
        )
        req["turns"].append(turn)
        body["result"]["conversation_summary"]["evidence"] = [{"turn_id": "system01", "quote": "System message"}]
        self.assertEqual(self.run_body(body, req)["status"], "failed")

    def test_original_stt_cannot_be_evidence(self):
        req, body = sample("stt_corrected")
        body["result"]["review_tags"][0]["evidence"][0]["quote"] = req["turns"][1]["original_transcript"]
        self.assertEqual(self.run_body(body, req)["status"], "failed")

    def test_correct_sentence_not_classified_as_error(self):
        self.assert_invalid(lambda r: r["turn_reviews"][0]["suggestions"][0].update(kind="grammar_error"), "free_correct_alternative")

    def test_unclear_review_cannot_have_definite_suggestion(self):
        def mutate(result):
            result["turn_reviews"][0]["suggestions"] = [
                {
                    "suggestion_id": "wrong",
                    "kind": "natural_alternative",
                    "source": {"turn_id": "t02", "quote": "I need that."},
                    "suggested_expression": "I need a coffee.",
                    "explanation_ko": "추측한 뜻",
                }
            ]
            result["summary_suggestion_id"] = "wrong"

        self.assert_invalid(mutate, "ambiguous")

    def test_improvement_requires_suggestion(self):
        def mutate(result):
            result["turn_reviews"][0]["suggestions"] = []
            result["summary_suggestion_id"] = None
            result["vocab_candidates"] = []

        self.assert_invalid(mutate)

    def test_summary_suggestion_must_exist(self):
        self.assert_invalid(lambda r: r.update(summary_suggestion_id="missing"))

    def test_duplicate_suggestion_ids_rejected(self):
        self.assert_invalid(lambda r: r["turn_reviews"][0]["suggestions"].append(copy.deepcopy(r["turn_reviews"][0]["suggestions"][0])))

    def test_original_and_suggestion_vocab_sources_supported(self):
        for case, source in (("free_order", "user_turn"), ("free_grammar", "suggestion"), ("idiomatic_takeaway", "suggestion")):
            with self.subTest(case=case):
                req, body = sample(case)
                vocab = self.run_body(body, req)["result"]["vocab_candidates"][0]
                self.assertEqual(vocab["source"]["type"], source)
                for field in ("expression", "meaning_ko", "example_en", "example_ko"):
                    self.assertTrue(vocab[field])

    def test_situation_expressions_keep_meaning_usage_and_example(self):
        request, body = sample()
        body["result"]["situation_expressions"] = copy.deepcopy(SITUATION[:2])
        result = self.run_body(body, request)["result"]
        self.assertEqual(result["situation_expressions"], SITUATION[:2])

    def test_reviewed_candidates_fill_in_when_filters_leave_too_few_expressions(self):
        request, body = sample("free_order")
        request["context"].update(scene_id="CAFE", scene_description="카페 · 음료 주문")
        # 모델이 학습자가 이미 한 말과 직원 전용 대사만 골라 와 둘 다 걸러지는 경우
        body["result"]["situation_expressions"] = [
            dict(SITUATION[0], expression="A small one, please."),
            dict(SITUATION[1], expression="What can I get you?"),
        ]

        expressions = self.run_body(body, request)["result"]["situation_expressions"]

        reviewed = {item["expression"] for item in situation_candidates(INPUT.validate_python(request))}
        self.assertEqual(len(expressions), 2)
        for item in expressions:
            self.assertIn(item["expression"], reviewed)
            self.assertEqual(set(item), set(SITUATION[0]))

    def test_results_without_situation_expressions_stay_valid(self):
        request, body = sample()
        body["result"].pop("situation_expressions", None)
        result = self.run_body(body, request)["result"]
        self.assertEqual(result["situation_expressions"], [])

    def test_duplicate_situation_expressions_rejected(self):
        twin = dict(SITUATION[0], expression="could I get  that to go?")
        self.assert_invalid(lambda r: r.update(situation_expressions=[copy.deepcopy(SITUATION[0]), twin]))

    def test_more_than_three_situation_expressions_rejected(self):
        extra = dict(SITUATION[0], expression="Do you have oat milk?")
        self.assert_invalid(lambda r: r.update(situation_expressions=copy.deepcopy(SITUATION) + [extra]))

    def test_prompt_asks_for_situation_expressions_from_the_scene(self):
        system = build_messages(sample()[0])[0]["content"]
        self.assertIn("situation_expressions", system)
        self.assertIn("scene_description", system)

    def test_vocab_suggestion_and_review_must_exist(self):
        for key in ("review_id", "suggestion_id"):
            with self.subTest(key=key):
                self.assert_invalid(lambda r: r["vocab_candidates"][0]["source"].update({key: "missing"}))

    def test_vocab_expression_must_equal_quoted_source(self):
        self.assert_invalid(lambda r: r["vocab_candidates"][0].update(expression="Unrelated expression"))

    def test_vocab_quote_must_come_from_suggestion(self):
        def mutate(result):
            result["vocab_candidates"][0]["expression"] = "invented phrase"
            result["vocab_candidates"][0]["source"]["quote"] = "invented phrase"

        self.assert_invalid(mutate)

    def test_vocab_user_quote_must_come_from_user(self):
        self.assert_invalid(lambda r: r["vocab_candidates"][0]["source"].update(turn_id="t01"), "free_order")

    def test_vocab_does_not_invent_meaning_of_ambiguous_turn(self):
        req, body = sample("ambiguous")
        body["result"]["vocab_candidates"] = [
            {
                "candidate_id": "c1",
                "kind": "word",
                "expression": "that",
                "meaning_ko": "커피",
                "example_en": "I need that.",
                "example_ko": "저 커피가 필요해요.",
                "source": {"type": "user_turn", "turn_id": "t02", "quote": "that"},
            }
        ]
        self.assertEqual(self.run_body(body, req)["status"], "failed")

    def test_duplicate_vocab_expression_rejected(self):
        def mutate(result):
            item = copy.deepcopy(result["vocab_candidates"][0])
            item["candidate_id"] = "different_id"
            result["vocab_candidates"].append(item)

        self.assert_invalid(mutate)

    def test_vocab_requires_meaning_and_example(self):
        for field in ("meaning_ko", "example_en", "example_ko"):
            with self.subTest(field=field):
                self.assert_invalid(lambda r: r["vocab_candidates"][0].update({field: ""}))

    def test_no_score_game_state_or_repractice_fields(self):
        for key, value in (("score", 100), ("mission_completed", True), ("affinity_delta", -1), ("next_practice", None)):
            with self.subTest(field=key):
                self.assert_invalid(lambda r: r.update({key: value}))

    def test_model_cannot_choose_user(self):
        req, body = sample()
        body["user_id"] = "other_user"
        self.assertEqual(self.run_body(body, req)["status"], "failed")

    def test_no_final_user_skips_model(self):
        for case in ("no_user_turns", "unconfirmed_only"):
            with self.subTest(case=case):
                response = generate_feedback(sample(case)[0], UnconfiguredClient(), ai_allowed=True)
                self.assertEqual(response["problem"]["code"], "NO_USABLE_USER_TURNS")

    def test_quota_blocks_call(self):
        req, body = sample()
        client = FixtureClient(body)
        self.assertEqual(generate_feedback(req, client, ai_allowed=False)["status"], "blocked_by_quota")
        self.assertEqual(client.calls, 0)

    def test_explicit_boolean_approval_required(self):
        with self.assertRaises(TypeError):
            generate_feedback(sample()[0], UnconfiguredClient(), ai_allowed="true")

    def test_missing_gms_configuration_is_not_stub(self):
        with self.assertRaises(ModelNotConfigured):
            generate_feedback(sample()[0], UnconfiguredClient(), ai_allowed=True)

    def test_provider_failures_are_sanitized_without_retry_approval(self):
        for exception, code in ((TimeoutError, "AI_TIMEOUT"), (ProviderCallError, "AI_PROVIDER_ERROR")):
            with self.subTest(code=code):

                def fail(_):
                    raise exception("SECRET_PROVIDER_DETAIL")

                response = generate_feedback(sample()[0], CallableClient(fail), ai_allowed=True)
                self.assertEqual(response["problem"]["code"], code)
                self.assertFalse(response["problem"]["retryable_now"])
                self.assertNotIn("SECRET_PROVIDER_DETAIL", json.dumps(response))

    def test_invalid_raw_json_is_rejected(self):
        values = ["not json", "```json\n{}\n```", '{"outcome":"usable","outcome":"usable"}', '{"result":NaN}']
        for raw in values:
            with self.subTest(raw=raw):
                response = generate_feedback(sample()[0], CallableClient(lambda _: raw), ai_allowed=True)
                self.assertEqual(response["problem"]["code"], "INVALID_MODEL_OUTPUT")

    def test_context_insufficiency_and_false_no_user_reason(self):
        for code, expected in (("INSUFFICIENT_CONTEXT", "insufficient_evidence"), ("NO_USABLE_USER_TURNS", "failed")):
            body = {"outcome": "insufficient_evidence", "result": None, "problem": {"code": code, "message_ko": "근거 부족"}}
            self.assertEqual(self.run_body(body)["status"], expected)

    def test_cross_request_response_rejected(self):
        req, body = sample()
        for key, value in (("request_id", "other"), ("conversation_revision", 99), ("analysis_config_version", "other")):
            response = self.run_body(body, req)
            response[key] = value
            with self.assertRaises(ValueError):
                validate_pair(req, response)
        response = self.run_body(body, req)
        response["scope"]["user_id"] = "other"
        with self.assertRaises(ValueError):
            validate_pair(req, response)

    def test_failed_result_cannot_contain_previous_feedback(self):
        req, body = sample()
        response = self.run_body(body, req)
        response.update(status="failed", problem={"code": "AI_TIMEOUT", "message_ko": "오류", "retryable_now": False})
        with self.assertRaises(ValidationError):
            OUTPUT.validate_python(response)

    def test_invalid_input_never_calls_model(self):
        for mutation in (
            lambda r: r.update(schema_version="0.2.0"),
            lambda r: r["turns"].reverse(),
            lambda r: r["turns"][1].update(assistance_ids=["missing"]),
        ):
            req, body = sample()
            mutation(req)
            client = FixtureClient(body)
            with self.assertRaises(ValidationError):
                generate_feedback(req, client, ai_allowed=True)
            self.assertEqual(client.calls, 0)

    def test_tutorial_goal_matches_mode(self):
        for case, goal in (("tutorial_assisted", None), ("free_order", "wrong goal")):
            req, _ = sample(case)
            req["context"]["tutorial_goal"] = goal
            with self.assertRaises(ValidationError):
                INPUT.validate_python(req)

    def test_prompt_lists_all_required_user_turns(self):
        req, _ = sample("mixed_five_turns")
        body = json.loads(build_messages(req)[1]["content"])
        self.assertEqual(body["required_review_turn_ids"], ["t02", "t04", "t06", "t08", "t10"])

    def test_prompt_excludes_stt_raw_and_server_identity(self):
        req, _ = sample("stt_corrected")
        content = build_messages(req)[1]["content"]
        self.assertNotIn(req["turns"][1]["original_transcript"], content)
        self.assertNotIn("scope", json.loads(content))
        self.assertIn(req["turns"][1]["final_text"], content)

    def test_prompt_excludes_unconfirmed_and_blocked(self):
        req, _ = sample("unconfirmed_only")
        req["turns"][1]["final_text"] = "PRIVATE_UNCONFIRMED"
        for state in ("needs_review", "blocked", "failed"):
            req["turns"][1]["text_status"] = state
            body = json.loads(build_messages(req)[1]["content"])
            self.assertNotIn("PRIVATE_UNCONFIRMED", json.dumps(body))
            self.assertEqual(body["required_review_turn_ids"], [])

    def test_instructions_in_dialogue_stay_in_data(self):
        req, _ = sample("instruction_in_dialogue")
        messages = build_messages(req)
        self.assertEqual([m["role"] for m in messages], ["system", "user"])
        self.assertNotIn(req["turns"][1]["final_text"], messages[0]["content"])
        self.assertIn(req["turns"][1]["final_text"], messages[1]["content"])
        # 실제 모델의 지시문 저항성 검증은 아니다.

    def test_exported_schema_matches_models_and_resolves_refs(self):
        from feedback.models import INPUT, LLM_OUTPUT, OUTPUT

        for name, adapter in (("input", INPUT), ("output", OUTPUT), ("llm-output", LLM_OUTPUT)):
            schema = read(f"schemas/feedback.{name}.schema.json")
            self.assertEqual(schema, adapter.json_schema())

            def walk(value):
                if isinstance(value, dict):
                    if "$ref" in value:
                        self.assertTrue(value["$ref"].startswith("#/"))
                        target = schema
                        for part in value["$ref"][2:].split("/"):
                            target = target[part.replace("~1", "/").replace("~0", "~")]
                    for child in value.values():
                        walk(child)
                elif isinstance(value, list):
                    for child in value:
                        walk(child)

            walk(schema)

    def test_cli_provenance_and_real_mode_rejection(self):
        command = [sys.executable, "-X", "utf8", str(ROOT / "demo.py"), "--case", "mixed_five_turns"]
        proc = subprocess.run(command, cwd=ROOT, capture_output=True, encoding="utf-8", timeout=15)
        self.assertEqual(proc.returncode, 0, proc.stderr)
        record = json.loads(proc.stdout)
        self.assertEqual(record["execution_mode"], "stub")
        self.assertFalse(record["model_called"])
        self.assertEqual(len(record["response"]["result"]["turn_reviews"]), 5)
        proc = subprocess.run(command + ["--mode", "real"], cwd=ROOT, capture_output=True, encoding="utf-8", timeout=15)
        self.assertEqual(proc.returncode, 2)


if __name__ == "__main__":
    unittest.main()
