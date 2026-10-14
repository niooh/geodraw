using System;
using Avalonia;
using GeoDraw.Models;

namespace GeoDraw.Geometry;

public static class GeometryHelper
{
    public static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public static double DistanceToSegment(Point p, Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var den = dx * dx + dy * dy;
        if (den == 0) return Distance(p, a);

        var t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / den;
        t = Math.Clamp(t, 0, 1);
        var proj = new Point(a.X + t * dx, a.Y + t * dy);
        return Distance(p, proj);
    }

    /// <summary>屏幕坐标 Y 向下，正角度视觉上为顺时针。</summary>
    public static Point RotatePoint(Point p, Point center, double deg)
    {
        var rad = deg * Math.PI / 180;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var dx = p.X - center.X;
        var dy = p.Y - center.Y;
        return new Point(
            center.X + dx * cos - dy * sin,
            center.Y + dx * sin + dy * cos);
    }

    public static Point ReflectPoint(Point p, Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var den = dx * dx + dy * dy;
        if (den == 0) return p;

        var t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / den;
        var proj = new Point(a.X + t * dx, a.Y + t * dy);
        return new Point(2 * proj.X - p.X, 2 * proj.Y - p.Y);
    }

    public static Point ReflectThroughPoint(Point p, Point center) =>
        new(2 * center.X - p.X, 2 * center.Y - p.Y);

    public static Rect NormalizeRect(Point a, Point b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y),
            Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    public static Rect BoundingBox(DrawObject obj)
    {
        if (obj.Type == DrawObjectType.Point)
        {
            var r = Math.Max(obj.Style.Radius, 0.5);
            return new Rect(obj.Geo.X - r, obj.Geo.Y - r, r * 2, r * 2);
        }

        if (obj.Path == null) return default;

        var pts = obj.Path.ToPoints();
        if (pts.Count == 0) return default;

        double minX = pts[0].X, minY = pts[0].Y, maxX = minX, maxY = minY;
        foreach (var p in pts)
        {
            minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y);
            maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y);
        }

        var pad = obj.Style.Width / 2;
        return new Rect(minX - pad, minY - pad,
            maxX - minX + pad * 2, maxY - minY + pad * 2);
    }
}