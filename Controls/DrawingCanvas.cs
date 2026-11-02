using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using GeoDraw.Geometry;
using GeoDraw.Models;
using GeoDraw.Services;
using PathSegment = GeoDraw.Models.PathSegment;

namespace GeoDraw.Controls;

public sealed class DrawingCanvas : Control
{
    public static readonly StyledProperty<Document?> DocumentProperty =
        AvaloniaProperty.Register<DrawingCanvas, Document?>(nameof(Document));

    public static readonly StyledProperty<ObservableCollection<DrawObject>?> SelectedObjectsProperty =
        AvaloniaProperty.Register<DrawingCanvas, ObservableCollection<DrawObject>?>(nameof(SelectedObjects));

    public static readonly StyledProperty<EditorTool> ToolProperty =
        AvaloniaProperty.Register<DrawingCanvas, EditorTool>(nameof(Tool));

    public static readonly StyledProperty<DrawStyle> CurrentStyleProperty =
        AvaloniaProperty.Register<DrawingCanvas, DrawStyle>(nameof(CurrentStyle), new DrawStyle());

    public static readonly StyledProperty<IdViewMode> ViewModeProperty =
        AvaloniaProperty.Register<DrawingCanvas, IdViewMode>(nameof(ViewMode));

    public Document? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public ObservableCollection<DrawObject>? SelectedObjects
    {
        get => GetValue(SelectedObjectsProperty);
        set => SetValue(SelectedObjectsProperty, value);
    }

    public EditorTool Tool
    {
        get => GetValue(ToolProperty);
        set => SetValue(ToolProperty, value);
    }

    public DrawStyle CurrentStyle
    {
        get => GetValue(CurrentStyleProperty);
        set => SetValue(CurrentStyleProperty, value);
    }

    public IdViewMode ViewMode
    {
        get => GetValue(ViewModeProperty);
        set => SetValue(ViewModeProperty, value);
    }

    /// <summary>由 MainWindow 注入；Render 时读取，不参与属性通知。</summary>
    public Highlighter? Highlighter { get; set; }

    /// <summary>文档即将被编辑前触发，供 Undo 栈抓取快照。</summary>
    public event Action? BeforeDocumentEdit;

    private bool _pointerDown;
    private Point _pressPoint;
    private bool _dragStarted;
    private bool _marquee;
    private Point _marqueeStart, _marqueeEnd;
    private bool _dragging;
    private Point _dragStart, _dragEnd;

    private readonly Dictionary<(Color color, double width), Pen> _penCache = new();
    private readonly Dictionary<Color, IBrush> _brushCache = new();

