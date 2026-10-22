import copy
import json
from pathlib import Path
import unittest

from feedback.prompt import build_messages
from feedback.gms import feedback_response_format

ROOT = Path(__file__).resolve().parents[1]


def sample():
    return json.loads((ROOT / 'samples/inputs/free_order.json').read_text(encoding='utf-8'))


class PromptEfficiencyTests(unittest.TestCase):
    def test_generation_budget_keeps_all_reviews_and_limits_optional_material(self):
        messages = build_messages(sample())
        definitions = feedback_response_format(messages)['json_schema']['schema']['$defs']
        fields = definitions['FeedbackData']['properties']
        self.assertEqual(fields['turn_reviews']['minItems'], 2)
        self.assertEqual(fields['turn_reviews']['maxItems'], 2)
        self.assertEqual(fields['review_tags']['maxItems'], 2)
        self.assertEqual(fields['vocab_candidates']['maxItems'], 2)
        self.assertEqual(definitions['TurnReview']['properties']['suggestions']['maxItems'], 1)

    def test_schema_is_sent_once_through_api_format(self):
        messages = build_messages(sample())
        self.assertNotIn('"$defs"', messages[0]['content'])
        self.assertTrue(feedback_response_format(messages)['json_schema']['strict'])

    def test_difficulty_changes_and_voice_origin_survive_compaction(self):
        request = sample()
        request['turns'][1]['input_type'] = 'voice'
        request['turns'][-1]['difficulty'] = copy.deepcopy(request['turns'][0]['difficulty'])
        request['turns'][-1]['difficulty']['vocabulary_profile'] = 'advanced'
        messages = build_messages(request)
        body = json.loads(messages[-1]['content'])
        self.assertEqual(len(body['difficulty_profiles']), 2)
        for original, compact in zip(request['turns'], body['turns']):
            self.assertEqual(compact['final_text'], original['final_text'])
            self.assertEqual(body['difficulty_profiles'][compact['difficulty_id']], original['difficulty'])
        self.assertEqual(body['turns'][1]['input_type'], 'voice')
        self.assertNotIn('difficulty', body['turns'][0])


if __name__ == '__main__':
    unittest.main()
