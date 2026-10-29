import unittest

from feedback.costs import estimate_observed_gms_credits


class CostTests(unittest.TestCase):
    def test_missing_or_malformed_counts_never_mean_free(self):
        for usage in (
            {},
            [],
            {"input_tokens": True, "output_tokens": 10},
            {"input_tokens": -1, "output_tokens": 10},
            {"input_tokens": 10, "output_tokens": None},
        ):
            with self.subTest(usage=usage):
                self.assertIsNone(
                    estimate_observed_gms_credits({"requested_model": "gpt-5.4", "returned_model": "gpt-5.4", "usage": usage})
                )

    def test_observed_gpt54_formula_and_unknown_models_are_distinguished(self):
        call = {
            "requested_model": "gpt-5.4",
            "returned_model": "gpt-5.4-2026-03-05",
            "usage": {"input_tokens": 6130, "output_tokens": 746, "cached_input_tokens": 4864},
        }
        self.assertEqual(estimate_observed_gms_credits(call), 474.35)
        call["returned_model"] = "other-model"
        self.assertIsNone(estimate_observed_gms_credits(call))
