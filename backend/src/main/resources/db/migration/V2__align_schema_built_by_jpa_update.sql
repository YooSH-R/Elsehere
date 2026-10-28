-- Flyway 도입 전에 ddl-auto=update로 만든 DB(dev, 팀원 로컬)에는 update가 고치지 않는 차이가 남아 있다.
-- 그런 DB는 V1로 인정(baseline)한 뒤 이 파일로 V1과 같게 맞춘다. V1로 만든 새 DB에서는 바뀌는 것이 없다.

-- enum 값이 늘어도 update는 이미 만든 CHECK 제약을 고치지 않는다.
alter table users drop constraint if exists users_provider_check;
alter table users
    add constraint users_provider_check check ((provider in ('GOOGLE','GUEST')));

alter table ai_requests drop constraint if exists ai_requests_kind_check;
alter table ai_requests
    add constraint ai_requests_kind_check
    check ((kind in ('STT','GUARD','JUDGE','NPC','TTS','HINT','MEANING','FEEDBACK','MEMORY')));

-- 장소별 사물 단어(scene_words, user_word_prefs)로 바꾸면서 엔티티에서 없앤 테이블이다. update는 테이블을 지우지 않는다.
drop table if exists user_object_prefs;
drop table if exists world_objects;
