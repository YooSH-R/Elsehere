import unittest
from copy import deepcopy

from dev_worker_network import validate_network, validate_worker_configuration


class FixedWorkerNetworkTests(unittest.TestCase):
    def setUp(self):
        self.network = {
            "Name": "a305-dev_default",
            "Driver": "bridge",
            "Internal": False,
            "Options": {"com.docker.network.bridge.name": "br-a305-dev"},
            "IPAM": {
                "Config": [
                    {
                        "Subnet": "172.19.0.0/16",
                        "Gateway": "172.19.0.1",
                        "IPRange": "172.19.128.0/17",
                    }
                ]
            },
            "Containers": {},
        }
        self.configuration = {
            "networks": {"default": {"name": "a305-dev_default", "external": True}},
            "services": {
                "ai-worker": {"networks": {"default": {"ipv4_address": "172.19.0.2"}}},
                "web": {"networks": {"default": {}}},
            },
        }

    def test_accepts_reserved_worker_address_and_separate_dynamic_pool(self):
        validate_network(self.network)
        validate_worker_configuration(self.configuration)

    def test_rejects_network_drift(self):
        for key, value in [
            ("Subnet", "172.20.0.0/16"),
            ("Gateway", "172.19.0.254"),
            ("IPRange", "172.19.0.0/16"),
        ]:
            with self.subTest(key=key):
                network = deepcopy(self.network)
                network["IPAM"]["Config"][0][key] = value
                with self.assertRaisesRegex(ValueError, "network"):
                    validate_network(network)

    def test_rejects_unstable_bridge_name(self):
        self.network["Options"] = {}
        with self.assertRaisesRegex(ValueError, "bridge"):
            validate_network(self.network)

    def test_rejects_worker_address_taken_by_another_container(self):
        self.network["Containers"]["web"] = {
            "Name": "a305-dev-web-1",
            "IPv4Address": "172.19.0.2/16",
        }
        with self.assertRaisesRegex(ValueError, "occupied"):
            validate_network(self.network)

    def test_accepts_existing_worker_at_reserved_address(self):
        self.network["Containers"]["worker"] = {
            "Name": "a305-dev-ai-worker-1",
            "IPv4Address": "172.19.0.2/16",
        }
        validate_network(self.network)

    def test_rejects_missing_static_worker_address(self):
        self.configuration["services"]["ai-worker"]["networks"]["default"] = {}
        with self.assertRaisesRegex(ValueError, "worker"):
            validate_worker_configuration(self.configuration)

    def test_rejects_compose_managed_network(self):
        self.configuration["networks"]["default"]["external"] = False
        with self.assertRaisesRegex(ValueError, "external"):
            validate_worker_configuration(self.configuration)

    def test_rejects_another_service_reserving_worker_address(self):
        self.configuration["services"]["web"]["networks"]["default"] = {
            "ipv4_address": "172.19.0.2"
        }
        with self.assertRaisesRegex(ValueError, "reserved"):
            validate_worker_configuration(self.configuration)
