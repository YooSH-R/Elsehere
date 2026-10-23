import json
import unittest
from pathlib import Path

from feedback.clients import FixtureClient
from feedback.models import INPUT
from feedback.prompt import build_messages
from feedback.service import generate_feedback

ROOT = Path(__file__).resolve().parents[1]


def read(path):
    return json.loads((ROOT / path).read_text(encoding="utf-8"))


class RobustnessTests(unittest.TestCase):
    def test_long_conversation_preserves_all_twenty_reviews_and_rejects_one_omission(self):
        payload = read("samples/robustness/long_twenty.json")
        template = read("samples/fixtures/free_correct_alternative.llm.json")["result"]
        users = [turn for turn in payload["turns"] if turn["speaker"] == "user"]
        evidence = {"turn_id": users[0]["turn_id"], "quote": users[0]["final_text"]}
        template["conversation_summary"] = {"text_ko": "가상 카페에서 주문과 시설 이용을 묻는 대화다.", "evidence": [evidence]}
        template["communication"] = {"judgement": "clear", "explanation_ko": "요청을 전달했다.", "evidence": [evidence]}
        template.update(review_tags=[], summary_suggestion_id=None, vocab_candidates=[])
        template["turn_reviews"] = [
            {
                "review_id": f"r{index}",
                "turn_id": turn["turn_id"],
                "original_text": turn["final_text"],
                "verdict": "appropriate",
                "explanation_ko": "문맥에 적절하다.",
                "assisted": False,
                "suggestions": [],
            }
            for index, turn in enumerate(users, 1)
        ]
        body = {"outcome": "usable", "result": template, "problem": None}
        response = generate_feedback(payload, FixtureClient(body), ai_allowed=True)
        self.assertEqual(response["status"], "completed")
        self.assertEqual(len(response["result"]["turn_reviews"]), 20)
        body["result"]["turn_reviews"].pop(10)
        self.assertEqual(generate_feedback(payload, FixtureClient(body), ai_allowed=True)["status"], "failed")

    def test_all_robustness_inputs_are_valid_and_preserve_exact_learner_texts(self):
        for case in read("samples/robustness/index.json"):
            with self.subTest(case=case["case_id"]):
                request = INPUT.validate_python(read(case["input_file"]))
                prompt = json.loads(build_messages(request)[-1]["content"])
                expected = [(t.turn_id, t.final_text) for t in request.turns if t.speaker == "user" and t.text_status == "final"]
                actual = [(t["turn_id"], t["final_text"]) for t in prompt["turns"] if t["speaker"] == "user"]
                self.assertEqual(actual, expected)
                self.assertEqual(prompt["required_review_turn_ids"], [turn_id for turn_id, _ in expected])

    def test_voice_case_only_correction_is_rejected(self):
        payload = read("samples/inputs/free_correct_alternative.json")
        body = read("samples/fixtures/free_correct_alternative.llm.json")
        turn = next(turn for turn in payload["turns"] if turn["speaker"] == "user")
        turn["input_type"] = "voice"
        review = body["result"]["turn_reviews"][0]
        review["suggestions"][0]["suggested_expression"] = turn["final_text"].lower()
        body["result"]["vocab_candidates"] = []
        response = generate_feedback(payload, FixtureClient(body), ai_allowed=True)
        self.assertEqual(response["status"], "failed")
        self.assertEqual(response["problem"]["code"], "INVALID_MODEL_OUTPUT")
