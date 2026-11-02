using Avalonia;
using Avalonia.Media;
using GeoDraw.Geometry;

namespace GeoDraw.Models;

public enum DrawObjectType { Point, Path }

public sealed class DrawObject
{
    public required string Id { get; init; }
    public required DrawObjectType Type { get; init; }

    /// <summary>Type == Point 时有效。</summary>
    public Point Geo { get; set; }

    /// <summary>Type == Path 时有效。</summary>
    public PathData? Path { get; set; }

    public DrawStyle Style { get; set; } = new();

    private StreamGeometry? _geometryCache;
    private Rect? _boundsCache;

    /// <summary>Path 的渲染几何，缓存；Path/Style 变更后需 InvalidateRenderCache。</summary>
    public StreamGeometry? GetGeometry()
    {
        if (Type != DrawObjectType.Path || Path == null) return null;
        return _geometryCache ??= Path.ToGeometry();
    }

    /// <summary>包围盒缓存，供命中测试与旋转中心复用。</summary>
    public Rect GetBounds() =>
        _boundsCache ??= GeometryHelper.BoundingBox(this);

    /// <summary>几何与包围盒缓存失效，样式或路径变化后调用。</summary>
    public void InvalidateRenderCache()
    {
        _geometryCache = null;
        _boundsCache = null;
    }

    public DrawObject Clone() => new()
    {
        Id = Id,
        Type = Type,
        Geo = Geo,
        Path = Path,
        Style = Style.Clone()
    };
}
