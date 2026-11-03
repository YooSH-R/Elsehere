create table town_stories (
    user_id bigint not null,
    created_at timestamp(6) with time zone default now() not null,
    updated_at timestamp(6) with time zone default now() not null,
    stage integer not null,
    completed_goals integer not null,
    version bigint not null,
    primary key (user_id)
);

alter table conversations add column town_story_stage integer;
