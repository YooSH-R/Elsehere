"""Summarize safe Nginx request logs without exporting individual requests."""

import argparse
import json
import math
from collections import Counter, defaultdict, deque
from datetime import UTC, datetime, timedelta
from pathlib import Path

SAFE_ROUTES = {
    "/",
    "/api/health",
    "/api/**",
    "/api/v1/auth/**",
    "/webgl/**",
    "/unmatched",
}


def read_event(line):
    try:
        event = json.loads(line)
        if not isinstance(event, dict):
            return None
        environment = event.get("environment")
        route = event.get("route")
        status = event.get("status")
        duration = event.get("duration_seconds")
        completed_at = datetime.fromisoformat(event["time"])
        if environment not in {"dev", "prod"} or route not in SAFE_ROUTES:
            return None
        if type(status) is not int or not 100 <= status <= 599:
            return None
        if (
            type(duration) not in {int, float}
            or not math.isfinite(duration)
            or duration < 0
        ):
            return None
        if completed_at.utcoffset() is None:
            return None
        return environment, route, completed_at, status, duration
    except (ValueError, TypeError, KeyError):
        # Old access formats and partial lines must never appear in diagnostics.
        return None


def peak_completed(completed_seconds, window_seconds):
    window = deque()
    window_count = 0
    peak = 0
    for second, count in sorted(completed_seconds.items()):
        window.append((second, count))
        window_count += count
        while second - window[0][0] >= window_seconds:
            window_count -= window.popleft()[1]
        peak = max(peak, window_count)
    return peak


def summarize_group(environment, route, events):
    completed_seconds = Counter(second for second, _, _ in events)
    durations = sorted(duration for _, _, duration in events)
    statuses = Counter(status // 100 for _, status, _ in events)
    return {
        "environment": environment,
        "route": route,
        "requests": len(events),
        "peak_completed_in_1s": peak_completed(completed_seconds, 1),
        "peak_completed_in_10s": peak_completed(completed_seconds, 10),
        "responses_2xx_3xx": statuses[2] + statuses[3],
        "responses_4xx": statuses[4],
        "responses_5xx": statuses[5],
        "duration_p95_seconds": durations[math.ceil(len(durations) * 0.95) - 1],
        "duration_max_seconds": durations[-1],
    }


def summarize(lines, start, end):
    groups = defaultdict(list)
    unrecognized_lines = 0
    outside_window_lines = 0
    for line in lines:
        event = read_event(line)
        if event is None:
            unrecognized_lines += 1
            continue
        environment, route, completed_at, status, duration = event
        if not start <= completed_at < end:
            outside_window_lines += 1
            continue
        groups[(environment, route)].append(
            (int(completed_at.timestamp()), status, duration)
        )
    return {
        "window_start": start.isoformat(),
        "window_end": end.isoformat(),
        "unrecognized_lines": unrecognized_lines,
        "outside_window_lines": outside_window_lines,
        "groups": [
            summarize_group(environment, route, events)
            for (environment, route), events in sorted(groups.items())
        ],
    }


def main(arguments=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--log", type=Path, default=Path("/var/log/nginx/access.log"))
    parser.add_argument("--minutes", type=int, default=15)
    parser.add_argument("--end", type=datetime.fromisoformat)
    options = parser.parse_args(arguments)
    if not 1 <= options.minutes <= 1440:
        parser.error("--minutes must be between 1 and 1440")
    end = options.end or datetime.now(UTC)
    if end.utcoffset() is None:
        parser.error("--end must include a timezone")
    end = end.astimezone(UTC)
    start = end - timedelta(minutes=options.minutes)
    with options.log.open(encoding="utf-8", errors="replace") as lines:
        report = summarize(lines, start, end)
    print(json.dumps(report, indent=2, allow_nan=False))


if __name__ == "__main__":
    main()
