-- 근무 시간이 아닌 NPC와의 대화(CONV-01 npcOffDuty). 워커에 응대 상황 대신 일상 상황을 보낸다.
-- 이전 대화는 모두 근무 중인 NPC와 나눈 대화이므로 false로 채운다.
ALTER TABLE conversations ADD COLUMN npc_off_duty boolean DEFAULT false NOT NULL;
