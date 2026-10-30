import hashlib
import importlib.util
import io
from pathlib import Path

import pytest

spec = importlib.util.spec_from_file_location("prepare_kokoro", Path(__file__).resolve().parents[2] / "tools/prepare_kokoro.py")
installer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(installer)


def test_verified_model_is_reused_without_network(tmp_path, monkeypatch):
    payload = b"verified test model"
    target = tmp_path / "model.onnx"
    target.write_bytes(payload)
    monkeypatch.setattr(installer, "urlopen", lambda *a, **k: pytest.fail("unexpected network"))
    installer.prepare_file(target, "https://example.test/model", hashlib.sha256(payload).hexdigest())
    assert target.read_bytes() == payload


def test_bad_download_is_never_published_as_model(tmp_path, monkeypatch):
    target = tmp_path / "model.onnx"
    monkeypatch.setattr(installer, "urlopen", lambda *a, **k: io.BytesIO(b"broken download"))
    with pytest.raises(ValueError, match="SHA256"):
        installer.prepare_file(target, "https://example.test/model", "0" * 64)
    assert not target.exists()
