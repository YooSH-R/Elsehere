"""Build application images with optional caches outside disposable Docker daemons."""

import tempfile
from pathlib import Path


def build_image(
    docker,
    repository,
    image_name,
    image_reference,
    dockerfile,
    *,
    build_arguments=None,
    labels=None,
    cache_directory=None,
):
    arguments = ["--tag", image_reference, "--file", dockerfile]
    for key, value in (build_arguments or {}).items():
        arguments.extend(["--build-arg", f"{key}={value}"])
    for key, value in (labels or {}).items():
        arguments.extend(["--label", f"{key}={value}"])
    if not cache_directory:
        docker("build", *arguments, repository)
        return

    cache_root = Path(cache_directory)
    cache_root.mkdir(parents=True, exist_ok=True)
    saved_cache = cache_root / image_name
    with tempfile.TemporaryDirectory(prefix=f".{image_name}-", dir=cache_root) as work:
        exported_cache = Path(work) / "cache"
        arguments = ["buildx", "build", "--load", *arguments]
        if (saved_cache / "index.json").is_file():
            arguments.extend(["--cache-from", f"type=local,src={saved_cache}"])
        arguments.extend(
            [
                "--cache-to",
                f"type=local,dest={exported_cache},mode=max",
                repository,
            ]
        )
        docker(*arguments)

        # Publish only a completed export; dropping the old directory bounds disk use.
        previous_cache = Path(work) / "previous"
        if saved_cache.exists():
            saved_cache.rename(previous_cache)
        try:
            exported_cache.rename(saved_cache)
        except OSError:
            if previous_cache.exists():
                previous_cache.rename(saved_cache)
            raise