    public DrawingCanvas()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DocumentProperty)
        {
            if (change.OldValue is Document oldDoc)
                oldDoc.Objects.CollectionChanged -= OnCollectionChanged;
            if (change.NewValue is Document newDoc)
                newDoc.Objects.CollectionChanged += OnCollectionChanged;
            InvalidateVisual();
        }
        else if (change.Property == SelectedObjectsProperty)
        {
            if (change.OldValue is INotifyCollectionChanged oldSel)
                oldSel.CollectionChanged -= OnCollectionChanged;
            if (change.NewValue is INotifyCollectionChanged newSel)
                newSel.CollectionChanged += OnCollectionChanged;
            InvalidateVisual();
        }
        else if (change.Property == ToolProperty
              || change.Property == ViewModeProperty
              || change.Property == CurrentStyleProperty)
        {
            InvalidateVisual();
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        InvalidateVisual();

    private IBrush GetBrush(Color color)
    {
        if (!_brushCache.TryGetValue(color, out var b))
        {
            b = new SolidColorBrush(color);
            _brushCache[color] = b;
        }
        return b;
    }

    private Pen GetPen(Color color, double width)
    {
        var key = (color, width);
        if (!_penCache.TryGetValue(key, out var p))
        {
            p = new Pen(GetBrush(color), width);
            _penCache[key] = p;
        }
        return p;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        context.FillRectangle(Brushes.White, new Rect(Bounds.Size));

        var doc = Document;
        if (doc != null)
        {
            var hl = Highlighter?.Snapshot();
            foreach (var obj in doc.Objects)
                DrawObject(context, obj, hl);
        }

        if (_dragging)
        {
            var preview = BuildPreviewPath();
            if (preview != null)
                context.DrawGeometry(null, GetPen(Color.FromRgb(140, 140, 140), 1), preview);
        }

        if (_marquee)
        {
            var r = GeometryHelper.NormalizeRect(_marqueeStart, _marqueeEnd);
            context.FillRectangle(GetBrush(Color.FromArgb(40, 0, 120, 215)), r);
            context.DrawRectangle(GetPen(Color.FromRgb(0, 120, 215), 1), r);
        }

        if (doc != null && ViewMode != IdViewMode.None)
            DrawIdLabels(context, doc);
    }

    private static Point RoundPoint(Point p) =>
        new(Math.Round(p.X, 2), Math.Round(p.Y, 2));

    private void DrawObject(
        DrawingContext context,
        DrawObject obj,
        IReadOnlyDictionary<string, Color>? hl)
    {
        var selected = SelectedObjects?.Contains(obj) == true;
        var stroke = hl != null && hl.TryGetValue(obj.Id, out var hc)
            ? hc
            : obj.Style.StrokeColor;

        switch (obj.Type)
        {
            case DrawObjectType.Point:
            {
                if (selected)
                    context.DrawEllipse(
                        GetBrush(Color.FromRgb(255, 165, 0)), null, obj.Geo,
                        obj.Style.Radius + 3.5, obj.Style.Radius + 3.5);
                context.DrawEllipse(
                    GetBrush(stroke), null, obj.Geo,
                    obj.Style.Radius, obj.Style.Radius);
                break;
            }
            case DrawObjectType.Path:
            {
                var geo = obj.GetGeometry();
                if (geo == null) break;
                if (selected)
                    context.DrawGeometry(
                        null, GetPen(Color.FromRgb(255, 200, 100), obj.Style.Width + 5), geo);
                context.DrawGeometry(null, GetPen(stroke, obj.Style.Width), geo);
                break;
            }
        }
    }

    private void DrawIdLabels(DrawingContext context, Document doc)
    {
        foreach (var obj in doc.Objects)
        {
            var show = (ViewMode == IdViewMode.PointIds && obj.Type == DrawObjectType.Point)
                    || (ViewMode == IdViewMode.PathIds && obj.Type == DrawObjectType.Path);
            if (!show) continue;

            Point anchor;
            if (obj.Type == DrawObjectType.Point)
            {
                anchor = new Point(obj.Geo.X + obj.Style.Radius + 3,
                                   obj.Geo.Y - obj.Style.Radius - 14);
            }
            else
            {
                anchor = obj.Path?.StartPoint() is { } sp
                    ? new Point(sp.X + 6, sp.Y - 16)
                    : default;
            }

            var ft = new FormattedText(obj.Id, CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Typeface.Default, 11,
                GetBrush(Color.FromRgb(50, 50, 50)));
            context.DrawText(ft, anchor);
        }
    }

    private StreamGeometry? BuildPreviewPath()
    {
        var path = ShapePath(Tool, _dragStart, _dragEnd);
        return path?.ToGeometry();
    }

    private static PathData? ShapePath(EditorTool tool, Point start, Point end)
    {
        switch (tool)
        {
            case EditorTool.Line:
            {
                var pd = new PathData();
                pd.Segments.Add(new PathSegment { Command = PathCommand.MoveTo, Points = [start] });
                pd.Segments.Add(new PathSegment { Command = PathCommand.LineTo, Points = [end] });
                return pd;
            }
            case EditorTool.Rect:
            {
                var pd = new PathData();
                pd.Segments.Add(new PathSegment { Command = PathCommand.MoveTo, Points = [start] });
                pd.Segments.Add(new PathSegment { Command = PathCommand.LineTo, Points = [new Point(end.X, start.Y)] });
                pd.Segments.Add(new PathSegment { Command = PathCommand.LineTo, Points = [end] });
                pd.Segments.Add(new PathSegment { Command = PathCommand.LineTo, Points = [new Point(start.X, end.Y)] });
                pd.Segments.Add(new PathSegment { Command = PathCommand.Close, Points = [] });
                return pd;
            }
            case EditorTool.Circle:
            {
                var r = GeometryHelper.Distance(start, end);
                return EllipsePath(new Point(start.X - r, start.Y - r),
                                   new Point(start.X + r, start.Y + r));
            }
            case EditorTool.Ellipse:
                return EllipsePath(start, end);
            default:
                return null;
        }
    }

    private static PathData EllipsePath(Point a, Point b)
    {
        const double kappa = 0.5522847498;
        var cx = (a.X + b.X) / 2;
        var cy = (a.Y + b.Y) / 2;
        var rx = Math.Max(Math.Abs(b.X - a.X) / 2, 0.5);
        var ry = Math.Max(Math.Abs(b.Y - a.Y) / 2, 0.5);
        var kx = kappa * rx;
        var ky = kappa * ry;

        var pd = new PathData();
        pd.Segments.Add(new PathSegment { Command = PathCommand.MoveTo, Points = [new Point(cx + rx, cy)] });
        pd.Segments.Add(new PathSegment { Command = PathCommand.CubicTo, Points =
            [new Point(cx + rx, cy + ky), new Point(cx + kx, cy + ry), new Point(cx, cy + ry)] });
        pd.Segments.Add(new PathSegment { Command = PathCommand.CubicTo, Points =
            [new Point(cx - kx, cy + ry), new Point(cx - rx, cy + ky), new Point(cx - rx, cy)] });
        pd.Segments.Add(new PathSegment { Command = PathCommand.CubicTo, Points =
            [new Point(cx - rx, cy - ky), new Point(cx - kx, cy - ry), new Point(cx, cy - ry)] });
        pd.Segments.Add(new PathSegment { Command = PathCommand.CubicTo, Points =
            [new Point(cx + kx, cy - ry), new Point(cx + rx, cy - ky), new Point(cx + rx, cy)] });
        return pd;
    }

    public DrawObject? HitTest(Point p)
    {
        var doc = Document;
        if (doc == null) return null;

        for (var i = doc.Objects.Count - 1; i >= 0; i--)
        {
            var o = doc.Objects[i];

            // 包围盒快速排除，阈值与下方精确判定对齐
            if (!o.GetBounds().Inflate(4).Contains(p)) continue;

            switch (o.Type)
            {
                case DrawObjectType.Point:
                {
                    var thr = Math.Max(o.Style.Radius + 4, 9);
                    if (GeometryHelper.Distance(p, o.Geo) <= thr) return o;
                    break;
                }
                case DrawObjectType.Path:
                {
                    if (o.Path == null) break;
                    var pts = o.Path.ToPoints();
                    var thr = Math.Max(o.Style.Width / 2 + 4, 6);
                    for (var j = 0; j < pts.Count - 1; j++)
                        if (GeometryHelper.DistanceToSegment(p, pts[j], pts[j + 1]) <= thr)
                            return o;
                    break;
                }
            }
        }
        return null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus();
        e.Pointer.Capture(this);

        _pointerDown = true;
        _dragStarted = false;
        _pressPoint = RoundPoint(e.GetPosition(this));

        if (Tool == EditorTool.Point && Document != null)
        {
            BeforeDocumentEdit?.Invoke();

            var obj = new DrawObject
            {
                Id = Document.NextId(DrawObjectType.Point),
                Type = DrawObjectType.Point,
                Geo = _pressPoint,
                Style = CurrentStyle.Clone()
            };
            Document.Objects.Add(obj);
            SelectedObjects?.Clear();
            SelectedObjects?.Add(obj);
            _pointerDown = false;
        }

        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_pointerDown) return;

        var pt = RoundPoint(e.GetPosition(this));

        if (!_dragStarted)
        {
            if (GeometryHelper.Distance(pt, _pressPoint) < 4) return;
            _dragStarted = true;

            if (Tool == EditorTool.Select)
            {
                _marquee = true;
                _marqueeStart = _pressPoint;
                _marqueeEnd = pt;
            }
            else if (Tool is EditorTool.Line or EditorTool.Rect
                                 or EditorTool.Circle or EditorTool.Ellipse)
            {
                _dragging = true;
                _dragStart = _pressPoint;
                _dragEnd = pt;
            }
        }

        if (_marquee) _marqueeEnd = pt;
        if (_dragging) _dragEnd = pt;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pointerDown) return;

        _pointerDown = false;
        e.Pointer.Capture(null);

        if (!_dragStarted)
        {
            if (Tool == EditorTool.Select)
                DoSelectClick(_pressPoint,
                    e.KeyModifiers.HasFlag(KeyModifiers.Control));
        }
        else
        {
            if (_marquee) ApplyMarquee(e.KeyModifiers.HasFlag(KeyModifiers.Control));
            if (_dragging) FinishShape();
        }

        _marquee = false;
        _dragging = false;
        _dragStarted = false;
        InvalidateVisual();
    }

    private void DoSelectClick(Point pt, bool additive)
    {
        var sel = SelectedObjects;
        if (sel == null) return;

        var hit = HitTest(pt);
        if (hit == null)
        {
            if (!additive) sel.Clear();
            return;
        }

        if (additive)
        {
            if (!sel.Remove(hit)) sel.Add(hit);
        }
        else
        {
            sel.Clear();
            sel.Add(hit);
        }
    }

    private void ApplyMarquee(bool additive)
    {
        var doc = Document;
        var sel = SelectedObjects;
        if (doc == null || sel == null) return;

        var r = GeometryHelper.NormalizeRect(_marqueeStart, _marqueeEnd);
        if (r.Width < 4 || r.Height < 4) return;

        var picked = doc.Objects
            .Where(o => GeometryHelper.BoundingBox(o).Intersects(r))
            .ToList();

        if (!additive) sel.Clear();
        foreach (var o in picked)
            if (!sel.Contains(o)) sel.Add(o);
    }

    private void FinishShape()
    {
        if (GeometryHelper.Distance(_dragStart, _dragEnd) < 3) return;

        var path = ShapePath(Tool, _dragStart, _dragEnd);
        if (path == null || Document == null) return;

        BeforeDocumentEdit?.Invoke();

        var obj = new DrawObject
        {
            Id = Document.NextId(DrawObjectType.Path),
            Type = DrawObjectType.Path,
            Path = path,
            Style = CurrentStyle.Clone()
        };
        Document.Objects.Add(obj);
        SelectedObjects?.Clear();
        SelectedObjects?.Add(obj);
    }
}
