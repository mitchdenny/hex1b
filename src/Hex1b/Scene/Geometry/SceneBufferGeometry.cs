namespace Hex1b.Scene.Geometry;

using Hex1b.Scene.Math;

/// <summary>
/// Represents the geometry of a mesh, storing vertex attributes (positions, normals, UVs, etc.)
/// and an optional index buffer for efficient topology representation.
/// </summary>
public class SceneBufferGeometry
{
    private Dictionary<string, SceneBufferAttribute> _attributes = new();
    private uint[]? _indices;

    public IReadOnlyDictionary<string, SceneBufferAttribute> Attributes => _attributes.AsReadOnly();
    public IReadOnlyList<uint>? Indices => _indices?.AsReadOnly();

    public int VertexCount
    {
        get
        {
            if (_attributes.Count == 0)
                return 0;
            return _attributes.Values.First().Count;
        }
    }

    public int PrimitiveCount
    {
        get
        {
            if (_indices != null)
                return _indices.Length / 3; // Assuming triangles
            return VertexCount / 3;
        }
    }

    /// <summary>
    /// Add or update a vertex attribute.
    /// </summary>
    public void SetAttribute(string name, SceneBufferAttribute attribute)
    {
        if (_attributes.Count > 0 && attribute.Count != VertexCount)
            throw new ArgumentException(
                $"Attribute count ({attribute.Count}) must match existing vertex count ({VertexCount})",
                nameof(attribute)
            );

        _attributes[name] = attribute;
    }

    /// <summary>
    /// Get a specific vertex attribute by name.
    /// </summary>
    public SceneBufferAttribute? GetAttribute(string name)
    {
        _attributes.TryGetValue(name, out var attr);
        return attr;
    }

    /// <summary>
    /// Check if a specific attribute exists.
    /// </summary>
    public bool HasAttribute(string name) => _attributes.ContainsKey(name);

    /// <summary>
    /// Set the index buffer for face topology.
    /// </summary>
    public void SetIndices(uint[] indices)
    {
        _indices = indices;
    }

    /// <summary>
    /// Get a face (triangle) by index.
    /// Returns the three vertex indices [i0, i1, i2].
    /// </summary>
    public (uint, uint, uint) GetFace(int faceIndex)
    {
        if (_indices == null)
        {
            var baseIdx = (uint)(faceIndex * 3);
            return (baseIdx, baseIdx + 1, baseIdx + 2);
        }

        var idx = faceIndex * 3;
        return (_indices[idx], _indices[idx + 1], _indices[idx + 2]);
    }

    /// <summary>
    /// Compute face normals if they don't exist.
    /// Assumes positions attribute exists with itemSize=3.
    /// </summary>
    public void ComputeVertexNormals()
    {
        var posAttr = GetAttribute("position");
        if (posAttr == null)
            throw new InvalidOperationException("Position attribute required to compute normals");

        var normals = new float[posAttr.Data.Length];

        // Accumulate face normals to vertices
        for (int f = 0; f < PrimitiveCount; f++)
        {
            var (i0, i1, i2) = GetFace(f);

            // Get positions
            var p0 = new Vector3(
                posAttr.GetComponent((int)i0, 0),
                posAttr.GetComponent((int)i0, 1),
                posAttr.GetComponent((int)i0, 2)
            );
            var p1 = new Vector3(
                posAttr.GetComponent((int)i1, 0),
                posAttr.GetComponent((int)i1, 1),
                posAttr.GetComponent((int)i1, 2)
            );
            var p2 = new Vector3(
                posAttr.GetComponent((int)i2, 0),
                posAttr.GetComponent((int)i2, 1),
                posAttr.GetComponent((int)i2, 2)
            );

            // Compute face normal
            var edge1 = p1 - p0;
            var edge2 = p2 - p0;
            var faceNormal = Vector3.Cross(edge1, edge2).Normalized;

            // Accumulate to all three vertices
            for (var i = 0; i < 3; i++)
            {
                var idx = (int)(i == 0 ? i0 : i == 1 ? i1 : i2);
                normals[idx * 3 + 0] += faceNormal.X;
                normals[idx * 3 + 1] += faceNormal.Y;
                normals[idx * 3 + 2] += faceNormal.Z;
            }
        }

        // Normalize all accumulated normals
        for (int i = 0; i < normals.Length; i += 3)
        {
            var nx = normals[i];
            var ny = normals[i + 1];
            var nz = normals[i + 2];
            var len = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
            if (len > float.Epsilon)
            {
                normals[i] /= len;
                normals[i + 1] /= len;
                normals[i + 2] /= len;
            }
        }

        SetAttribute("normal", new SceneBufferAttribute("normal", normals, 3));
    }
}
