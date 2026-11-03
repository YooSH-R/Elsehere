"""Provision and validate the development worker's GPU tunnel network."""

import argparse
import json
import subprocess

NETWORK_NAME = "a305-dev_default"
WORKER_ADDRESS = "172.19.0.2"
BRIDGE_NAME = "br-a305-dev"
NETWORK_SUBNET = "172.19.0.0/16"
NETWORK_GATEWAY = "172.19.0.1"
DYNAMIC_RANGE = "172.19.128.0/17"


def validate_network(network):
    expected = {
        "Subnet": NETWORK_SUBNET,
        "Gateway": NETWORK_GATEWAY,
        "IPRange": DYNAMIC_RANGE,
    }
    configurations = network.get("IPAM", {}).get("Config", [])
    if (
        network.get("Name") != NETWORK_NAME
        or network.get("Driver") != "bridge"
        or network.get("Internal") is not False
        or len(configurations) != 1
        or any(configurations[0].get(key) != value for key, value in expected.items())
    ):
        raise ValueError(
            "Development worker network subnet, gateway or allocation range differs."
        )
    if network.get("Options", {}).get("com.docker.network.bridge.name") != BRIDGE_NAME:
        raise ValueError("Development GPU firewall requires the stable bridge name.")
    for container in network.get("Containers", {}).values():
        address = container.get("IPv4Address", "").split("/")[0]
        if (
            address == WORKER_ADDRESS
            and container.get("Name") != "a305-dev-ai-worker-1"
        ):
            raise ValueError(
                "The reserved worker address is occupied by another container."
            )


def validate_worker_configuration(configuration):
    network = configuration.get("networks", {}).get("default", {})
    if network.get("name") != NETWORK_NAME or network.get("external") is not True:
        raise ValueError("Development worker requires the persistent external network.")
    services = configuration.get("services", {})
    worker_networks = services.get("ai-worker", {}).get("networks", {})
    if (
        set(worker_networks) != {"default"}
        or worker_networks["default"].get("ipv4_address") != WORKER_ADDRESS
    ):
        raise ValueError("Development worker must reserve its fixed IPv4 address.")
    for name, service in services.items():
        address = (service.get("networks", {}).get("default") or {}).get("ipv4_address")
        if name != "ai-worker" and address == WORKER_ADDRESS:
            raise ValueError(
                "The worker address is reserved exclusively for ai-worker."
            )


def docker(*arguments):
    result = subprocess.run(
        ["sudo", "-n", "docker", *arguments],
        check=False,
        capture_output=True,
        text=True,
        timeout=30,
    )
    if result.returncode:
        raise RuntimeError("Docker network operation failed; inspect it on the server.")
    return result.stdout


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--create",
        action="store_true",
        help="Create only if absent; never replace an existing network.",
    )
    arguments = parser.parse_args()
    names = docker("network", "ls", "--format", "{{.Name}}").splitlines()
    if NETWORK_NAME not in names:
        if not arguments.create:
            raise ValueError("Development worker network is missing.")
        docker(
            "network",
            "create",
            "--driver",
            "bridge",
            "--subnet",
            NETWORK_SUBNET,
            "--gateway",
            NETWORK_GATEWAY,
            "--ip-range",
            DYNAMIC_RANGE,
            "--opt",
            "com.docker.network.bridge.name=" + BRIDGE_NAME,
            NETWORK_NAME,
        )
    validate_network(json.loads(docker("network", "inspect", NETWORK_NAME))[0])
    print("Development worker network settings verified.")


if __name__ == "__main__":
    main()
