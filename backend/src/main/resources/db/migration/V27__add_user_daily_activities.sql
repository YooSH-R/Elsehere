-- 사용자가 장소 화면에 들어온 한국 날짜 하루(게임 시계 신호로 쌓음). 어드민 이용 현황의 활동한 날·머문 시간·재방문(D1·D7)에 쓴다.
create table user_daily_activities (
    activity_date date not null,
    user_id bigint not null,
    created_at timestamp(6) with time zone default now() not null,
    updated_at timestamp(6) with time zone default now() not null,
    first_seen_at timestamp(6) with time zone not null,
    last_seen_at timestamp(6) with time zone not null,
    active_seconds bigint default 0 not null,
    primary key (activity_date, user_id)
);
