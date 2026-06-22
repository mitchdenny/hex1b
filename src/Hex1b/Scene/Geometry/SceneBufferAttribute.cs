namespace Hex1b.Scene.Geometry;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Represents a vertex attribute (positions, normals, UVs, colors, etc.)
/// stored in a typed array for efficient access during rendering.
/// </summary>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public class SceneBufferAttribute
{
    public string Name { get; }
    public float[] Data { get; }
    public int ItemSize { get; }
    public int Count => Data.Length / ItemSize;

    public SceneBufferAttribute(string name, float[] data, int itemSize)
    {
        if (data.Length % itemSize != 0)
            throw new ArgumentException($"Data length must be divisible by itemSize ({itemSize})", nameof(data));

        Name = name;
        Data = data;
        ItemSize = itemSize;
    }

    public SceneBufferAttribute(string name, int itemSize, int count)
        : this(name, new float[itemSize * count], itemSize)
    {
    }

    /// <summary>
    /// Get the value at the specified item index.
    /// Returns an array of ItemSize floats.
    /// </summary>
    public float[] GetItem(int index)
    {
        var startIdx = index * ItemSize;
        var item = new float[ItemSize];
        Array.Copy(Data, startIdx, item, 0, ItemSize);
        return item;
    }

    /// <summary>
    /// Set the value at the specified item index.
    /// </summary>
    public void SetItem(int index, float[] values)
    {
        if (values.Length != ItemSize)
            throw new ArgumentException($"Value array must have length {ItemSize}", nameof(values));
        
        var startIdx = index * ItemSize;
        Array.Copy(values, 0, Data, startIdx, ItemSize);
    }

    /// <summary>
    /// Get a single component at the specified item and component index.
    /// </summary>
    public float GetComponent(int itemIndex, int componentIndex)
    {
        if (componentIndex < 0 || componentIndex >= ItemSize)
            throw new ArgumentOutOfRangeException(nameof(componentIndex));
        return Data[itemIndex * ItemSize + componentIndex];
    }

    /// <summary>
    /// Set a single component at the specified item and component index.
    /// </summary>
    public void SetComponent(int itemIndex, int componentIndex, float value)
    {
        if (componentIndex < 0 || componentIndex >= ItemSize)
            throw new ArgumentOutOfRangeException(nameof(componentIndex));
        Data[itemIndex * ItemSize + componentIndex] = value;
    }
}
