from types import SimpleNamespace

import pytest

from conftest import make_client


def test_health_reports_phoneme_readiness_separately_from_voice(provider):
    for ready in (True, False):
        with make_client(provider, tts=SimpleNamespace(phoneme_timing_ready=ready)) as client:
            health = client.get("/health").json()
            assert health["tts"] is True
            assert health["tts_phoneme_timing"] is ready


def test_required_timing_blocks_startup_with_missing_or_legacy_model(provider):
    for voice in (None, SimpleNamespace(phoneme_timing_ready=False)):
        with pytest.raises(RuntimeError, match="phoneme timing"):
            with make_client(provider, tts=voice, tts_require_timing=True):
                pass


def test_required_timing_allows_verified_model(provider):
    with make_client(provider, tts=SimpleNamespace(phoneme_timing_ready=True), tts_require_timing=True) as client:
        assert client.get("/health").json()["tts_phoneme_timing"] is True
