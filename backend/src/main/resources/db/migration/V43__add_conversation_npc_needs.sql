-- 대화를 시작한 순간 NPC의 생활 욕구(CONV-01 needs, S15P31A305-931). 매 턴 워커에 보내고 돌발 부탁을 고르는 데 쓴다.
ALTER TABLE conversations ADD COLUMN npc_needs jsonb;
