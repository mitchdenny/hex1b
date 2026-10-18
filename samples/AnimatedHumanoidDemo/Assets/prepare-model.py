"""Reproduce the bundled GLB, normalizing the source's exported skin weights."""

import hashlib
import json
import math
from pathlib import Path
import struct
import urllib.request

URL = (
    "https://raw.githubusercontent.com/mrdoob/three.js/"
    "121e626bb5d58ddd4f1d975010997f73688e665f/"
    "examples/models/gltf/RobotExpressive/RobotExpressive.glb"
)
SOURCE_SHA256 = "047f5e5fb3bb6d378bd1df16ca6137f2a596c99b3a1b5690b4020c05aaf6f319"


def main():
    with urllib.request.urlopen(URL) as response:
        data = bytearray(response.read())
    if hashlib.sha256(data).hexdigest() != SOURCE_SHA256:
        raise ValueError("The upstream model does not match the pinned source hash.")
    json_length, json_type = struct.unpack_from("<II", data, 12)
    if json_type != 0x4E4F534A:
        raise ValueError("Expected a GLB JSON chunk.")
    model = json.loads(data[20:20 + json_length])
    binary_start = 20 + json_length + 8
    accessors = {
        primitive["attributes"]["WEIGHTS_0"]
        for mesh in model["meshes"]
        for primitive in mesh["primitives"]
        if "WEIGHTS_0" in primitive["attributes"]
    }
    for index in sorted(accessors):
        accessor = model["accessors"][index]
        view = model["bufferViews"][accessor["bufferView"]]
        if accessor["componentType"] != 5126 or accessor["type"] != "VEC4":
            raise ValueError("Expected four floating-point skin weights.")
        start = binary_start + view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
        stride = view.get("byteStride", 16)
        for vertex in range(accessor["count"]):
            offset = start + vertex * stride
            weights = struct.unpack_from("<4f", data, offset)
            total = sum(weights)
            if not all(math.isfinite(w) and w >= 0 for w in weights) or total <= 0:
                raise ValueError("Cannot normalize invalid skin weights.")
            struct.pack_into("<4f", data, offset, *(w / total for w in weights))
    destination = Path(__file__).with_name("RobotExpressive.glb")
    destination.write_bytes(data)
    print(f"{destination.name}: {hashlib.sha256(data).hexdigest()}")


if __name__ == "__main__":
    main()
