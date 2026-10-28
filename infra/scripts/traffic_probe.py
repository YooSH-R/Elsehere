"""Probe only the disposable Nginx fixture on its private Docker network."""

import json
import re
import threading
import time
from concurrent.futures import ThreadPoolExecutor
from http.client import HTTPConnection
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

HOLD_STARTED = threading.Event()
RELEASE_HOLD = threading.Event()


class HoldingUpstream(BaseHTTPRequestHandler):
    def do_GET(self):
        HOLD_STARTED.set()
        if not RELEASE_HOLD.wait(timeout=10):
            self.send_error(504)
            return
        body = b'{"fixture":true}'
        self.send_response(200)
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, format, *arguments):
        pass


def request(path, port=80, headers=None, *, host="backend", method="GET", body=None):
    connection = HTTPConnection(host, port, timeout=15)
    try:
        connection.request(method, path, body=body, headers=headers or {})
        response = connection.getresponse()
        body = response.read()
        response_headers = {
            name.lower(): value for name, value in response.getheaders()
        }
        return response.status, body, response_headers
    finally:
        connection.close()


def check_limit_response(response):
    status, body, headers = response
    assert status == 429, status
    assert json.loads(body)["error"]["code"] == "TOO_MANY_REQUESTS", body
    assert "application/json" in headers["content-type"], headers
    assert re.fullmatch("[a-f0-9]{32}", headers["x-request-id"]), headers


def check_upload_limits():
    boundary = "a305-upload-check"
    prefix = (
        f"--{boundary}\r\n"
        'Content-Disposition: form-data; name="file"; filename="fixture.wav"\r\n'
        "Content-Type: audio/wav\r\n\r\n"
    ).encode()
    suffix = f"\r\n--{boundary}--\r\n".encode()
    normal = prefix + bytes(3 * 1024 * 1024) + suffix
    oversized = bytes(5 * 1024 * 1024 + 1)
    for host in ("backend", "app-web"):
        for body, expected_status in ((normal, 200), (oversized, 413)):
            # Bytes retain embedded NULs; string-based wget POST can truncate them.
            status, response_body, _ = request(
                "/api/upload",
                host=host,
                method="POST",
                body=body,
                headers={"Content-Type": f"multipart/form-data; boundary={boundary}"},
            )
            assert status == expected_status, (
                f"{host}: {len(body)} bytes expected {expected_status}, got {status}"
            )
            if expected_status == 200:
                assert json.loads(response_body)["content_length"] == str(len(body))
    print(
        "Upload proxies: binary 3 MiB audio multipart accepted with its full length; "
        "requests above 5 MiB rejected with 413 at each layer.",
        flush=True,
    )


def check_shared_ip_requests():
    start = threading.Barrier(50)

    def normal_request(index):
        start.wait(timeout=15)
        return request("/api/health")[0]

    # One client IP verifies the proxy policy, not backend/AI capacity for players.
    with ThreadPoolExecutor(max_workers=50) as executor:
        statuses = list(executor.map(normal_request, range(50)))
    assert statuses == [200] * 50, statuses


def check_rate_and_observation():
    statuses = []
    for _ in range(8):
        response = request("/api/rate", port=8083)
        statuses.append(response[0])
        if response[0] == 429:
            check_limit_response(response)
            break
    assert 200 in statuses and 429 in statuses, statuses
    time.sleep(2.1)
    assert request("/api/rate", port=8083)[0] == 200
    for _ in range(8):
        assert request("/api/observe", port=8083)[0] == 200

    status, body, headers = request("/api/upstream429", port=8083)
    assert status == 429
    assert json.loads(body)["error"]["code"] == "UPSTREAM_FIXTURE_LIMIT", body
    assert re.fullmatch("[a-f0-9]{32}", headers["x-request-id"]), headers


def check_connection_limit():
    upstream = ThreadingHTTPServer(("0.0.0.0", 8088), HoldingUpstream)
    server_thread = threading.Thread(target=upstream.serve_forever, daemon=True)
    server_thread.start()
    try:
        with ThreadPoolExecutor(max_workers=1) as executor:
            first = executor.submit(request, "/api/hold", 8083)
            try:
                assert HOLD_STARTED.wait(timeout=10), "Holding request did not arrive"
                check_limit_response(request("/api/hold", port=8083))
                check_limit_response(
                    request(
                        "/api/hold",
                        port=8083,
                        headers={"X-Forwarded-For": "203.0.113.10"},
                    )
                )
            finally:
                RELEASE_HOLD.set()
            assert first.result(timeout=15)[0] == 200
        assert request("/api/hold", port=8083)[0] == 200
    finally:
        upstream.shutdown()
        upstream.server_close()
        server_thread.join(timeout=5)


def main():
    check_upload_limits()
    check_shared_ip_requests()
    check_rate_and_observation()
    check_connection_limit()
    print(
        "Traffic fixture: 50 shared-IP requests, rate/connection rejection and "
        "recovery, dry-run acceptance, JSON/request IDs, upstream 429 and XFF passed."
    )


if __name__ == "__main__":
    main()
