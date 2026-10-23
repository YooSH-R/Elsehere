"""수작업 가상 응답을 재생해 회화 설명 UI를 확인한다. 실제 모델/네트워크 호출 없음."""

import argparse
import copy
import json
from pathlib import Path

from feedback.clients import FixtureClient
from feedback.service import generate_feedback

ROOT = Path(__file__).resolve().parent


def examples():
    records = []
    cases = [
        (
            "문법 교정 뒤 회화 표현",
            "Did you send the money?",
            "I forgot pay you back.",
            "I forgot to pay you back.",
            "grammar_error",
            "C06",
            "해야 할 일을 잊었다는 뜻이면 forgot 뒤에 to와 동사원형을 써요.",
        ),
        ("안부에 자연스럽게 답함", "How are you?", "I'm good.", None, None, "C01", "안부를 묻는 질문에 잘 지낸다고 자연스럽게 답했어요."),
        (
            "수락 의도를 뒤에서 직접 확인함",
            "Would you like some water?",
            "I'm good.",
            "Yes, please.",
            "context_fit",
            "C01",
            "문법은 맞지만 사양으로 들릴 수 있어요. 뒤에서 확인한 수락 의도라면 이렇게 말해요.",
        ),
    ]
    base = json.loads((ROOT / "samples/inputs/free_grammar.json").read_text(encoding="utf-8"))
    for index, (title, npc, original, corrected, kind, catalog_id, explanation) in enumerate(cases):
        request = copy.deepcopy(base)
        request["request_id"] = f"authored_example_{index}"
        request["analysis_config_version"] = "authored_fixture_no_model"
        request["context"].update(scene_id="spoken_example", scene_description=title)
        request["turns"] = request["turns"][:2]
        request["turns"][0]["final_text"] = npc
        request["turns"][1]["final_text"] = original
        source = {"turn_id": "t02", "quote": original}
        review = {
            "review_id": "r2",
            "turn_id": "t02",
            "original_text": original,
            "verdict": "improvement_suggested" if corrected else "appropriate",
            "explanation_ko": explanation,
            "assisted": False,
            "suggestions": [],
        }
        if corrected:
            review["suggestions"] = [
                {"suggestion_id": "s2", "kind": kind, "source": source, "suggested_expression": corrected, "explanation_ko": explanation}
            ]
        reviews = [review]
        if kind == "context_fit":
            turn = copy.deepcopy(request["turns"][1])
            turn.update(turn_id="t03", sequence=3, final_text="I meant yes. I'd like some water.")
            request["turns"].append(turn)
            reviews.append(
                {
                    "review_id": "r3",
                    "turn_id": "t03",
                    "original_text": turn["final_text"],
                    "verdict": "appropriate",
                    "explanation_ko": "물을 받겠다는 뜻을 직접 확인했어요.",
                    "assisted": False,
                    "suggestions": [],
                }
            )
        body = {
            "outcome": "usable",
            "problem": None,
            "spoken_usage_selection": {"catalog_id": catalog_id, **source},
            "result": {
                "conversation_summary": {"text_ko": title + " — 수작업 가상 예시예요.", "evidence": [source]},
                "communication": {"judgement": "clear", "explanation_ko": explanation, "evidence": [source]},
                "review_tags": [],
                "turn_reviews": reviews,
                "summary_suggestion_id": "s2" if corrected else None,
                "vocab_candidates": [],
            },
        }
        response = generate_feedback(request, FixtureClient(body), ai_allowed=True)
        if response["status"] != "completed" or len(response["result"]["spoken_usage_notes"]) != 1:
            raise ValueError("가상 예시 검증 실패")
        records.append({"title": title, "feedback": response})
    return records


def render_html(records):
    data = json.dumps(records, ensure_ascii=False).replace("<", "\\u003c")
    panel = (ROOT / "prototype_panel.js").read_text(encoding="utf-8")
    return (
        """<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>피드백 표시 예시 · 모델 호출 없음</title><style>
body{margin:0;background:#f5f2eb;color:#28251f;font:17px/1.65 system-ui,sans-serif} main{max-width:920px;margin:32px auto;padding:0 24px}h1{font-size:26px;margin-bottom:4px}h2{margin:12px 0}h3,h4{color:#a96129;margin:4px 0 16px}button{font:inherit;cursor:pointer;border:1px solid #d8c8b7;background:white;border-radius:12px;padding:9px 14px;margin:8px 8px 8px 0}button[aria-pressed=true]{background:#386e55;color:white}#fbBox{background:#fffdfa;border-radius:24px;padding:24px;margin-top:16px}.fsec{border:1px solid #e5dccf;border-radius:17px;padding:20px;margin:16px 0}.fsec:empty{display:none}.said{color:#746b5e}.better{font-size:21px;font-weight:700;margin-top:8px}.spoken-note{background:#f0f6f1;border-color:#b8d0bd}small,summary{font-size:14px;color:#6b6257}p{margin:12px 0}.x{display:none}a{color:#276244}#previewCafe{display:none}
</style><main><h1>문법·문맥 교정 → 실제 회화 용법</h1><p>수작업 가상 응답의 표시 예시 · 실제 모델 호출 없음 · 저장·수집 없음</p><nav id="cases"></nav><div id="fb"><div id="fbBox"></div></div><button id="previewCafe"></button></main><script>
const $ = id => document.getElementById(id);
const esc = value => String(value).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const PREVIEW_VOICE_AVAILABLE = true;
function closeModals() {} function openTalk() {}
"""
        + panel
        + "\nconst records = "
        + data
        + """;
records.forEach((record, index) => {
  const button = document.createElement('button'); button.textContent = record.title;
  button.onclick = () => {document.querySelectorAll('nav button').forEach(b => b.setAttribute('aria-pressed','false')); button.setAttribute('aria-pressed','true'); renderFeedbackV3(record);};
  $('cases').append(button); if(index === 0) button.click();
});
</script></html>"""
    )


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "local_results/spoken_usage_preview.html")
    args = parser.parse_args()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    records = examples()
    args.output.write_text(render_html(records), encoding="utf-8", newline="\n")
    args.output.with_suffix(".json").write_text(
        json.dumps(
            {
                "execution_mode": "authored_fixture",
                "model_called": False,
                "records": records,
            },
            ensure_ascii=False,
            indent=2,
        )
        + "\n",
        encoding="utf-8",
        newline="\n",
    )
    print("수작업 예시 3개 구조 검증 및 HTML 생성 완료. 실제 모델 호출 0회.")


if __name__ == "__main__":
    main()
