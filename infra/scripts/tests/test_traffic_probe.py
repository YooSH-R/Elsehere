import hashlib
import threading
import unittest
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

import traffic_probe


class TrafficProbeTests(unittest.TestCase):
    def test_binary_post_preserves_nul_bytes_and_content_length(self):
        received = {}

        class UploadReceiver(BaseHTTPRequestHandler):
            def do_POST(self):
                received["length"] = int(self.headers["Content-Length"])
                body = self.rfile.read(received["length"])
                received["digest"] = hashlib.sha256(body).digest()
                self.send_response(200)
                self.send_header("Content-Length", "0")
                self.end_headers()

            def log_message(self, format, *arguments):
                pass

        upstream = ThreadingHTTPServer(("127.0.0.1", 0), UploadReceiver)
        server_thread = threading.Thread(target=upstream.serve_forever, daemon=True)
        server_thread.start()
        try:
            bodies = (
                b"multipart-prefix\r\n" + bytes(3 * 1024 * 1024) + b"\r\nsuffix",
                bytes(5 * 1024 * 1024 + 1),
            )
            for body in bodies:
                with self.subTest(length=len(body)):
                    status, _, _ = traffic_probe.request(
                        "/upload",
                        port=upstream.server_port,
                        host="127.0.0.1",
                        method="POST",
                        body=body,
                    )
                    self.assertEqual(status, 200)
                    self.assertEqual(received["length"], len(body))
                    self.assertEqual(received["digest"], hashlib.sha256(body).digest())
        finally:
            upstream.shutdown()
            upstream.server_close()
            server_thread.join(timeout=5)


if __name__ == "__main__":
    unittest.main()
