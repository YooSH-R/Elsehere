#!/bin/sh
set -eu

# A downstream job may have downloaded another check's receipt with its artifacts.
rm -f dist/ci/success.json
if [ "${CI_JOB_STATUS:-}" != "success" ] || [ "${CI_PIPELINE_SOURCE:-}" != "merge_request_event" ]; then
    exit 0
fi

mkdir -p dist/ci
printf '{"schema_version":1,"commit":"%s","project_id":"%s","pipeline_id":"%s","job_id":"%s","job":"%s"}\n' \
    "$CI_COMMIT_SHA" "$CI_PROJECT_ID" "$CI_PIPELINE_ID" "$CI_JOB_ID" "$CI_JOB_NAME" \
    > dist/ci/success.json
