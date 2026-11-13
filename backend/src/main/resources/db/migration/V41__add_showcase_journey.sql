CREATE TABLE showcase_journeys (
    user_id bigint NOT NULL PRIMARY KEY,
    stage integer NOT NULL,
    objectives integer NOT NULL,
    active boolean NOT NULL,
    umbrella_borrowed boolean NOT NULL,
    meal_paid boolean NOT NULL,
    taxi_requested boolean NOT NULL,
    scene varchar(20) NOT NULL,
    version bigint NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

ALTER TABLE conversations ADD COLUMN showcase_stage integer;
ALTER TABLE conversations ADD COLUMN showcase_goal_ids jsonb;
