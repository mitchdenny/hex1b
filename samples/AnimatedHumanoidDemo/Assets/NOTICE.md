# Robot Expressive

**Author:** [Tomas Laulhe / Quaternius](https://www.patreon.com/quaternius).
**License:** [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/).

This GLB is derived from the three.js example collection at commit
`121e626bb5d58ddd4f1d975010997f73688e665f`:

- [Model](https://github.com/mrdoob/three.js/blob/121e626bb5d58ddd4f1d975010997f73688e665f/examples/models/gltf/RobotExpressive/RobotExpressive.glb)
- [Upstream attribution and license declaration](https://github.com/mrdoob/three.js/blob/121e626bb5d58ddd4f1d975010997f73688e665f/examples/models/gltf/RobotExpressive/README.md)

Upstream modifications by [Don McCurdy](https://donmccurdy.com/): three facial
expression morph targets, FBX2GLTF conversion, removal of duplicate materials,
and reduced material metalness.

Local modification: normalize each exported `WEIGHTS_0` vector to sum to one.
The source includes non-unit joint weights and a weight slightly greater than
one; strict glTF validation rejects these. Geometry, topology, materials, and
animation tracks are otherwise unchanged.

Reproduce with `python3 samples/AnimatedHumanoidDemo/Assets/prepare-model.py`.
This downloads the pinned source, verifies its hash, and rewrites only the weight
values in the binary chunk. Python is not required to run the demo.

Upstream SHA-256: `047f5e5fb3bb6d378bd1df16ca6137f2a596c99b3a1b5690b4020c05aaf6f319`

Bundled SHA-256: `0e151c75035b88a162ef43a17ed4655ce48890922dc15fd245b40b795549a0b4`
