"""고정 모델 응답으로 교정과 선택적 회화 설명의 연결을 검증한다. 실모델 품질 검사가 아니다."""

import json
import unittest
from pathlib import Path

from feedback.clients import FixtureClient
from feedback.gms import feedback_response_format
from feedback.models import validate_pair
from feedback.prompt import build_messages
from feedback.service import generate_feedback
from feedback.spoken_usage import load_catalog

ROOT = Path(__file__).resolve().parents[1]


def scenario(original="I forgot pay you back.", corrected="I forgot to pay you back."):
    request = json.loads((ROOT / "samples/inputs/free_grammar.json").read_text(encoding="utf-8"))
    body = json.loads((ROOT / "samples/fixtures/free_grammar.llm.json").read_text(encoding="utf-8"))
    old_original = request["turns"][1]["final_text"]
    old_corrected = body["result"]["turn_reviews"][0]["suggestions"][0]["suggested_expression"]
    # 같은 증거를 가리키는 기존 fixture의 모든 인용을 함께 바꾼다.
    serialized = json.dumps(body, ensure_ascii=False).replace(old_corrected, corrected).replace(old_original, original)
    body = json.loads(serialized)
    request["turns"][1]["final_text"] = original
    request["context"]["scene_description"] = "친구에게 돈 갚기를 잊었다고 말한다."
    body["spoken_usage_selection"] = {"catalog_id": "C06", "turn_id": "t02", "quote": original}
    return request, body


