"""음성 조각 보관소 (/v1/audio/{id}): 보관 시간과 함께 전체 크기로도 제한한다 (코드 리뷰 #9: 턴 전체 음성까지 담으면 메모리가 약 두 배)."""

from app.tts import AudioStore


def test_oldest_audio_is_dropped_when_total_size_is_over():
    store = AudioStore(ttl_s=600, max_items=100, max_bytes=10)
    a, b, c = store.put(b"aaaa"), store.put(b"bbbb"), store.put(b"cccc")
    assert store.get(a) is None  # 12바이트 > 10 → 가장 오래된 것부터
    assert store.get(b) == b"bbbb" and store.get(c) == b"cccc"


def test_single_audio_bigger_than_limit_is_still_kept():
    """한 조각이 상한보다 커도 방금 넣은 것은 받을 수 있어야 한다 (대답이 끊기지 않게)."""
    store = AudioStore(ttl_s=600, max_items=100, max_bytes=3)
    big = store.put(b"too big")
    assert store.get(big) == b"too big"


def test_settings_limit_reaches_the_store(provider):
    from conftest import make_client

    with make_client(provider, audio_max_mb=1.5) as c:
        assert c.app.state.audio.max_bytes == 1_500_000
