"""Receive protected dev CI releases through a restricted SSH command."""

import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tarfile
import tempfile
import time
from pathlib import Path
from urllib.parse import urlencode
from urllib.request import urlopen


def write_json(path, value):
    write_text(path, json.dumps(value, indent=2) + "\n")


def write_text(path, value):
    path.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    with tempfile.NamedTemporaryFile(
        mode="w", encoding="utf-8", dir=path.parent, delete=False
    ) as temporary:
        temporary.write(value)
        temporary_path = Path(temporary.name)
    temporary_path.chmod(0o600)
    os.replace(temporary_path, path)


def image_environment(images):
    names = {"backend": "BACKEND_IMAGE", "ai": "AI_IMAGE", "web": "WEB_IMAGE"}
    return "".join(
        f"{names[name]}={image['reference']}\n" for name, image in images.items()
    )


def receive_release(archive, directory):
    limits = {
        "manifest.json": 65536,
        "compose.app.yaml": 262144,
        "images.tar.gz": 2 * 1024**3,
    }
    received = set()
    with tarfile.open(fileobj=archive, mode="r|*") as bundle:
        for member in bundle:
            if (
                member.name not in limits
                or member.name in received
                or not member.isfile()
                or member.size > limits.get(member.name, 0)
            ):
                raise ValueError("Invalid deployment bundle member.")
            with (
                bundle.extractfile(member) as source,
                (directory / member.name).open("wb") as destination,
            ):
                shutil.copyfileobj(source, destination)
            (directory / member.name).chmod(0o600)
            received.add(member.name)
    if not {"manifest.json", "compose.app.yaml"}.issubset(received):
        raise ValueError("Deployment bundle is incomplete.")
    return json.loads((directory / "manifest.json").read_text(encoding="utf-8"))


