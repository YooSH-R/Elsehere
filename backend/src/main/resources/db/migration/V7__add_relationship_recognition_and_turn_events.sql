alter table user_npc_relations
    add column recognition smallint default 0 not null;

alter table turns
    add column events jsonb;
