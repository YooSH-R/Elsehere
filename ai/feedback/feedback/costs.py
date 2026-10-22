"""이 프로젝트의 GMS 실측 관측식. 공식 요금표나 실제 차감 응답이 아니다."""
from decimal import Decimal


def estimate_observed_gms_credits(call):
    if call.get('requested_model') != 'gpt-5.4':
        return None
    model = call.get('returned_model')
    if not isinstance(model,str) or not (model=='gpt-5.4' or model.startswith('gpt-5.4-')):
        return None
    usage = call.get('usage') or {}
    if not isinstance(usage,dict):
        return None
    counts = [usage.get('input_tokens'),usage.get('output_tokens')]
    if any(type(count) is not int or count < 0 for count in counts):
        return None
    # 이전 13건은 캐시 토큰 유무와 무관하게 이 식에 일치했다. 캐시 할인을 가정하지 않는다.
    return float(Decimal(counts[0])*Decimal('0.05') + Decimal(counts[1])*Decimal('0.225'))
