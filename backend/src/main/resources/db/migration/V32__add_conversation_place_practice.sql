-- 장소 연습(PRC-02)으로 시작한 대화의 상황 ID, 그 대화에서 이룬 목표 ID, 상황의 목표를 모두 이룬 시각.
-- 장소 연습 목록(PRC-01)의 완료는 이 시각이 있는 대화가 하나라도 있는지로 본다. 장소 연습이 아닌 대화는 모두 NULL이다.
ALTER TABLE conversations ADD COLUMN practice_scenario varchar(40);
ALTER TABLE conversations ADD COLUMN practice_completed_goals jsonb;
ALTER TABLE conversations ADD COLUMN practice_done_at timestamp(6) with time zone;