class DeploymentServer:
    def __init__(self, config, runtime):
        self.config = config
        self.runtime = runtime
        self.directory = Path(config["directory"])

    def initialize(self):
        directory = Path(self.config["directory"])
        commits = [
            self.config["minimum_backend_commit"],
            *self.config["bootstrap_sources"].values(),
        ]
        if not all(re.fullmatch(r"[0-9a-f]{40}", commit) for commit in commits):
            raise ValueError(
                "Replace the bootstrap source commits with full verified SHAs."
            )
        environment_file = Path(self.config["environment_file"])
        if not os.access(environment_file, os.R_OK | os.W_OK):
            raise PermissionError(
                "The deployment owner must read and write deploy.env."
            )
        if os.name == "posix" and environment_file.stat().st_mode & 0o077:
            raise PermissionError("deploy.env must remain readable only by its owner.")
        if (directory / "bootstrap.json").exists() or (
            directory / "last-success.json"
        ).exists():
            raise ValueError("Deployment state is already initialized.")
        snapshot = self.runtime.snapshot()
        images = {
            name: {
                **image,
                "source_commit": self.config["bootstrap_sources"][name],
            }
            for name, image in snapshot["images"].items()
        }
        self.runtime.check(images)
        record = {
            "schema_version": 1,
            "candidate_commit": None,
            "pipeline_id": 0,
            "images": images,
            "compose_file": self.config["initial_compose_file"],
            "minimum_backend_commit": self.config["minimum_backend_commit"],
            "overlays": {
                filename: hashlib.sha256(Path(filename).read_bytes()).hexdigest()
                for filename in self.config["overlays"]
            },
        }
        write_json(directory / "bootstrap.json", record)
        return record

    def apply(self, archive):
        previous = self.state()
        release = Path(tempfile.mkdtemp(prefix="release-", dir=self.directory))
        image_archive = release / "images.tar.gz"
        try:
            manifest = receive_release(archive, release)
            previous = self.state()
            self.validate_manifest(manifest, previous)
            images = manifest["images"]
            images_file = release / "images.env"
            write_text(images_file, image_environment(images))
            previous_images_file = release / "previous-images.env"
            write_text(previous_images_file, image_environment(previous["images"]))
            compose_file = release / "compose.app.yaml"
            self.runtime.validate(compose_file, images_file, previous["compose_file"])
            if manifest["builds"]:
                if not image_archive.is_file():
                    raise ValueError("Built image archive is missing.")
                self.runtime.load_images(image_archive)

            service_names = {"backend": "backend", "ai": "ai-worker", "web": "web"}
            services = [service_names[name] for name in manifest["builds"]]
            if manifest["configuration_changed"]:
                services = ["ai-worker", "backend", "web"]
            environment_file = Path(self.config["environment_file"])
            original_environment = environment_file.read_text(encoding="utf-8")
            try:
                if services:
                    self.runtime.up(compose_file, images_file, services)
                self.runtime.check(images)
                replacement = original_environment.splitlines()
                for assignment in image_environment(images).splitlines():
                    name = assignment.split("=", 1)[0]
                    replacement = [
                        line for line in replacement if not line.startswith(name + "=")
                    ]
                    replacement.append(assignment)
                write_text(environment_file, "\n".join(replacement) + "\n")
                record = {
                    key: value
                    for key, value in previous.items()
                    if key != "state_token"
                }
                record.update(
                    candidate_commit=manifest["candidate_commit"],
                    pipeline_id=manifest["pipeline_id"],
                    images=images,
                    compose_file=str(compose_file),
                )
                write_json(self.directory / "last-success.json", record)
                return {"result": "deployed" if services else "unchanged", **record}
            except Exception as failure:
                failure_record = {
                    "candidate_commit": manifest["candidate_commit"],
                    "pipeline_id": manifest["pipeline_id"],
                    "restored": False,
                }
                try:
                    write_text(environment_file, original_environment)
                    if services:
                        self.runtime.up(
                            Path(previous["compose_file"]),
                            previous_images_file,
                            services,
                        )
                    self.runtime.check(previous["images"])
                except Exception as rollback_failure:
                    write_json(self.directory / "last-failure.json", failure_record)
                    raise RuntimeError(
                        "post-deploy check and rollback failed; manual recovery required"
                    ) from rollback_failure
                failure_record["restored"] = True
                write_json(self.directory / "last-failure.json", failure_record)
                raise RuntimeError(
                    "post-deploy check failed; previous release restored"
                ) from failure
        finally:
            image_archive.unlink(missing_ok=True)

    def state(self):
        path = self.directory / "last-success.json"
        if not path.exists():
            path = self.directory / "bootstrap.json"
        stored = json.loads(path.read_text(encoding="utf-8"))
        record = {
            key: stored[key]
            for key in [
                "schema_version",
                "candidate_commit",
                "pipeline_id",
                "images",
                "compose_file",
                "minimum_backend_commit",
                "overlays",
            ]
        }
        actual = self.runtime.snapshot()["images"]
        for name, image in record["images"].items():
            if any(actual[name][key] != image[key] for key in ["reference", "id"]):
                raise ValueError(
                    "Running images changed outside the deployment record."
                )
        record["overlays"] = {
            filename: hashlib.sha256(Path(filename).read_bytes()).hexdigest()
            for filename in self.config["overlays"]
        }
        record["minimum_backend_commit"] = self.config["minimum_backend_commit"]
        record["compose_digest"] = hashlib.sha256(
            Path(record["compose_file"]).read_bytes()
        ).hexdigest()
        record["state_token"] = hashlib.sha256(
            json.dumps(record, sort_keys=True).encode()
        ).hexdigest()
        return record

    def validate_manifest(self, manifest, previous):
        if manifest.get("schema_version") != 1 or not re.fullmatch(
            r"[0-9a-f]{40}", manifest.get("candidate_commit", "")
        ):
            raise ValueError("Invalid deployment candidate.")
        allowed_branches = {"dev"}
        if self.config.get("pilot_branch"):
            allowed_branches.add(self.config["pilot_branch"])
        if manifest.get("branch") not in allowed_branches:
            raise ValueError("Only dev or the configured protected pilot may deploy.")
        if (
            not isinstance(manifest.get("pipeline_id"), int)
            or manifest["pipeline_id"] <= previous["pipeline_id"]
            or manifest.get("expected_state_token") != previous["state_token"]
        ):
            raise ValueError("Deployment state changed or the pipeline is outdated.")
        builds = manifest.get("builds")
        if not isinstance(builds, list) or len(builds) != len(set(builds)):
            raise ValueError("Invalid image build selection.")
        if not set(builds).issubset({"backend", "ai", "web"}):
            raise ValueError("Unknown deployment service.")
        if manifest.get("minimum_backend_commit") != previous["minimum_backend_commit"]:
            raise ValueError("Monitoring compatibility requirement changed.")
        if not isinstance(manifest.get("configuration_changed"), bool):
            raise TypeError("Invalid configuration change flag.")
        images = manifest.get("images", {})
        if set(images) != {"backend", "ai", "web"}:
            raise ValueError("Exactly three application images are required.")
        for name, image in images.items():
            if name not in builds:
                if image != previous["images"][name]:
                    raise ValueError("An unchanged service image cannot be replaced.")
            elif (
                image.get("reference") != f"a305/{name}:{manifest['candidate_commit']}"
                or image.get("source_commit") != manifest["candidate_commit"]
                or not re.fullmatch(r"sha256:[0-9a-f]{64}", image.get("id", ""))
            ):
                raise ValueError("Built image does not match the deployment candidate.")


