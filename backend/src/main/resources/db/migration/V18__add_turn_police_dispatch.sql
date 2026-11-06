ALTER TABLE turns
    ADD COLUMN police_dispatch boolean NOT NULL DEFAULT false,
    ADD COLUMN police_dispatch_place_code varchar(40);
