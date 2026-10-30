"""Prepare the pinned duration-output Kokoro files before starting the worker."""

import argparse
import hashlib
import shutil
from pathlib import Path
from urllib.request import urlopen

RELEASE = "https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.1"
FILES = {
    "kokoro-v1.0.int8.onnx": "ae315a79b623f244700e4afb9246c46a26066782e049ba174bf3ba433970ee9c",
    "voices-v1.0.bin": "bca610b8308e8d99f32e6fe4197e7ec01679264efed0cac9140fe9c29f1fbf7d",
}


def sha256(path):
    with path.open("rb") as source:
        return hashlib.file_digest(source, "sha256").hexdigest()


def prepare_file(target, url, expected_sha256):
    if target.exists():
        if sha256(target) != expected_sha256:
            raise ValueError(f"SHA256 mismatch for existing {target.name}; choose a new model directory.")
        return
    target.parent.mkdir(parents=True, exist_ok=True)
    temporary = target.with_suffix(target.suffix + ".download")
    try:
        with urlopen(url, timeout=120) as response, temporary.open("wb") as output:
            shutil.copyfileobj(response, output)
        if sha256(temporary) != expected_sha256:
            raise ValueError(f"SHA256 mismatch for downloaded {target.name}")
        temporary.replace(target)
    finally:
        temporary.unlink(missing_ok=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--directory", type=Path, required=True)
    args = parser.parse_args()
    for name, digest in FILES.items():
        prepare_file(args.directory / name, f"{RELEASE}/{name}", digest)
        print(f"Verified {name}: {digest}")


if __name__ == "__main__":
    main()