class ComposeRuntime:
    """Run only the dev app Compose while retaining the installed monitoring overlay."""

    def __init__(self, config):
        self.config = config
        self.health_after = time.time() - 45

    def docker(self, *arguments, timeout=240):
        result = subprocess.run(
            [
                *self.config.get("docker_command", ["sudo", "-n", "docker"]),
                *map(str, arguments),
            ],
            check=False,
            capture_output=True,
            timeout=timeout,
        )
        if result.returncode:
            # Compose configuration can contain credentials; never echo its raw output.
            raise RuntimeError(
                f"Docker {arguments[0]} failed; inspect dev containers locally."
            )
        return result.stdout.decode("utf-8")

    def compose(self, compose_file, images_file, *arguments):
        command = [
            "compose",
            "--project-name",
            "a305-dev",
            "--env-file",
            self.config["environment_file"],
            "--env-file",
            str(images_file),
            "--file",
            str(compose_file),
        ]
        for overlay in self.config["overlays"]:
            command.extend(["--file", overlay])
        return self.docker(*command, *arguments)

    def inspect(self, service):
        template = (
            '{"reference":{{json .Config.Image}},"id":{{json .Image}},'
            '"container_id":{{json .Id}},"health":{{json .State.Health.Status}},'
            '"mounts":{{json .Mounts}},"ports":{{json .HostConfig.PortBindings}}}'
        )
        return json.loads(
            self.docker("inspect", "--format", template, f"a305-dev-{service}-1")
        )

    def snapshot(self):
        images = {}
        containers = {}
        for image_name, service in [
            ("backend", "backend"),
            ("ai", "ai-worker"),
            ("web", "web"),
        ]:
            container = self.inspect(service)
            images[image_name] = {key: container[key] for key in ["reference", "id"]}
            containers[image_name] = container["container_id"]
        containers["database"] = self.inspect("database")["container_id"]
        return {"images": images, "containers": containers}

    def validate(self, compose_file, images_file, previous_compose_file):
        candidate = json.loads(
            self.compose(compose_file, images_file, "config", "--format", "json")
        )
        previous = json.loads(
            self.compose(
                previous_compose_file, images_file, "config", "--format", "json"
            )
        )
        services = candidate.get("services", {})
        if set(services) != {"database", "ai-worker", "backend", "web"}:
            raise ValueError(
                "Unexpected app services; monitoring is managed separately."
            )
        if services["database"] != previous["services"]["database"]:
            raise ValueError(
                "Database configuration changes require a separate migration."
            )
        for name, service in services.items():
            if service.get("privileged") or service.get("network_mode") == "host":
                raise ValueError(
                    "Privileged or host-network app containers are not accepted."
                )
            ports = service.get("ports", [])
            if name != "web" and ports:
                raise ValueError("Only the web service may publish a host port.")
            if name == "web" and (
                len(ports) != 1
                or ports[0].get("host_ip") != "127.0.0.1"
                or str(ports[0].get("published")) != "8081"
                or ports[0].get("target") != 80
            ):
                raise ValueError("Dev web must remain on loopback port 8081.")
            for volume in service.get("volumes", []):
                if volume.get("type") == "bind" and (
                    name != "web"
                    or volume.get("target") != "/etc/nginx/conf.d/00-observability.conf"
                    or not volume.get("read_only")
                ):
                    raise ValueError(
                        "Unexpected host mount in application configuration."
                    )
        current_mounts = {
            mount["Destination"]: mount["Source"]
            for mount in self.inspect("web")["mounts"]
            if mount["Type"] == "bind"
        }
        candidate_mounts = {
            volume["target"]: volume["source"]
            for volume in services["web"].get("volumes", [])
            if volume.get("type") == "bind"
        }
        if (
            "/etc/nginx/conf.d/00-observability.conf" not in candidate_mounts
            or candidate_mounts != current_mounts
        ):
            raise ValueError(
                "The installed monitoring web log mount must be preserved."
            )
        if (
            services["backend"].get("environment", {}).get("MANAGEMENT_ADDRESS")
            != "0.0.0.0"
        ):
            raise ValueError("The internal monitoring address must be preserved.")

    def load_images(self, archive):
        self.docker("load", "--input", archive, timeout=600)

    def up(self, compose_file, images_file, services):
        self.compose(
            compose_file,
            images_file,
            "up",
            "--detach",
            "--no-deps",
            "--wait",
            "--wait-timeout",
            "180",
            "--pull",
            "never",
            *services,
        )
        self.health_after = time.time()

    def check(self, images):
        last_error = None
        for attempt in range(45):
            try:
                self.check_once(images)
                return
            except (OSError, ValueError, RuntimeError) as error:
                last_error = error
                time.sleep(2)
        raise RuntimeError(
            "post-deploy application or monitoring check failed"
        ) from last_error

    def check_once(self, images):
        for name, service in [
            ("backend", "backend"),
            ("ai", "ai-worker"),
            ("web", "web"),
        ]:
            container = self.inspect(service)
            if container["health"] != "healthy" or any(
                container[key] != images[name][key] for key in ["reference", "id"]
            ):
                raise RuntimeError(
                    "Application health or image identity does not match."
                )
        if self.inspect("database")["health"] != "healthy":
            raise RuntimeError("Database is not healthy.")
        for url in [
            "http://127.0.0.1:8081/api/health",
            self.config["public_health_url"],
        ]:
            with urlopen(url, timeout=10) as response:
                if json.load(response) != {"status": "UP"}:
                    raise RuntimeError("Application/database health is not UP.")
        with urlopen("http://127.0.0.1:8081/version.json", timeout=10) as response:
            version = json.load(response)
        if (
            version.get("commit") != images["web"]["source_commit"]
            or version.get("build_type") != "unity-webgl"
        ):
            raise RuntimeError(
                "WebGL build metadata does not match the recorded source."
            )
        status = self.docker(
            "exec",
            "a305-dev-backend-1",
            "curl",
            "--silent",
            "--output",
            "/dev/null",
            "--write-out",
            "%{http_code}",
            "http://127.0.0.1:9090/actuator/prometheus",
        )
        if status.strip() != "401":
            raise RuntimeError("Monitoring endpoint must require authentication.")
        # Instant-query result timestamps are evaluation times, not scrape times.
        query = urlencode({"query": 'timestamp(up{job="elsehere-api"} == 1)'})
        with urlopen(
            f"http://127.0.0.1:9091/api/v1/query?{query}", timeout=10
        ) as response:
            samples = json.load(response).get("data", {}).get("result", [])
        if not samples or any(
            float(sample["value"][1]) < self.health_after for sample in samples
        ):
            raise RuntimeError("Prometheus is not collecting the dev backend.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", type=Path, required=True)
    parser.add_argument("--initialize", action="store_true")
    arguments = parser.parse_args()
    config = json.loads(arguments.config.read_text(encoding="utf-8"))
    directory = Path(config["directory"])
    directory.mkdir(parents=True, exist_ok=True, mode=0o700)
    server = DeploymentServer(config, ComposeRuntime(config))
    # The installed forced SSH command accepts no shell commands or file transfers.
    operation = (
        "initialize"
        if arguments.initialize
        else os.environ.get("SSH_ORIGINAL_COMMAND", "")
    )
    import fcntl

    with (directory / "deploy.lock").open("a") as lock:
        fcntl.flock(lock.fileno(), fcntl.LOCK_EX)
        if operation == "initialize":
            result = server.initialize()
        elif operation == "state":
            result = server.state()
        elif operation == "apply":
            result = server.apply(sys.stdin.buffer)
        else:
            raise ValueError("Only state and apply SSH operations are accepted.")
    print(json.dumps(result))


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, TypeError, RuntimeError, tarfile.TarError) as error:
        print(json.dumps({"error": str(error)}))
        sys.exit(1)
