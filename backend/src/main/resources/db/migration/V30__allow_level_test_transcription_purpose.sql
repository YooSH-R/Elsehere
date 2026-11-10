-- 음성 인식 용도에 레벨 테스트 말하기 답(LEVEL_TEST)이 생겼다. 학습용 녹음은 대화·튜토리얼 발화만 보관하지만,
-- 용도 값은 엔티티와 같은 enum을 쓰므로 검사 조건을 맞춘다.
alter table training_recordings
    drop constraint if exists training_recordings_purpose_check;

alter table training_recordings
    add constraint training_recordings_purpose_check
    check (purpose in ('TURN', 'TUTORIAL', 'LEVEL_TEST'));