class SpokenUsageTests(unittest.TestCase):
    def test_reviewed_catalog_has_sources_and_examples_for_every_entry(self):
        catalog = load_catalog()
        self.assertEqual(len(catalog), 38)
        self.assertEqual(len({entry["catalog_id"] for entry in catalog}), 38)
        self.assertEqual(sum(entry["priority"] == "이해용" for entry in catalog), 3)
        for entry in catalog:
            with self.subTest(catalog_id=entry["catalog_id"]):
                self.assertTrue(entry["usage_ko"])
                self.assertTrue(entry["example_ko"])
                self.assertTrue(all(url.startswith("https://") for url in entry["sources"]))
                request, _ = scenario(entry["example_en"])
                candidates = json.loads(build_messages(request)[-1]["content"])["spoken_usage_candidates"]
                candidate = next(item for item in candidates if item["catalog_id"] == entry["catalog_id"])
                self.assertEqual(candidate["usage_mode"] == "understanding_only", entry["priority"] == "이해용")

    def test_empty_or_irrelevant_selection_keeps_correction_without_extra_calls(self):
        for selection in (
            None,
            {"catalog_id": "unknown", "turn_id": "t02", "quote": "I forgot pay you back."},
            {"catalog_id": "C01", "turn_id": "t02", "quote": "I forgot pay you back."},
            {"catalog_id": "C06", "turn_id": "t01", "quote": "Where would you like to go?"},
            {"catalog_id": "C06", "turn_id": "t02", "quote": "invented quote"},
        ):
            with self.subTest(selection=selection):
                request, body = scenario()
                body["spoken_usage_selection"] = selection
                client = FixtureClient(body)
                response = generate_feedback(request, client, ai_allowed=True)
                self.assertEqual(response["status"], "completed")
                self.assertEqual(response["result"]["spoken_usage_notes"], [])
                self.assertEqual(client.calls, 1)

    def test_clarification_or_unassessable_never_gets_spoken_note(self):
        for verdict in ("needs_clarification", "not_assessable"):
            with self.subTest(verdict=verdict):
                request, body = scenario()
                body["result"]["turn_reviews"][0].update(verdict=verdict, suggestions=[])
                body["result"].update(summary_suggestion_id=None, vocab_candidates=[])
                response = generate_feedback(request, FixtureClient(body), ai_allowed=True)
                self.assertEqual(response["status"], "completed")
                self.assertEqual(response["result"]["spoken_usage_notes"], [])

    def test_only_final_user_turns_are_searched_across_new_scenes(self):
        for scene in ("hospital_future", "library_future", "airport_future"):
            request, _ = scenario("I'm good.")
            request["context"].update(scene_id=scene, scene_description="나중에 추가할 상황")
            for status in ("final", "needs_review", "blocked", "failed"):
                request["turns"][1]["text_status"] = status
                candidates = json.loads(build_messages(request)[-1]["content"])["spoken_usage_candidates"]
                self.assertEqual(bool(candidates), status == "final")
            request["turns"][1].update(text_status="final", final_text="Hello.")
            request["turns"][0]["final_text"] = "I'm good."
            self.assertEqual(json.loads(build_messages(request)[-1]["content"])["spoken_usage_candidates"], [])

    def test_empty_candidates_force_null_and_all_provider_objects_are_strict(self):
        request, _ = scenario("Hello.")
        schema = feedback_response_format(build_messages(request))["json_schema"]["schema"]
        self.assertEqual(schema["properties"]["spoken_usage_selection"], {"type": "null"})

        def walk(node):
            if isinstance(node, dict):
                if node.get("type") == "object":
                    self.assertFalse(node["additionalProperties"])
                    self.assertEqual(set(node["properties"]), set(node["required"]))
                for child in node.values():
                    walk(child)
            elif isinstance(node, list):
                for child in node:
                    walk(child)

        walk(schema)

    def test_does_not_interpret_good_at_as_declining_offer(self):
        request, _ = scenario("I'm good at swimming.")
        candidates = json.loads(build_messages(request)[-1]["content"])["spoken_usage_candidates"]
        self.assertNotIn("C01", [item["catalog_id"] for item in candidates])

    def test_confirmed_acceptance_uses_context_correction_not_grammar_error(self):
        request, body = scenario("I'm good.", "Yes, please.")
        request["turns"][0]["final_text"] = "Would you like some water?"
        request["turns"][2]["final_text"] = "Let me know if you need anything."
        request["context"]["scene_description"] = "음료를 권하는 상황"
        # 뒤에 학습자가 수락 의도를 직접 확인한 경우. 질문만으로 수락을 추정하지 않는다.
        turn = dict(request["turns"][1])
        turn.update(turn_id="t04", sequence=4, final_text="I meant yes. I'd like some water.")
        request["turns"].append(turn)
        body["result"]["conversation_summary"]["evidence"] = [{"turn_id": "t02", "quote": "I'm good."}]
        body["result"]["turn_reviews"][0]["suggestions"][0]["kind"] = "context_fit"
        body["result"]["turn_reviews"].append(
            {
                "review_id": "r4",
                "turn_id": "t04",
                "original_text": turn["final_text"],
                "verdict": "appropriate",
                "explanation_ko": "수락 의도를 분명히 했어요.",
                "assisted": False,
                "suggestions": [],
            }
        )
        body["spoken_usage_selection"]["catalog_id"] = "C01"
        response = generate_feedback(request, FixtureClient(body), ai_allowed=True)
        self.assertEqual(response["status"], "completed")
        self.assertEqual(response["result"]["turn_reviews"][0]["suggestions"][0]["kind"], "context_fit")
        self.assertEqual(len(response["result"]["spoken_usage_notes"]), 1)

    def test_does_not_offer_forgetting_alternative_for_negated_forgetting(self):
        for text in ("I never forgot to pay you back.", "I haven't forgotten our plan."):
            with self.subTest(text=text):
                request, _ = scenario(text)
                candidates = json.loads(build_messages(request)[-1]["content"])["spoken_usage_candidates"]
                self.assertNotIn("C06", [item["catalog_id"] for item in candidates])

    def test_candidate_budget_does_not_duplicate_long_utterances(self):
        request, _ = scenario("A long introduction. " * 100 + "I'm good.")
        candidates = json.loads(build_messages(request)[-1]["content"])["spoken_usage_candidates"]
        self.assertGreater(len(candidates), 0)
        self.assertLessEqual(len(candidates), 3)
        for candidate in candidates:
            self.assertLessEqual(len(candidate["source"]["quote"]), 160)
            self.assertIn("I'm good", candidate["source"]["quote"])
            self.assertIn(candidate["source"]["quote"], request["turns"][1]["final_text"])

    def test_provider_only_offers_retrieved_catalog_ids_or_null(self):
        request, _ = scenario()
        schema = feedback_response_format(build_messages(request))["json_schema"]["schema"]
        self.assertIn("spoken_usage_selection", schema["required"])
        choices = schema["properties"]["spoken_usage_selection"]["anyOf"]
        self.assertIn({"type": "null"}, choices)
        selection = schema["$defs"]["SpokenUsageSelection"]
        self.assertEqual(selection["properties"]["catalog_id"]["enum"], ["C06"])
        self.assertEqual(set(selection["required"]), set(selection["properties"]))
        self.assertNotIn("SpokenUsageNote", schema["$defs"])

    def test_im_good_after_greeting_remains_appropriate_with_context_note(self):
        request, body = scenario("I'm good.", "Yes, please.")
        request["turns"][0]["final_text"] = "How are you?"
        request["context"]["scene_description"] = "친구와 안부를 묻고 답한다."
        body["result"]["conversation_summary"]["evidence"] = [{"turn_id": "t02", "quote": "I'm good."}]
        body["result"]["review_tags"] = []
        body["result"]["vocab_candidates"] = []
        body["result"]["summary_suggestion_id"] = None
        review = body["result"]["turn_reviews"][0]
        review.update(verdict="appropriate", explanation_ko="안부에 자연스럽게 답했어요.", suggestions=[])
        body["spoken_usage_selection"]["catalog_id"] = "C01"
        response = generate_feedback(request, FixtureClient(body), ai_allowed=True)
        self.assertEqual(response["status"], "completed")
        self.assertEqual(response["result"]["turn_reviews"][0]["suggestions"], [])
        note = response["result"]["spoken_usage_notes"][0]
        self.assertIn("안부", note["usage_ko"])
        self.assertIn("사양", note["usage_ko"])
        self.assertIn("잘 지내", note["meaning_ko"])

    def test_grammar_correction_stays_primary_with_one_related_spoken_note(self):
        request, body = scenario()
        client = FixtureClient(body)
        response = generate_feedback(request, client, ai_allowed=True)
        self.assertEqual(response["status"], "completed")
        validate_pair(request, response)
        result = response["result"]
        suggestion = result["turn_reviews"][0]["suggestions"][0]
        self.assertEqual(suggestion["kind"], "grammar_error")
        self.assertEqual(suggestion["suggested_expression"], "I forgot to pay you back.")
        self.assertEqual(len(result["spoken_usage_notes"]), 1)
        note = result["spoken_usage_notes"][0]
        self.assertEqual(note["expression"], "It slipped my mind.")
        self.assertEqual(note["source"], {"turn_id": "t02", "quote": request["turns"][1]["final_text"]})
        self.assertEqual(note["example_kind"], "learning_example")
        self.assertTrue(note["sources"])
        self.assertEqual(client.calls, 1)


if __name__ == "__main__":
    unittest.main()
