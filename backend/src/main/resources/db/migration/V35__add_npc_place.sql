-- 단어장에서 대화 표현을 장소로 나누려고 NPC가 일하는 곳과 그 묶음을 둔다. 값은 시드(world.sql)가 채운다.
alter table npcs
    add column place_name varchar(30);

alter table npcs
    add column place_group varchar(20);

alter table npcs
    add constraint npcs_place_group_check
    check (place_group in ('FOOD', 'SHOPPING', 'SERVICES', 'SAFETY', 'DAILY'));
