using Avalonia;

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

    public DrawObject Clone() => new()
    {
        Id = Id,
        Type = Type,
        Geo = Geo,
        Path = Path,
        Style = Style.Clone()
    };
}
