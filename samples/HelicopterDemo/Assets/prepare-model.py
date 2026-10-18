"""Convert the pinned Zsky helicopter to simple colored triangles for Native AOT."""

from collections import defaultdict
import argparse
import hashlib
import io
import json
from pathlib import Path
import struct

URL = "https://static.poly.pizza/6f3cef79-867c-4b3c-9d97-346e465e4079.glb"
SOURCE_SHA256 = "08894cd4be8a628420657ad5c7fbceaa75a658450c33a757018c06a99c6fb4ae"


def convert(data):
    if hashlib.sha256(data).hexdigest() != SOURCE_SHA256:
        raise ValueError("The helicopter does not match the pinned source hash.")
    json_length = struct.unpack_from("<I", data, 12)[0]
    model = json.loads(data[20:20 + json_length])
    binary = memoryview(data)[28 + json_length:]

    def accessor(index, fmt):
        item = model["accessors"][index]
        view = model["bufferViews"][item["bufferView"]]
        offset = view.get("byteOffset", 0) + item.get("byteOffset", 0)
        stride = view.get("byteStride", struct.calcsize(fmt))
        return [struct.unpack_from(fmt, binary, offset + i * stride) for i in range(item["count"])]

    # The pinned FBX conversion has one mesh, rotated -90 degrees about X,
    # with uniform scale 100. Bake that transform and turn its nose toward -Z.
    node = model["nodes"][1]
    if node["mesh"] != 0 or node["scale"] != [100.0] * 3:
        raise ValueError("Unexpected source hierarchy.")
    groups = defaultdict(list)
    for primitive in model["meshes"][0]["primitives"]:
        positions = [(-x * 100, z * 100, y * 100)
                     for x, y, z in accessor(primitive["attributes"]["POSITION"], "<3f")]
        index_type = model["accessors"][primitive["indices"]]["componentType"]
        indices = [v[0] for v in accessor(primitive["indices"], {5123: "<H", 5125: "<I"}[index_type])]
        material_index = primitive["material"]
        material_name = model["materials"][material_index]["name"]
        for i in range(0, len(indices), 3):
            vertices = [positions[index] for index in indices[i:i + 3]]
            kind = 0
            if material_name == "Gray_Aircraft" and min(v[1] for v in vertices) > 5.2:
                kind = 1
            elif material_name == "Black_Aircraft" and min(v[2] for v in vertices) > 18.9:
                kind = 2
            groups[kind, material_index].extend(vertices)

    all_positions = [p for group in groups.values() for p in group]

    def center(positions):
        return tuple((min(p[i] for p in positions) + max(p[i] for p in positions)) / 2 for i in range(3))

    rotor_positions = {kind: [p for (k, _), group in groups.items() if k == kind for p in group]
                       for kind in (1, 2)}
    if len(rotor_positions[1]) != 48 * 3 or len(rotor_positions[2]) != 36 * 3:
        raise ValueError("Unexpected rotor geometry.")
    main_center = center(rotor_positions[1])
    origin = (main_center[0], min(p[1] for p in all_positions), main_center[2])
    span = max(max(p[i] for p in all_positions) - min(p[i] for p in all_positions) for i in (0, 2))
    scale = 7 / span
    pivots = {0: origin, 1: main_center, 2: center(rotor_positions[2])}
    output = io.BytesIO()
    output.write(struct.pack("<4sI", b"H1M1", len(groups)))
    for (kind, material_index), positions in sorted(groups.items()):
        color = model["materials"][material_index]["pbrMetallicRoughness"]["baseColorFactor"][:3]
        color = [12.92 * c if c <= 0.0031308 else 1.055 * c ** (1 / 2.4) - 0.055 for c in color]
        pivot = pivots[kind]
        offset = [(pivot[i] - origin[i]) * scale for i in range(3)]
        output.write(struct.pack("<I3f3fI", kind, *color, *offset, len(positions)))
        for position in positions:
            output.write(struct.pack("<3f", *((position[i] - pivot[i]) * scale for i in range(3))))
    return output.getvalue()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path, help=f"GLB downloaded from {URL}")
    args = parser.parse_args()
    output = convert(args.source.read_bytes())
    destination = Path(__file__).with_name("Helicopter.mesh")
    destination.write_bytes(output)
    print(f"{destination.name}: {len(output)} bytes, SHA-256 {hashlib.sha256(output).hexdigest()}")


if __name__ == "__main__":
    main()
