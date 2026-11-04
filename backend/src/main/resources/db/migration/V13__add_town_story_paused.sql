-- 마을 이야기는 퀘스트 하나를 끝내면 다음 퀘스트를 시작할 때까지 멈춘다.
alter table town_stories add column paused boolean not null default false;
