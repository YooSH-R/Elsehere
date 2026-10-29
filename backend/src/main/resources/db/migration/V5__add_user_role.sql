-- 운영자 어드민(/admin)을 쓸 수 있는 관리자 역할. 이미 가입한 사용자와 새 가입자는 일반 사용자(USER)다.
-- 관리자는 운영자가 DB에서 직접 지정한다(docs/admin.md).
alter table users
    add column role varchar(10) default 'USER' not null
    constraint users_role_check check ((role in ('USER','ADMIN')));
