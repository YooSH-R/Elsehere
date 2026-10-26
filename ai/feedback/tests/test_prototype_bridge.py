import json
import unittest
from pathlib import Path
from types import SimpleNamespace

from feedback.clients import FixtureClient
from feedback.models import INPUT
from feedback.prompt import build_messages
from feedback.prototype_bridge import build_prototype_input, generate_prototype_feedback


class PrototypeBridgeTests(unittest.TestCase):
    def test_unknown_guard_and_korean_original_remain_final_while_blocked_text_is_excluded(self):
        conversation = {
            "id": "conv_example",
            "uid": "user_example",
            "npc": "emma",
            "level": 2,
            "history": [
                {"who": "npc", "text": "What would you like?"},
                {"who": "me", "text": "차 주세요", "english": "Generated English", "rude": None},
                {"who": "me", "text": "BLOCKED_MARKER", "rude": True},
                {"who": "me", "text": "Thank you.", "rude": False},
            ],
        }
        payload = build_prototype_input(conversation, {"scene": "a cafe"}, {"place": "cafe", "title_ko": "카페"}, "gpt-5.4")
        self.assertEqual([turn["text_status"] for turn in payload["turns"]], ["final", "final", "blocked", "final"])
        messages = build_messages(payload)
        sent = json.loads(messages[1]["content"])
        self.assertEqual(sent["required_review_turn_ids"], ["t02", "t04"])
        self.assertEqual(sent["turns"][1]["final_text"], "차 주세요")
        self.assertNotIn("BLOCKED_MARKER", messages[1]["content"])
        self.assertNotIn("Generated English", messages[1]["content"])

    def test_all_blocked_turns_skip_model_and_do_not_reuse_previous_call_usage(self):
        class PreviouslyUsedClient(FixtureClient):
            settings = SimpleNamespace(model="gpt-5.4")
            last_call = {"elapsed_s": 8.0, "usage": {"input_tokens": 999, "output_tokens": 555}}

        client = PreviouslyUsedClient({})
        conversation = {
            "id": "conv_blocked",
            "uid": "user_example",
            "npc": "emma",
            "level": 2,
            "history": [{"who": "me", "text": "blocked learner text", "rude": True}],
        }
        result = generate_prototype_feedback(conversation, {"scene": "a cafe"}, {"place": "cafe", "title_ko": "카페"}, client)
        self.assertEqual(result["status"], "insufficient_evidence")
        self.assertEqual(client.calls, 0)
        self.assertEqual(result["feedback_call"], {})
        self.assertIsNone(result["usage"]["input_tokens"])
        self.assertIsNone(result["usage"]["output_tokens"])
        self.assertIsNone(result["usage"]["model"])

    def test_confirmed_rude_turn_is_blocked_without_replacing_original_text(self):
        conversation = {
            "id": "conv_example",
            "uid": "user_example",
            "npc": "emma",
            "level": 2,
            "history": [
                {"who": "npc", "text": "What would you like?"},
                {"who": "me", "text": "무례한 원문", "english": "Generated translation", "rude": True},
            ],
        }
        payload = build_prototype_input(conversation, {"scene": "a cafe"}, {"place": "cafe", "title_ko": "카페 주문"}, "gpt-5.4")
        self.assertEqual(payload["turns"][1]["text_status"], "blocked")
        self.assertEqual(payload["turns"][1]["final_text"], "무례한 원문")

    def test_prototype_end_returns_validated_feedback_and_usage(self):
        class SampleClient(FixtureClient):
            settings = SimpleNamespace(model="gpt-5.4")
            last_call = {
                "elapsed_s": 1.0,
                "requested_model": "gpt-5.4",
                "returned_model": "gpt-5.4-test",
                "usage": {"input_tokens": 100, "output_tokens": 20, "cached_input_tokens": 0},
            }

        root = Path(__file__).resolve().parent.parent
        body = json.loads((root / "samples/fixtures/free_grammar.llm.json").read_text(encoding="utf-8"))
        client = SampleClient(body)
        conv = {
            "id": "conv_example",
            "uid": "user_example",
            "npc": "alex",
            "level": 2,
            "closed": True,
            "history": [
                {"who": "npc", "text": "Where would you like to go?"},
                {"who": "me", "text": "I want go to the station."},
                {"who": "npc", "text": "The station is straight ahead."},
            ],
        }
        result = generate_prototype_feedback(conv, {"scene": "a street"}, {"place": "street", "title_ko": "길 안내"}, client)
        self.assertEqual(result["status"], "completed")
        self.assertEqual(result["result"]["turn_reviews"][0]["original_text"], "I want go to the station.")
        self.assertEqual(result["usage"]["model"], "gpt-5.4-test")
        self.assertEqual(result["usage"]["input_tokens"], 100)
        self.assertEqual(client.calls, 1)

    def test_original_learner_text_and_order_are_preserved(self):
        conversation = {
            "id": "conv_example",
            "uid": "user_example",
            "npc": "emma",
            "level": 2,
            "history": [
                {"who": "npc", "text": "What would you like?"},
                {"who": "me", "text": "I want drink a coffee.", "english": "I want to drink a coffee.", "input_mode": "voice"},
                {"who": "npc", "text": "Sure. What size?"},
            ],
        }
        payload = build_prototype_input(conversation, {"scene": "a cafe"}, {"place": "cafe", "title_ko": "카페 주문"}, "gpt-5.4")
        checked = INPUT.validate_python(payload)
        self.assertEqual([t.speaker for t in checked.turns], ["npc", "user", "npc"])
        self.assertEqual(checked.turns[1].final_text, "I want drink a coffee.")
        self.assertEqual(checked.turns[1].input_type, "voice")
        self.assertIsNone(checked.turns[1].original_transcript)
        self.assertEqual(checked.scope.conversation_id, "conv_example")
        self.assertEqual(checked.context.mode, "free_chat")
        self.assertEqual(checked.turns[1].assistance_ids, [])


if __name__ == "__main__":
    unittest.main()
