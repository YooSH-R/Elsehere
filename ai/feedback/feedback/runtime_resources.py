"""피드백 배포 자원 검사. 환경변수·키·모델·사용자 데이터를 읽지 않는다."""

import json
import re
from pathlib import Path

RESOURCES = ("prompts/feedback_system.ko.md", "content/spoken_usage.ko.json")


def valid_catalog(catalog: object) -> bool:
    if not isinstance(catalog, list) or not catalog:
        return False
    seen = set()
    for entry in catalog:
        if not isinstance(entry, dict):
            return False
        for field in ("catalog_id", "expression", "meaning_ko", "usage_ko", "example_en", "example_ko", "priority"):
            if not isinstance(entry.get(field), str) or not entry[field].strip():
                return False
        if entry["catalog_id"] in seen:
            return False
        seen.add(entry["catalog_id"])
        for field in ("sources", "patterns"):
            items = entry.get(field)
            if not isinstance(items, list) or not items or any(not isinstance(item, str) or not item.strip() for item in items):
                return False
        if any(not url.startswith("https://") for url in entry["sources"]):
            return False
        try:
            for pattern in entry["patterns"]:
                re.compile(pattern, re.IGNORECASE)
        except re.error:
            return False
    return True


def check_runtime_resources(root: Path) -> dict:
    issues = []
    for resource in RESOURCES:
        try:
            text = (root / resource).read_text(encoding="utf-8")
        except FileNotFoundError:
            issues.append({"resource": resource, "code": "missing"})
            continue
        except UnicodeError:
            issues.append({"resource": resource, "code": "invalid_utf8"})
            continue
        except OSError:
            issues.append({"resource": resource, "code": "unreadable"})
            continue
        code = None
        if not text.strip():
            code = "empty"
        elif resource.endswith(".json"):
            try:
                catalog = json.loads(text)
            except ValueError:
                code = "invalid_json"
            else:
                if not valid_catalog(catalog):
                    code = "invalid_catalog"
        if code:
            issues.append({"resource": resource, "code": code})
    return {"ready": not issues, "issues": issues}
