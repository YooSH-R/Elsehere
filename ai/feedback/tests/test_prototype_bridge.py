import unittest
import json
from pathlib import Path
from types import SimpleNamespace

from feedback.clients import FixtureClient
from feedback.models import INPUT
from feedback.prototype_bridge import build_prototype_input, generate_prototype_feedback


class PrototypeBridgeTests(unittest.TestCase):
    def test_prototype_end_returns_validated_feedback_and_usage(self):
        class SampleClient(FixtureClient):
            settings = SimpleNamespace(model="gpt-5.4")
            last_call = {"elapsed_s": 1.0, "requested_model": "gpt-5.4", "returned_model": "gpt-5.4-test",
                         "usage": {"input_tokens": 100, "output_tokens": 20, "cached_input_tokens": 0}}
        root = Path(__file__).resolve().parent.parent
        body = json.loads((root / "samples/fixtures/free_grammar.llm.json").read_text(encoding="utf-8"))
        client = SampleClient(body)
        conv = {"id": "conv_example", "uid": "user_example", "npc": "alex", "level": 2, "closed": True,
                "history": [{"who": "npc", "text": "Where would you like to go?"},
                            {"who": "me", "text": "I want go to the station."},
                            {"who": "npc", "text": "The station is straight ahead."}]}
        result = generate_prototype_feedback(conv, {"scene": "a street"}, {"place": "street", "title_ko": "길 안내"}, client)
        self.assertEqual(result["status"], "completed")
        self.assertEqual(result["result"]["turn_reviews"][0]["original_text"], "I want go to the station.")
        self.assertEqual(result["usage"]["model"], "gpt-5.4-test")
        self.assertEqual(result["usage"]["input_tokens"], 100)
        self.assertEqual(client.calls, 1)

    def test_original_learner_text_and_order_are_preserved(self):
        conversation = {
            "id": "conv_example", "uid": "user_example", "npc": "emma", "level": 2,
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
