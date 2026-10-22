const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const { test } = require('node:test');

function render(result) {
  const box = { innerHTML: '', querySelector: () => ({}) };
  const elements = {fbBox: box, fb: {classList: {add() {}}}, previewCafe: {}};
  const context = vm.createContext({
    $: id => elements[id],
    esc: value => String(value).replace(/[&<>"']/g, char => ({'&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;'}[char])),
    closeModals() {}, openTalk() {}, PREVIEW_VOICE_AVAILABLE: true,
  });
  vm.runInContext(fs.readFileSync(path.join(__dirname, '../prototype_panel.js'), 'utf8'), context);
  context.renderFeedbackV3({feedback: {status: 'completed', result}});
  return box.innerHTML;
}

function result() {
  return {
    conversation_summary: {text_ko: '가상 검증 예시'}, communication: {explanation_ko: '뜻이 전달됐어요.'},
    review_tags: [], vocab_candidates: [],
    turn_reviews: [{turn_id: 'u1', verdict: 'improvement_suggested', original_text: 'I forgot pay you back.',
      explanation_ko: '문법을 먼저 교정해요.', suggestions: [{kind: 'grammar_error',
        suggested_expression: 'I forgot to pay you back.', explanation_ko: 'to와 동사원형을 써요.'}]}],
    spoken_usage_notes: [{source: {turn_id: 'u1'}, expression: 'It slipped my mind.',
      meaning_ko: '깜빡 잊었어요.', usage_ko: '선택적인 회화 설명이에요.',
      example_en: "I'm sorry. It slipped my mind.", example_ko: '미안해요. 깜빡 잊었어요.',
      sources: ['https://rachelsenglish.com/conversation-practice/']}],
  };
}

test('문법 교정 다음에 별도의 실제 회화 설명과 학습 예문을 표시한다', () => {
  const html = render(result());
  assert.ok(html.includes('실제 회화에서는 이렇게 써요'));
  assert.ok(html.indexOf('I forgot to pay you back.') < html.indexOf('실제 회화에서는 이렇게 써요'));
  assert.ok(html.includes('학습용 예문'));
  assert.ok(html.includes('It slipped my mind.'));
});

test('기존 저장 결과에 새 설명 필드가 없어도 그대로 표시한다', () => {
  const data = result();
  delete data.spoken_usage_notes;
  const html = render(data);
  assert.ok(html.includes('I forgot to pay you back.'));
  assert.ok(!html.includes('실제 회화에서는 이렇게 써요'));
});

test('회화 설명의 HTML을 이스케이프하고 실행 가능한 출처 링크를 제외한다', () => {
  const data = result();
  data.spoken_usage_notes[0].usage_ko = '<img src=x onerror=alert(1)>';
  data.spoken_usage_notes[0].sources = ['javascript:alert(1)', 'https://example.com/" onclick="alert(1)'];
  const html = render(data);
  assert.ok(html.includes('&lt;img'));
  assert.ok(!html.includes('<img'));
  assert.ok(!html.includes('href="javascript:'));
  assert.ok(!html.includes('href="https://example.com/" onclick="'));
});
