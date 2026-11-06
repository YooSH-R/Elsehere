"""Keep a fully closed eyelid at 90% without duplicating an authored frame."""


def closure_frames(frames):
    result = list(frames)
    if not any(frame["weight"] == 90 for frame in frames):
        closed = next((frame for frame in frames if frame["weight"] == 100), None)
        if closed is not None:
            result.append(dict(closed, weight=90))
    return sorted(result, key=lambda frame: frame["weight"])
