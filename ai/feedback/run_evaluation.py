"""가상 강건성 사례를 수동 실호출하고 구조 검증과 명시적 기대 조건을 구분한다."""
import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import subprocess
import sys

from pydantic import ValidationError

from feedback.gms import GmsClient
from feedback.models import INPUT, LLM_OUTPUT, validate_pair
from feedback.prompt import PROMPT_VERSION
from feedback.service import generate_feedback
from run_gms import load_settings, SampleRecorder

ROOT = Path(__file__).resolve().parent


def require_local_checks():
    checks = subprocess.run([sys.executable,'-X','utf8','-m','unittest','discover','-s','tests'],
                            cwd=ROOT,capture_output=True,text=True,encoding='utf-8')
    if checks.returncode:
        raise RuntimeError('로컬 자동 검증에 실패했습니다. 모델 호출을 차단합니다.')


def check_expectations(response, expected):
    issues = []
    if response['status'] not in expected['statuses']:
        return ['unexpected_status:' + response['status']]
    if response['status'] != 'completed':
        return issues
    result = response['result']
    reviews = {review['turn_id']:review for review in result['turn_reviews']}
    for turn_id, accepted in expected.get('verdicts', {}).items():
        if reviews.get(turn_id, {}).get('verdict') not in accepted:
            issues.append('unexpected_verdict:' + turn_id)
    for turn_id, required in expected.get('required_suggestion_kinds', {}).items():
        kinds = {item['kind'] for item in reviews.get(turn_id, {}).get('suggestions', [])}
        if not set(required).issubset(kinds):
            issues.append('missing_correction:' + turn_id)
    suggestions = [item for review in reviews.values() for item in review['suggestions']]
    if len(suggestions) > expected.get('max_suggestions', float('inf')):
        issues.append('too_many_suggestions')
    if len(result['vocab_candidates']) > expected.get('max_vocab', float('inf')):
        issues.append('too_many_vocab')
    if set(expected.get('forbid_suggestion_kinds', [])) & {item['kind'] for item in suggestions}:
        issues.append('forbidden_correction_kind')
    return issues


def validation_diagnostic(payload, response, raw):
    """합성 데이터 수동 검증 전용. 원문을 진단 메시지에 포함하지 않는다."""
    if response['status'] != 'failed' or not raw:
        return None
    try:
        checked = LLM_OUTPUT.validate_json(raw)
        if checked.outcome == 'usable':
            validate_pair(payload, response | {'status':'completed', 'problem':None, 'result':checked.result.model_dump()})
    except ValidationError as error:
        return [{'loc':list(item['loc']), 'type':item['type']} for item in error.errors()]
    except ValueError as error:
        return str(error)
    return None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--case', action='append', help='실호출은 명시 필수. 생략하면 로컬 검증만 전체 수행')
    parser.add_argument('--repeat', type=int, default=1)
    parser.add_argument('--execute-real', action='store_true')
    parser.add_argument('--output-dir', type=Path)
    parser.add_argument('--env-file', type=Path, default=ROOT.parents[1]/'.env.local')
    args = parser.parse_args()
    if args.execute_real and not args.case:
        parser.error('실호출은 --case로 사례 하나를 명시해야 합니다. 전체 자동 호출은 지원하지 않습니다.')
    if args.execute_real and (len(args.case) != 1 or args.repeat != 1):
        parser.error('실호출은 한 번에 한 사례, 1회만 허용합니다. 먼저 로컬 검증 결과를 확인하세요.')
    if not 1 <= args.repeat <= 3:
        parser.error('repeat은 1~3회입니다. 자동 재시도가 아닌 명시적 반복 평가입니다.')
    cases = json.loads((ROOT/'samples/robustness/index.json').read_text(encoding='utf-8'))
    if args.case:
        unknown = set(args.case) - {case['case_id'] for case in cases}
        if unknown:
            parser.error('존재하지 않는 case_id: ' + ', '.join(sorted(unknown)))
        cases = [case for case in cases if case['case_id'] in args.case]
    settings = load_settings(args.env_file)
    if settings.model != 'gpt-5.4':
        parser.error('이 평가는 원래 기획 모델인 gpt-5.4 전용입니다.')
    payloads = []
    for case in cases:
        payload = json.loads((ROOT/case['input_file']).read_text(encoding='utf-8'))
        payload['analysis_config_version'] = f'{PROMPT_VERSION}:{settings.model}'
        payloads.append(INPUT.validate_python(payload).model_dump(mode='json'))
    if not args.execute_real:
        print(json.dumps({'network_called':False,'validated_case_count':len(cases),'real_calls':0,
                          'real_calls_allowed_per_execution':1,
                          'model':settings.model,'key_configured':bool(settings.api_key)}, ensure_ascii=False))
        return 0
    require_local_checks()
    output = args.output_dir or ROOT/'local_results'/datetime.now().strftime('robustness_%Y%m%d_%H%M%S')
    output.mkdir(parents=True, exist_ok=True)
    summaries = []
    for repetition in range(1, args.repeat+1):
        for case, payload in zip(cases, payloads):
            client = GmsClient(settings)
            recorder = SampleRecorder(client)
            started = datetime.now(timezone.utc).isoformat()
            result_file = output/f"{case['case_id']}_{repetition}.json"
            # 호출 전에 기록을 예약한다. 중단되면 과금 가능성이 있는 미완료 호출로 남는다.
            try:
                with result_file.open('x',encoding='utf-8') as stream:
                    json.dump({'execution_state':'started','case_id':case['case_id'],
                               'started_at_utc':started,'automatic_retries':0,
                               'billing_status':'응답 미확인. 중단돼도 공급자 과금 가능'},stream,ensure_ascii=False)
            except FileExistsError:
                parser.error('기존 검증 결과를 덮어쓰지 않습니다. 새 output-dir를 사용하세요.')
            response = generate_feedback(payload, recorder, ai_allowed=True)
            issues = check_expectations(response, case['expect'])
            record = {'execution_state':'finished','synthetic_sample':True,'case_id':case['case_id'],'repetition':repetition,
                      'prompt_version':PROMPT_VERSION,'started_at_utc':started,'automatic_retries':0,
                      'call':client.last_call,'response':response,'expectation_issues':issues,
                      'validation_diagnostic':validation_diagnostic(payload,response,recorder.content),
                      'semantic_review':'pending','raw_model_output':recorder.content}
            content = json.dumps(record,ensure_ascii=False,indent=2)
            if settings.api_key:
                content = content.replace(settings.api_key,'[REDACTED]')
            result_file.write_text(content+'\n',encoding='utf-8')
            summary = {key:record[key] for key in ('case_id','repetition','call','expectation_issues','validation_diagnostic')}
            summary['status'] = response['status']
            summaries.append(summary)
            print(json.dumps(summary,ensure_ascii=False),flush=True)
    (output/'summary.json').write_text(json.dumps(summaries,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    return 1 if any(item['expectation_issues'] for item in summaries) else 0


if __name__ == '__main__':
    raise SystemExit(main())
