"""Read FBX material bindings in an isolated copy; write Unity-ready textures only."""

import hashlib
import json
from pathlib import Path
import sys
import bpy


def image_node(socket):
    if not socket.is_linked:
        return None
    node = socket.links[0].from_node
    return node if node.type == "TEX_IMAGE" else None


def image_hash(image):
    if image.packed_file:
        return hashlib.sha256(image.packed_file.data).hexdigest()
    path = Path(bpy.path.abspath(image.filepath))
    return hashlib.sha256(path.read_bytes()).hexdigest() if path.is_file() else None


def extract(source, destination):
    destination = Path(destination)
    destination.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source), use_anim=False)
    entries, warnings = [], []
    for index, material in enumerate(bpy.data.materials):
        if not material.use_nodes:
            raise ValueError(f"Unsupported material: {material.name}")
        shader = next((n for n in material.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if shader is None:
            raise ValueError(f"No principled material: {material.name}")
        color = image_node(shader.inputs["Base Color"])
        normal = None
        if shader.inputs["Normal"].is_linked:
            normal_map = shader.inputs["Normal"].links[0].from_node
            if normal_map.type == "NORMAL_MAP":
                normal = image_node(normal_map.inputs["Color"])
        if color and normal and color.image and normal.image:
            color_hash, normal_hash = image_hash(color.image), image_hash(normal.image)
            if color_hash and color_hash == normal_hash:
                warnings.append(material.name + ": 색상 이미지와 동일한 노멀 연결을 자동으로 해제했습니다.")
                normal = None
        entry = {"name": material.name, "color": "", "normal": ""}
        for channel, node in (("color", color), ("normal", normal)):
            if node is None:
                continue
            if node.image is None or not node.image.has_data or min(node.image.size) == 0:
                raise ValueError(f"Missing {channel} image: {material.name}")
            filename = f"Material_{index}_{channel}.png"
            node.image.filepath_raw = str(destination / filename)
            node.image.file_format = "PNG"
            node.image.save()
            entry[channel] = filename
        entries.append(entry)
    (destination / "materials.json").write_text(
        json.dumps({"materials": entries, "warnings": warnings}, indent=2),
        encoding="utf-8",
    )


if __name__ == "__main__":
    source, destination = sys.argv[sys.argv.index("--") + 1:]
    extract(Path(source).resolve(), Path(destination).resolve())
