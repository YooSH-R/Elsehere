-- 학습자 말의 영어 번역·다른 표현(/v1/expressions)을 의도 판정과 따로 부르므로 AI 호출 기록 종류에 EXPRESSIONS를 더한다.
alter table ai_requests drop constraint if exists ai_requests_kind_check;
alter table ai_requests
    add constraint ai_requests_kind_check
    check ((kind in ('STT','GUARD','JUDGE','NPC','TTS','HINT','MEANING','FEEDBACK','MEMORY','EXPRESSIONS')));
