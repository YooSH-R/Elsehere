import subprocess
import sys
from pathlib import Path
import unittest
from unittest.mock import patch

from run_evaluation import check_expectations, require_local_checks


class EvaluationTests(unittest.TestCase):
    def test_failed_local_checks_block_real_evaluation(self):
        with patch('run_evaluation.subprocess.run', return_value=subprocess.CompletedProcess([],1,'','')):
            with self.assertRaises(RuntimeError):
                require_local_checks()

    def test_repeated_real_calls_are_blocked_before_network(self):
        result = subprocess.run([sys.executable, '-X', 'utf8', 'run_evaluation.py', '--execute-real',
                                 '--case', 'voice_surface', '--repeat', '3', '--env-file', 'does-not-exist.env'],
                                cwd=Path(__file__).resolve().parents[1], capture_output=True, text=True,
                                encoding='utf-8')
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('한 번에 한 사례', result.stderr)

    def test_real_batch_without_explicit_case_is_blocked_before_settings_or_network(self):
        result = subprocess.run([sys.executable, '-X', 'utf8', 'run_evaluation.py', '--execute-real',
                                 '--env-file', 'does-not-exist.env'],
                                cwd=Path(__file__).resolve().parents[1], capture_output=True, text=True,
                                encoding='utf-8')
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('실호출은 --case', result.stderr)

    def test_optional_material_limits_and_forbidden_kinds_are_checked(self):
        response = {'status':'completed','result':{'turn_reviews':[
            {'turn_id':'u1','verdict':'appropriate','suggestions':[{'kind':'grammar_error'}]}],
            'vocab_candidates':[{}]}}
        self.assertEqual(len(check_expectations(response, {'statuses':['completed'],
            'max_suggestions':0,'max_vocab':0,'forbid_suggestion_kinds':['grammar_error']})), 3)

    def test_wrong_verdict_and_missing_expected_correction_fail(self):
        response = {'status':'completed','result':{'turn_reviews':[
            {'turn_id':'u1','verdict':'appropriate','suggestions':[]}], 'vocab_candidates':[]}}
        expected = {'statuses':['completed'],'verdicts':{'u1':['improvement_suggested']},
                    'required_suggestion_kinds':{'u1':['grammar_error']}}
        issues = check_expectations(response, expected)
        self.assertEqual(len(issues), 2)
        response['result']['turn_reviews'][0].update(verdict='improvement_suggested', suggestions=[{'kind':'grammar_error'}])
        self.assertEqual(check_expectations(response, expected), [])
