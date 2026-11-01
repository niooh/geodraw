using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GeoDraw.Geometry;
using GeoDraw.Models;

namespace GeoDraw.ViewModels;

public partial class MainViewModel
{
    // 旋转参数
    private bool _rotateClockwise = true;
    public bool RotateClockwise
    {
        get => _rotateClockwise;
        set
        {
            if (_rotateClockwise == value) return;
            _rotateClockwise = value;
            OnPropertyChanged(nameof(RotateClockwise));
            OnPropertyChanged(nameof(RotateCounterClockwise));
        }
    }

    public bool RotateCounterClockwise
    {
        get => !_rotateClockwise;
        set => RotateClockwise = !value;
    }

    [ObservableProperty] private double _rotateAngle = 90;
    [ObservableProperty] private int _rotateCount = 1;

    private bool HasRotateCenter;

    private DrawObject CloneWithNewId(DrawObject o) => new()
    {
        Id = Document.NextId(o.Type),
        Type = o.Type,
        Style = o.Style.Clone(),
        Geo = o.Geo,
        Path = o.Path
    };

    private void ReplaceSelectionWith(List<DrawObject> created)
    {
        if (created.Count == 0)
        {
            Log("nothing to transform");
            return;
        }
        foreach (var c in created) Document.Objects.Add(c);
        SelectedObjects.Clear();
        foreach (var c in created) SelectedObjects.Add(c);
    }

    // 变换
    [RelayCommand]
    private void Duplicate()
    {
        if (SelectedObjects.Count == 0) return;
        var created = SelectedObjects.Select(o =>
        {
            var c = CloneWithNewId(o);
            if (o.Type == DrawObjectType.Point)
                c.Geo = new Point(o.Geo.X + 16, o.Geo.Y + 16);
            else if (o.Path != null)
                c.Path = o.Path.Transform(p => new Point(p.X + 16, p.Y + 16));
            return c;
        }).ToList();
        ReplaceSelectionWith(created);
        Log($"duplicate: {created.Count} object(s)");
    }

    [RelayCommand]
    private void ReflectAcrossPoint()
    {
        var center = SelectedObjects.LastOrDefault(o => o.Type == DrawObjectType.Point);
        if (center == null)
        {
            Log("select a center point plus objects to transform");
            return;
        }

        var created = new List<DrawObject>();
        foreach (var o in SelectedObjects.ToList())
        {
            if (o == center) continue;
            var c = CloneWithNewId(o);
            if (o.Type == DrawObjectType.Point)
                c.Geo = GeometryHelper.ReflectThroughPoint(o.Geo, center.Geo);
            else if (o.Path != null)
                c.Path = o.Path.Transform(p => GeometryHelper.ReflectThroughPoint(p, center.Geo));
            created.Add(c);
        }
        ReplaceSelectionWith(created);
        if (created.Count > 0)
            Log($"reflect point: {created.Count} object(s) about {center.Id}");
    }

    [RelayCommand]
    private void ReflectAcrossLine()
    {
        var axis = SelectedObjects.LastOrDefault(
            o => o.Type == DrawObjectType.Path && o.Path != null);
        if (axis?.Path == null)
        {
            Log("select an axis line plus objects to transform");
            return;
        }

        var pts = axis.Path.ToPoints();
        if (pts.Count < 2)
        {
            Log($"axis {axis.Id} is degenerate");
            return;
        }

        var ax1 = pts[0];
        var ax2 = pts[^1];
        if (GeometryHelper.Distance(ax1, ax2) < 1.0)
            ax2 = pts[pts.Count / 2];
        if (GeometryHelper.Distance(ax1, ax2) < 1.0)
        {
            Log($"axis {axis.Id} is degenerate");
            return;
        }

        var created = new List<DrawObject>();
        foreach (var o in SelectedObjects.ToList())
        {
            if (o == axis) continue;
            var c = CloneWithNewId(o);
            if (o.Type == DrawObjectType.Point)
                c.Geo = GeometryHelper.ReflectPoint(o.Geo, ax1, ax2);
            else if (o.Path != null)
                c.Path = o.Path.Transform(p => GeometryHelper.ReflectPoint(p, ax1, ax2));
            created.Add(c);
        }
        ReplaceSelectionWith(created);
        if (created.Count > 0)
            Log($"reflect line: {created.Count} object(s) about {axis.Id}");
    }

    [RelayCommand]
    private void Rotate()
    {
        if (SelectedObjects.Count == 0)
        {
            Log("select objects to rotate");
            return;
        }

        var (center, centerObj) = ComputeRotateCenter();
        if (!HasRotateCenter)
        {
            Log("no rotation center");
            return;
        }

        var sign = RotateClockwise ? 1.0 : -1.0;
        var count = Math.Max(1, RotateCount);
        var created = new List<DrawObject>();

        for (var i = 1; i <= count; i++)
        {
            var step = sign * RotateAngle * i;
            foreach (var o in SelectedObjects.ToList())
            {
                if (o == centerObj) continue;
                var c = CloneWithNewId(o);
                if (o.Type == DrawObjectType.Point)
                    c.Geo = GeometryHelper.RotatePoint(o.Geo, center, step);
                else if (o.Path != null)
                    c.Path = o.Path.Transform(p => GeometryHelper.RotatePoint(p, center, step));
                created.Add(c);
            }
        }

        ReplaceSelectionWith(created);
        if (created.Count == 0) return;

        var dir = RotateClockwise ? "CW" : "CCW";
        Log($"rotate: {created.Count} new object(s), " +
            $"{RotateAngle:0.#}° × {count} {dir}, center ({center.X:0},{center.Y:0})");
    }

    private (Point center, DrawObject? centerObj) ComputeRotateCenter()
    {
        for (var i = SelectedObjects.Count - 1; i >= 0; i--)
        {
            if (SelectedObjects[i].Type == DrawObjectType.Point)
            {
                HasRotateCenter = true;
                return (SelectedObjects[i].Geo, SelectedObjects[i]);
            }
        }

        if (SelectedObjects.Count == 0)
        {
            HasRotateCenter = false;
            return (default, null);
        }

        var r = GeometryHelper.BoundingBox(SelectedObjects[0]);
        for (var i = 1; i < SelectedObjects.Count; i++)
            r = r.Union(GeometryHelper.BoundingBox(SelectedObjects[i]));

        HasRotateCenter = true;
        return (new Point(r.X + r.Width / 2, r.Y + r.Height / 2), null);
    }
}
