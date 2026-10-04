CREATE DATABASE IF NOT EXISTS analytics_dev;
CREATE DATABASE IF NOT EXISTS analytics_stage;
CREATE DATABASE IF NOT EXISTS analytics_prod;

CREATE TABLE IF NOT EXISTS analytics_dev.events
(
    insert_id String,
    event_type LowCardinality(String),
    event_time DateTime64(3, 'UTC'),
    server_time DateTime64(3, 'UTC'),
    user_id String,
    device_id String,
    session_id Int64,
    platform LowCardinality(String),
    app_version LowCardinality(String),
    country LowCardinality(String),
    experiment_id LowCardinality(String),
    group_id LowCardinality(String),
    config_version LowCardinality(String),
    source LowCardinality(String),
    event_properties String CODEC(ZSTD(3)),
    user_properties String CODEC(ZSTD(3))
)
ENGINE = ReplacingMergeTree(server_time)
PARTITION BY toYYYYMM(event_time)
ORDER BY (event_type, toDate(event_time), user_id, insert_id)
TTL toDateTime(event_time) + INTERVAL 400 DAY;

CREATE TABLE IF NOT EXISTS analytics_stage.events AS analytics_dev.events;
CREATE TABLE IF NOT EXISTS analytics_prod.events AS analytics_dev.events;
