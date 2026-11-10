-- 택시 기사가 이 사용자 턴에서 알아들은 목적지. 첫 응답을 잃고 같은 요청을 다시 보내도 같은 목적지를 돌려준다.
ALTER TABLE turns
    ADD COLUMN taxi_destination_code varchar(40);
