"""Receive fixture notifications only inside the disposable validation network."""

import json
from http.server import BaseHTTPRequestHandler, HTTPServer
from typing import ClassVar


class NotificationReceiver(BaseHTTPRequestHandler):
    messages: ClassVar[list[dict]] = []

    def do_POST(self):
        size = int(self.headers["Content-Length"])
        self.messages.append(json.loads(self.rfile.read(size)))
        self.send_response(200)
        self.end_headers()
        self.wfile.write(b"ok")

    def do_GET(self):
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.end_headers()
        self.wfile.write(json.dumps(self.messages).encode())

    def log_message(self, *_):
        # Assertions read the received payloads; access logs add no useful evidence.
        return


if __name__ == "__main__":
    HTTPServer(("0.0.0.0", 8085), NotificationReceiver).serve_forever()
