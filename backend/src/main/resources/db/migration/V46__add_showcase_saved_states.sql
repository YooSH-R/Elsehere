CREATE TABLE showcase_saved_states (
    user_id bigint NOT NULL,
    kind varchar(10) NOT NULL,
    play_seconds bigint,
    relations jsonb NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (kind, user_id)
);
