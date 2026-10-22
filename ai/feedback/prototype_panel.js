function renderFeedbackV3(record) {
  $("fb").classList.add("show");
  const box = $("fbBox"), feedback = record.feedback;
  let html = `<button class="x" title="닫기">✕</button><h2>대화 피드백</h2>`;
  if (feedback.status !== "completed") {
    html += `<div class="fsec">${esc(feedback.problem?.message_ko || "평가할 대화가 부족해요.")}</div>`;
  } else {
    const result = feedback.result;
    html += `<div class="fsec"><h3>대화 요약</h3>${esc(result.conversation_summary.text_ko)}</div>`;
    html += `<div class="fsec"><h3>의도 전달</h3>${esc(result.communication.explanation_ko)}</div>`;
    html += `<div class="fsec">${result.review_tags.map(tag => `<span class="badge ${tag.kind === "strength" ? "ok" : ""}">${esc(tag.label_ko)}${tag.assisted ? " · 도움 사용" : ""}</span>`).join(" ")}</div>`;
    const labels = {appropriate: "잘 표현했어요", improvement_suggested: "더 자연스럽게", needs_clarification: "뜻을 확인해야 해요", not_assessable: "평가할 표현이 부족해요"};
    html += `<div class="fsec"><h3>내가 한 말 돌아보기</h3>`;
    for (const review of result.turn_reviews) {
      html += `<div class="fsec"><strong>${esc(labels[review.verdict])}</strong><div class="said">${esc(review.original_text)}</div><p>${esc(review.explanation_ko)}</p>`;
      for (const suggestion of review.suggestions) {
        const kindLabels = {grammar_error: "문법 교정", vocabulary_error: "어휘 교정", natural_alternative: "다른 표현 · 원문도 적절해요", context_fit: "상황에 맞는 표현", idiomatic_expression: "선택할 수 있는 관용 표현"};
        html += `<small>${esc(kindLabels[suggestion.kind])}</small><div class="better">${esc(suggestion.suggested_expression)}</div><p>${esc(suggestion.explanation_ko)}</p>`;
      }
      const spokenNote = (result.spoken_usage_notes || []).find(note => note.source.turn_id === review.turn_id);
      if (spokenNote) {
        html += `<aside class="fsec spoken-note"><h4>실제 회화에서는 이렇게 써요</h4><strong>${esc(spokenNote.expression)}</strong><p>${esc(spokenNote.meaning_ko)}</p><p>${esc(spokenNote.usage_ko)}</p><small>학습용 예문 · 실제 플레이 발화와 별개</small><p>${esc(spokenNote.example_en)}<br>${esc(spokenNote.example_ko)}</p>`;
        const sources = spokenNote.sources.filter(url => /^https:\/\//i.test(url));
        html += `<details><summary>용법 참고 자료</summary>${sources.map((url, index) => `<p><a href="${esc(url)}" target="_blank" rel="noopener noreferrer">공개 대본 ${index + 1}</a></p>`).join("")}</details></aside>`;
      }
      html += `</div>`;
    }
    html += `</div>`;
    if (result.vocab_candidates.length) {
      html += `<div class="fsec"><h3>저장해둘 표현 후보</h3>`;
      for (const candidate of result.vocab_candidates) {
        html += `<p><strong>${esc(candidate.expression)}</strong><br>${esc(candidate.meaning_ko)}<br>${esc(candidate.example_en)}<br>${esc(candidate.example_ko)}</p>`;
      }
      html += `<small>검증판에서는 후보를 보여줍니다. 단어장 저장 연결은 별도입니다.</small></div>`;
    }
  }
  const call = feedback.feedback_call || {};
  const estimate = feedback.credit_estimate?.credits;
  html += `<details class="fsec"><summary>생성 정보</summary><p>모델: ${esc(call.returned_model || call.requested_model || "호출 없음")}<br>분석 설정: ${esc(feedback.analysis_config_version || "-")}<br>걸린 시간: ${esc(call.elapsed_s ?? "-")}초<br>입력 / 출력 토큰: ${esc(call.usage?.input_tokens ?? "-")} / ${esc(call.usage?.output_tokens ?? "-")}<br>캐시 입력 토큰: ${esc(call.usage?.cached_input_tokens ?? "미확인")}${estimate == null ? "" : `<br>예상 GMS 크레딧: ${esc(estimate)}<br><small>이전 사용 내역의 관측식 기준. 실제 차감량은 GMS에서 확인해야 해요.</small>`}</p></details>`;
  box.innerHTML = html;
  box.querySelector(".x").onclick = closeModals;
}

$("previewCafe").onclick = () => openTalk("emma");
if (!PREVIEW_VOICE_AVAILABLE) {
  $("mic").disabled = true;
  $("micText").textContent = "지금은 타자로 테스트하세요. 음성은 본인 Groq 키 설정 후 사용할 수 있어요.";
}
