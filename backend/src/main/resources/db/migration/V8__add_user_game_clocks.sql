create table user_game_clocks (
    user_id bigint not null,
    created_at timestamp(6) with time zone default now() not null,
    updated_at timestamp(6) with time zone default now() not null,
    last_beat_at timestamp(6) with time zone,
    play_seconds bigint default 0 not null,
    primary key (user_id)
);

alter table user_npc_relations
    add column last_talk_game_day integer;
