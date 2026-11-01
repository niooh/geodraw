using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using GeoDraw.Models;
using PathSegment = GeoDraw.Models.PathSegment;

namespace GeoDraw.ViewModels;

public partial class MainViewModel
{
    private List<DrawObject> GetSelectedPoints() =>
        SelectedObjects.Where(o => o.Type == DrawObjectType.Point).ToList();

    private void ReplaceSelectionWithSingle(DrawObject obj)
    {
        Document.Objects.Add(obj);
        SelectedObjects.Clear();
        SelectedObjects.Add(obj);
    }

    // Connect 系列
    [RelayCommand]
    private void ConnectOneCurve()
    {
        var points = GetSelectedPoints();
        if (points.Count < 2)
        {
            Log("need at least 2 points");
            return;
        }

        PathData pd;
        string desc;
        switch (points.Count)
        {
            case 2:
            {
                pd = new PathData();
                pd.Segments.Add(new PathSegment { Command = PathCommand.MoveTo, Points = [points[0].Geo] });
                pd.Segments.Add(new PathSegment { Command = PathCommand.LineTo, Points = [points[1].Geo] });
                desc = "line";
                break;
            }
            case 3:
            {
                pd = new PathData();
                pd.Segments.Add(new PathSegment { Command = PathCommand.MoveTo, Points = [points[0].Geo] });
                pd.Segments.Add(new PathSegment { Command = PathCommand.QuadTo, Points = [points[1].Geo, points[2].Geo] });
                desc = "quadratic bezier";
                break;
            }
            case 4:
            {
                pd = new PathData();
                pd.Segments.Add(new PathSegment { Command = PathCommand.MoveTo, Points = [points[0].Geo] });
                pd.Segments.Add(new PathSegment { Command = PathCommand.CubicTo, Points = [points[1].Geo, points[2].Geo, points[3].Geo] });
                desc = "cubic bezier";
                break;
            }
            default:
                Log("connect: 2-4 points only");
                return;
        }

        var line = new DrawObject
        {
            Id = Document.NextId(DrawObjectType.Path),
            Type = DrawObjectType.Path,
            Path = pd,
            Style = CurrentStyle.Clone()
        };
        ReplaceSelectionWithSingle(line);
        Log($"connect: {line.Id} ({desc})");
    }

    [RelayCommand]
    private void ConnectM()
    {
        var points = GetSelectedPoints();
        if (points.Count < 2)
        {
            Log("need at least 2 points");
            return;
        }

        var pd = new PathData();
        pd.Segments.Add(new PathSegment { Command = PathCommand.MoveTo, Points = [points[0].Geo] });
        foreach (var p in points.Skip(1))
            pd.Segments.Add(new PathSegment { Command = PathCommand.LineTo, Points = [p.Geo] });

        var line = new DrawObject
        {
            Id = Document.NextId(DrawObjectType.Path),
            Type = DrawObjectType.Path,
            Path = pd,
            Style = CurrentStyle.Clone()
        };
        ReplaceSelectionWithSingle(line);
        Log($"connect(M): {line.Id}, {points.Count} vertices");
    }

    [RelayCommand]
    private void ConnectQ()
    {
        var points = GetSelectedPoints();
        if (points.Count < 3)
        {
            Log("need at least 3 points");
            return;
        }

        var pd = new PathData();
        pd.Segments.Add(new PathSegment { Command = PathCommand.MoveTo, Points = [points[0].Geo] });

        var n = 0;
        for (var i = 0; i + 2 < points.Count; i += 2)
        {
            pd.Segments.Add(new PathSegment
            {
                Command = PathCommand.QuadTo,
                Points = [points[i + 1].Geo, points[i + 2].Geo]
            });
            n++;
        }

        if (n == 0)
        {
            Log("not enough points to form a curve");
            return;
        }

        var line = new DrawObject
        {
            Id = Document.NextId(DrawObjectType.Path),
            Type = DrawObjectType.Path,
            Path = pd,
            Style = CurrentStyle.Clone()
        };
        ReplaceSelectionWithSingle(line);
        Log($"connect(Q): {line.Id}, {n} quadratic segments");
    }

    [RelayCommand]
    private void ConnectC()
    {
        var points = GetSelectedPoints();
        if (points.Count < 4)
        {
            Log("need at least 4 points");
            return;
        }

        var pd = new PathData();
        pd.Segments.Add(new PathSegment { Command = PathCommand.MoveTo, Points = [points[0].Geo] });

        var n = 0;
        for (var i = 0; i + 3 < points.Count; i += 3)
        {
            pd.Segments.Add(new PathSegment
            {
                Command = PathCommand.CubicTo,
                Points = [points[i + 1].Geo, points[i + 2].Geo, points[i + 3].Geo]
            });
            n++;
        }

        if (n == 0)
        {
            Log("not enough points to form a curve");
            return;
        }

        var line = new DrawObject
        {
            Id = Document.NextId(DrawObjectType.Path),
            Type = DrawObjectType.Path,
            Path = pd,
            Style = CurrentStyle.Clone()
        };
        ReplaceSelectionWithSingle(line);
        Log($"connect(C): {line.Id}, {n} cubic segments");
    }
}