using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GeoDraw.Controls;
using GeoDraw.Geometry;
using GeoDraw.Models;
using GeoDraw.Services;
using PathSegment = GeoDraw.Models.PathSegment;

namespace GeoDraw.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public Document Document { get; } = new();
    public ObservableCollection<DrawObject> SelectedObjects { get; } = new();
    public Highlighter Highlighter { get; } = new();

    public DrawingCanvas? Canvas { get; set; }

    [ObservableProperty]
    private EditorTool _tool = EditorTool.Select;

    public string ToolLabel => $"Tool: {Tool}";
    partial void OnToolChanged(EditorTool value) => OnPropertyChanged(nameof(ToolLabel));

    [ObservableProperty]
    private IdViewMode _viewMode = IdViewMode.None;

    public string ViewLabel => $"View: {ViewMode}";
    partial void OnViewModeChanged(IdViewMode value) => OnPropertyChanged(nameof(ViewLabel));

    [ObservableProperty] private DrawStyle _currentStyle = new();
    [ObservableProperty] private string _logText = "";
    [ObservableProperty] private string _selectedSummary = "none";
    [ObservableProperty] private string _cliInput = "";

    // 属性面板字段
    private bool _syncingSelection;

    [ObservableProperty] private bool _hasSingleSelection;
    [ObservableProperty] private bool _isPointSelected;
    [ObservableProperty] private bool _isPathSelected;
    [ObservableProperty] private string _selId = "";
    [ObservableProperty] private double _selX;
    [ObservableProperty] private double _selY;
    [ObservableProperty] private double _selRadius = 5;
    [ObservableProperty] private double _selWidth = 2;
    [ObservableProperty] private int _selSegmentCount;

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

    public MainViewModel()
    {
        SelectedObjects.CollectionChanged += (_, _) => UpdateSelectionSummary();
    }

    partial void OnSelXChanged(double value)
    {
        if (_syncingSelection) return;
        if (SingleSelectedPoint() is { } p)
        {
            p.Geo = new Point(value, p.Geo.Y);
            Canvas?.InvalidateVisual();
        }
    }

    partial void OnSelYChanged(double value)
    {
        if (_syncingSelection) return;
        if (SingleSelectedPoint() is { } p)
        {
            p.Geo = new Point(p.Geo.X, value);
            Canvas?.InvalidateVisual();
        }
    }

    partial void OnSelRadiusChanged(double value)
    {
        if (_syncingSelection) return;
        if (SingleSelectedPoint() is { } p)
        {
            p.Style.Radius = value;
            Canvas?.InvalidateVisual();
        }
    }

    partial void OnSelWidthChanged(double value)
    {
        if (_syncingSelection) return;
        if (SingleSelectedPath() is { } p)
        {
            p.Style.Width = value;
            Canvas?.InvalidateVisual();
        }
    }

    private DrawObject? SingleSelectedPoint() =>
        SelectedObjects.Count == 1 && SelectedObjects[0].Type == DrawObjectType.Point
            ? SelectedObjects[0] : null;

    private DrawObject? SingleSelectedPath() =>
        SelectedObjects.Count == 1 && SelectedObjects[0].Type == DrawObjectType.Path
            ? SelectedObjects[0] : null;

    private void UpdateSelectionSummary()
    {
        _syncingSelection = true;
        try
        {
            if (SelectedObjects.Count == 0)
            {
                SelectedSummary = "none";
                HasSingleSelection = false;
                IsPointSelected = false;
                IsPathSelected = false;
                SelId = "";
                return;
            }

            if (SelectedObjects.Count > 1)
            {
                SelectedSummary = $"{SelectedObjects.Count} objects: " +
                    string.Join(", ", SelectedObjects.Select(o => o.Id));
                HasSingleSelection = false;
                IsPointSelected = false;
                IsPathSelected = false;
                SelId = "";
                return;
            }

            var o = SelectedObjects[0];
            HasSingleSelection = true;
            SelId = o.Id;
            IsPointSelected = o.Type == DrawObjectType.Point;
            IsPathSelected = o.Type == DrawObjectType.Path;

            if (IsPointSelected)
            {
                SelX = o.Geo.X;
                SelY = o.Geo.Y;
                SelRadius = o.Style.Radius;
                SelectedSummary = $"{o.Id} · point";
            }
            else
            {
                SelWidth = o.Style.Width;
                SelSegmentCount = o.Path?.Segments.Count ?? 0;
                SelectedSummary = $"{o.Id} · path";
            }
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    // 命令
    [RelayCommand]
    private void SetTool(string? name)
    {
        if (Enum.TryParse<EditorTool>(name, out var t))
        {
            Tool = t;
            Log($"tool: {t}");
        }
    }

    [RelayCommand]
    private void SetView(string? name)
    {
        if (Enum.TryParse<IdViewMode>(name, out var v))
        {
            ViewMode = v;
            Log($"view: {v}");
        }
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedObjects.Count == 0) return;

        var ids = SelectedObjects.Select(o => o.Id).ToList();
        foreach (var o in ids.Select(Document.Find).Where(o => o != null).ToList())
            Document.Objects.Remove(o!);

        SelectedObjects.Clear();
        Log($"delete {string.Join(", ", ids)}");
    }

    [RelayCommand]
    private void ClearAll()
    {
        Document.Objects.Clear();
        SelectedObjects.Clear();
        Log("cleared");
    }

    // 变换
    private DrawObject CloneWithNewId(DrawObject o) => new()
    {
        Id = Document.NextId(o.Type),
        Type = o.Type,
        Style = o.Style.Clone(),
        Geo = o.Geo,
        Path = o.Path
    };

    private void ReplaceSelectionWith(System.Collections.Generic.List<DrawObject> created)
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

        var created = new System.Collections.Generic.List<DrawObject>();
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

        var created = new System.Collections.Generic.List<DrawObject>();
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
        var created = new System.Collections.Generic.List<DrawObject>();

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

    private bool HasRotateCenter;

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

    public void LogExternal(string line) => Log(line);

    // 选中导航
    [RelayCommand]
    private void SelectNext()
    {
        var objects = Document.Objects;
        if (objects.Count == 0) return;

        if (SelectedObjects.Count == 0)
        {
            SelectedObjects.Add(objects[0]);
        }
        else
        {
            var cur = SelectedObjects[^1];
            var idx = objects.IndexOf(cur);
            var next = objects[(idx + 1) % objects.Count];
            SelectedObjects.Clear();
            SelectedObjects.Add(next);
        }
    }

    [RelayCommand]
    private void SelectPrev()
    {
        var objects = Document.Objects;
        if (objects.Count == 0) return;

        if (SelectedObjects.Count == 0)
        {
            SelectedObjects.Add(objects[^1]);
        }
        else
        {
            var cur = SelectedObjects[^1];
            var idx = objects.IndexOf(cur);
            var n = objects.Count;
            var prev = objects[(idx - 1 + n) % n];
            SelectedObjects.Clear();
            SelectedObjects.Add(prev);
        }
    }

    // Connect 系列
    private List<DrawObject> GetSelectedPoints() =>
        SelectedObjects.Where(o => o.Type == DrawObjectType.Point).ToList();

    private void ReplaceSelectionWithSingle(DrawObject obj)
    {
        Document.Objects.Add(obj);
        SelectedObjects.Clear();
        SelectedObjects.Add(obj);
    }

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
	
    // CLI
    [RelayCommand]
    private void RunCli()
    {
        var s = CliInput.Trim();
        CliInput = "";
        if (s.Length == 0) return;

        var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        switch (parts[0])
        {
            case "select" when parts.Length > 1:
                SelectedObjects.Clear();
                foreach (var id in parts[1..])
                {
                    var o = Document.Find(id);
                    if (o != null) SelectedObjects.Add(o);
                    else Log($"not found: {id}");
                }
                break;
            case "which" when parts.Length > 1:
                RunWhich(parts[1]);
                break;
            case "clear":
                ClearAll();
                break;
            case "help":
                Log("commands: select <id...> | which <id> | clear | help");
                break;
            default:
                Log($"unknown command: {parts[0]}");
                break;
        }
    }

    private void RunWhich(string id)
    {
        var o = Document.Find(id);
        if (o == null)
        {
            Log($"not found: {id}");
            return;
        }

        var hl = Color.FromRgb(255, 0, 0);
        if (o.Type == DrawObjectType.Path && IsRed(o.Style.StrokeColor))
            hl = Color.FromRgb(0, 200, 0);

        Highlighter.Set(id, hl, TimeSpan.FromSeconds(2));
        Canvas?.InvalidateVisual();
        Log($"which {id}");

        var captured = id;
        _ = Task.Delay(2000).ContinueWith(_ =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                Highlighter.Clear(captured);
                Canvas?.InvalidateVisual();
            });
        });
    }

    private static bool IsRed(Color c) => c.R > 0xb0 && c.G < 0x40 && c.B < 0x40;

    public string BuildSvg(int width, int height)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        sb.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">""");
        sb.AppendLine("""<rect width="100%" height="100%" fill="#ffffff"/>""");

        foreach (var o in Document.Objects)
        {
            var hex = $"#{o.Style.StrokeColor.R:x2}{o.Style.StrokeColor.G:x2}{o.Style.StrokeColor.B:x2}";
            if (o.Type == DrawObjectType.Point)
            {
                sb.AppendLine($"""<circle cx="{o.Geo.X:0.##}" cy="{o.Geo.Y:0.##}" r="{o.Style.Radius:0.##}" fill="{hex}"/>""");
            }
            else if (o.Path != null)
            {
                var d = o.Path.ToSvgD();
                if (d.Length == 0) continue;
                sb.AppendLine($"""<path d="{d}" fill="none" stroke="{hex}" stroke-width="{o.Style.Width:0.##}" stroke-linecap="round" stroke-linejoin="round"/>""");
            }
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    /// <summary>用新文档替换当前文档，重置选择和状态。</summary>
    public void LoadDocument(Document newDoc)
    {
        Document.Objects.Clear();
        foreach (var o in newDoc.Objects)
            Document.Objects.Add(o);

        Document.PCounter = newDoc.PCounter;
        Document.LCounter = newDoc.LCounter;
        SelectedObjects.Clear();
        Log($"imported {Document.Objects.Count} object(s)");
    }

    public string ExportJson() => DocumentSerializer.Serialize(Document);

    private void Log(string line)
    {
        LogText = LogText.Length == 0 ? line : LogText + "\n" + line;
    }
}
