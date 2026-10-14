using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;

namespace GeoDraw.Models;

public enum PathCommand { MoveTo, LineTo, QuadTo, CubicTo, Close }

public sealed class PathSegment
{
    public required PathCommand Command { get; init; }
    public required Point[] Points { get; init; }
}

public sealed class PathData
{
    public List<PathSegment> Segments { get; } = new();

    private List<Point>? _pointsCache;

    public void InvalidateCache() => _pointsCache = null;

    /// <summary>采样成折线点，用于命中测试和包围盒。</summary>
    public IReadOnlyList<Point> ToPoints()
    {
        if (_pointsCache != null) return _pointsCache;

        var pts = new List<Point>();
        Point cur = default, start = default;
        var hasStart = false;

        foreach (var seg in Segments)
        {
            switch (seg.Command)
            {
                case PathCommand.MoveTo:
                    cur = seg.Points[0];
                    start = cur;
                    hasStart = true;
                    pts.Add(cur);
                    break;
                case PathCommand.LineTo:
                    cur = seg.Points[0];
                    pts.Add(cur);
                    break;
                case PathCommand.QuadTo:
                {
                    var (ctrl, end) = (seg.Points[0], seg.Points[1]);
                    AppendQuad(pts, cur, ctrl, end, 15);
                    cur = end;
                    break;
                }
                case PathCommand.CubicTo:
                {
                    var (c1, c2, end) = (seg.Points[0], seg.Points[1], seg.Points[2]);
                    AppendCubic(pts, cur, c1, c2, end, 20);
                    cur = end;
                    break;
                }
                case PathCommand.Close:
                    if (hasStart)
                    {
                        pts.Add(start);
                        cur = start;
                    }
                    break;
            }
        }

        _pointsCache = pts;
        return pts;
    }

    /// <summary>转换为 Avalonia 的 StreamGeometry，用于实际渲染。</summary>
    public StreamGeometry ToGeometry()
    {
        var geo = new StreamGeometry();
        using var ctx = geo.Open();

        var open = false;
        foreach (var seg in Segments)
        {
            switch (seg.Command)
            {
                case PathCommand.MoveTo:
                    if (open) { ctx.EndFigure(false); }
                    ctx.BeginFigure(seg.Points[0], false);
                    open = true;
                    break;
                case PathCommand.LineTo:
                    if (!open) { ctx.BeginFigure(seg.Points[0], false); open = true; }
                    else { ctx.LineTo(seg.Points[0]); }
                    break;
                case PathCommand.QuadTo:
                    if (!open) { ctx.BeginFigure(seg.Points[0], false); open = true; }
                    else { ctx.QuadraticBezierTo(seg.Points[0], seg.Points[1]); }
                    break;
                case PathCommand.CubicTo:
                    if (!open) { ctx.BeginFigure(seg.Points[0], false); open = true; }
                    else { ctx.CubicBezierTo(seg.Points[0], seg.Points[1], seg.Points[2]); }
                    break;
                case PathCommand.Close:
                    if (open) { ctx.EndFigure(true); open = false; }
                    break;
            }
        }
        if (open) ctx.EndFigure(false);

        return geo;
    }

    /// <summary>逐点仿射变换，返回新对象，不修改自身。</summary>
    public PathData Transform(Func<Point, Point> f)
    {
        var result = new PathData();
        foreach (var seg in Segments)
        {
            result.Segments.Add(new PathSegment
            {
                Command = seg.Command,
                Points = seg.Points.Select(f).ToArray()
            });
        }
        return result;
    }

    /// <summary>生成 SVG 的 d 属性字符串，格式与 Go 原型一致。</summary>
    public string ToSvgD()
    {
        var parts = new List<string>();
        foreach (var seg in Segments)
        {
            var cmd = seg.Command switch
            {
                PathCommand.MoveTo => "M",
                PathCommand.LineTo => "L",
                PathCommand.QuadTo => "Q",
                PathCommand.CubicTo => "C",
                _ => "Z"
            };
            var coords = string.Join(" ", seg.Points.Select(p =>
                $"{p.X:0.##},{p.Y:0.##}"));
            parts.Add(coords.Length == 0 ? cmd : $"{cmd} {coords}");
        }
        return string.Join(" ", parts);
    }

    private static void AppendQuad(List<Point> dst, Point p0, Point p1, Point p2, int n)
    {
        for (var i = 1; i <= n; i++)
        {
            var t = (double)i / n;
            var mt = 1 - t;
            dst.Add(new Point(
                mt * mt * p0.X + 2 * mt * t * p1.X + t * t * p2.X,
                mt * mt * p0.Y + 2 * mt * t * p1.Y + t * t * p2.Y));
        }
    }

    private static void AppendCubic(List<Point> dst, Point p0, Point p1, Point p2, Point p3, int n)
    {
        for (var i = 1; i <= n; i++)
        {
            var t = (double)i / n;
            var mt = 1 - t;
            dst.Add(new Point(
                mt * mt * mt * p0.X + 3 * mt * mt * t * p1.X + 3 * mt * t * t * p2.X + t * t * t * p3.X,
                mt * mt * mt * p0.Y + 3 * mt * mt * t * p1.Y + 3 * mt * t * t * p2.Y + t * t * t * p3.Y));
        }
    }
}
