-- 기존 완료 내역을 새 알림으로 한꺼번에 세지 않고, 이후 완성되는 피드백부터 미확인으로 센다.
ALTER TABLE conversation_feedback ADD COLUMN read_at timestamptz;
UPDATE conversation_feedback SET read_at = created_at WHERE status = 'DONE';
