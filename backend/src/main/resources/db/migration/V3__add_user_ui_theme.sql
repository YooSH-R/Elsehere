-- 사용자가 집에서 고르는 화면 테마(USER-01). 계정에 저장해 다른 기기에서도 같은 테마로 보인다.
-- 이미 가입한 사용자는 기본 테마 JELLY로 채운다.
alter table users
    add column ui_theme varchar(20) default 'JELLY' not null
    constraint users_ui_theme_check check ((ui_theme in ('JELLY','STICKER','CLOUD')));
