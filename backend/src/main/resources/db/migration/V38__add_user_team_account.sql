-- 팀 기기(elsehere_team 쿠키를 남긴 브라우저)에서 쓴 계정. 어드민 이용 현황에서 뺀다
alter table users add column team_account boolean not null default false;
