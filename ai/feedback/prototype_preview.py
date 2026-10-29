"""별도 Maple Town 복사본에서 GPT-5.4 피드백을 플레이로 검증한다."""

import argparse
import importlib.util
import os
import sys
from pathlib import Path
from urllib.parse import urlsplit

from run_gms import load_settings

from feedback.gms import GmsClient
from feedback.prototype_bridge import generate_prototype_feedback

ROOT = Path(__file__).resolve().parent


def render_preview_html(source, voice_available):
    marker = "function openFeedback(r) {"
    if source.count(marker) != 1:
        raise ValueError("프로토타입의 피드백 화면 구조가 달라 연결 위치를 확인해야 합니다.")
    source = source.replace(marker, marker + '\n  if (r?.feedback?.schema_version === "0.3.0") { renderFeedbackV3(r); return; }', 1)
    source = source.replace(
        "const SET = (() =>",
        'SETTINGS_DEF.translate.options = [["off", "검증판에서 끔"]];\nSETTINGS_DEF.suggest.options = [["off", "검증판에서 끔"]];\nconst SET = (() =>',
        1,
    )
    source = source.replace("function applySettings() {", 'function applySettings() {\n  SET.translate = "off"; SET.suggest = "off";', 1)
    source = source.replace(
        "function setMicText(state, msg) {",
        'function setMicText(state, msg) {\n  if (!PREVIEW_VOICE_AVAILABLE) { $("mic").disabled = true; $("micText").textContent = "지금은 타자로 테스트하세요. 음성은 본인 Groq 키 설정 후 사용할 수 있어요."; return; }',
        1,
    )
    source = source.replace("function toggleMic() {", "function toggleMic() {\n  if (!PREVIEW_VOICE_AVAILABLE) return;", 1)
    banner = '<aside style="position:fixed;top:12px;left:50%;transform:translateX(-50%);z-index:10;background:#fff8e7;color:#403321;padding:12px 16px;border-radius:14px;box-shadow:0 3px 15px #0002;font:14px sans-serif;max-width:420px">피드백 검증판 · 피드백 GPT-5.4<br><small>수집용과 별도 저장 · 추천·번역 끔</small><br><button id="previewCafe" style="margin-top:8px;padding:8px">카페 Emma와 대화</button></aside>'
    banner = banner.replace(
        "</aside>", '<br><a href="/feedback-demo" target="_blank" rel="noopener">과금 없는 피드백 표시 예시</a></aside>'
    )
    source = source.replace("<body>", "<body>" + banner, 1)
    panel = f"\nconst PREVIEW_VOICE_AVAILABLE = {'true' if voice_available else 'false'};\n" + (ROOT / "prototype_panel.js").read_text(
        encoding="utf-8"
    )
    ending = source.rfind("</script>")
    return source[:ending] + panel + source[ending:]


def prepare_server(prototype_root, settings):
    path = prototype_root / "prototype/mvp_web/server.py"
    spec = importlib.util.spec_from_file_location("maple_feedback_preview", path)
    server = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(server)
    # NPC·판정·기억은 프로토타입 기본 구성, 피드백만 별도 GPT-5.4 어댑터.
    server.use_profile(server.PROFILES["default_profile"], {"feedback": settings.model})
    server.PROVIDERS["gms"]["key"] = settings.api_key
    server.API_KEY = settings.api_key
    server.TTS_CHECK["auto"] = False
    voice_available = bool(server.PROVIDERS.get("groq", {}).get("key"))
    server.feedback = lambda conv, outcome: generate_prototype_feedback(
        conv, server.NPCS[conv["npc"]], server.SCENARIOS[conv["scenario"]], GmsClient(settings)
    )
    original_add_turn = server.add_turn

    def add_turn(conv, text, judgement, reply):
        original_add_turn(conv, text, judgement, reply)
        conv["history"][-2]["input_mode"] = conv.pop("_preview_input_mode", "text")

    server.add_turn = add_turn
    source = (path.parent / "static/index.html").read_text(encoding="utf-8")
    html = render_preview_html(source, voice_available).encode("utf-8")

    class PreviewHandler(server.Handler):
        def do_GET(self):
            if urlsplit(self.path).path == "/feedback-demo":
                demo = ROOT / "local_results/spoken_usage_preview.html"
                if not demo.is_file():
                    self.send_error(404, "Run render_spoken_examples.py first")
                    return
                content = demo.read_bytes()
                self.send_response(200)
                self.send_header("Content-Type", "text/html; charset=utf-8")
                self.send_header("Content-Length", str(len(content)))
                self.end_headers()
                self.wfile.write(content)
                return
            if urlsplit(self.path).path in ("/", "/index.html"):
                self.send_response(200)
                self.send_header("Content-Type", "text/html; charset=utf-8")
                self.send_header("Content-Length", str(len(html)))
                self.end_headers()
                self.wfile.write(html)
                return
            super().do_GET()

        def do_POST(self):
            if self.path in ("/api/talk/hint", "/api/talk/npc_ko"):
                return self._json(409, {"error": "현재 피드백 검증판은 추천·번역을 끈 상태입니다."})
            super().do_POST()

        def _turn(self, uid, conv, text, req=None):
            conv["_preview_input_mode"] = (req or {}).get("input_mode", "text")
            return super()._turn(uid, conv, text, req)

        def _stt(self, uid, audio, mime):
            if not voice_available:
                return self._json(503, {"error": "음성 인식용 본인 Groq 키가 없습니다. 지금은 타자로 테스트하세요."})
            return super()._stt(uid, audio, mime)

    return server, PreviewHandler, voice_available


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--prototype-root", type=Path, required=True)
    parser.add_argument("--env-file", type=Path, default=ROOT.parents[1] / ".env.local")
    parser.add_argument("--port", type=int, default=8011)
    args = parser.parse_args()
    settings = load_settings(args.env_file)
    if not settings.api_key or settings.model != "gpt-5.4":
        parser.error("로컬 GMS 키와 GMS_MODEL=gpt-5.4 설정이 필요합니다.")
    if args.env_file.is_file():
        for line in args.env_file.read_text(encoding="utf-8-sig").splitlines():
            name, separator, value = line.strip().partition("=")
            if separator and name in ("GROQ_API_KEY", "GROK_API_KEY"):
                os.environ.setdefault(name, value.strip().strip("\"'"))
    server, handler, voice_available = prepare_server(args.prototype_root.resolve(), settings)
    print(
        f"피드백 검증판: http://localhost:{args.port}/?at=cafe&play=normal · GPT-5.4 · 음성 {'가능' if voice_available else '미설정: 타자 사용'}",
        flush=True,
    )
    server.ThreadingHTTPServer(("127.0.0.1", args.port), handler).serve_forever()


if __name__ == "__main__":
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8")
    main()
