using System.Linq;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GeoDraw.Models;

namespace GeoDraw.ViewModels;

public partial class MainViewModel
{
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
    [ObservableProperty] private string _selectedSummary = "none";

	partial void OnSelXChanged(double value)
	{
		if (_syncingSelection) return;
		if (SingleSelectedPoint() is { } p)
		{
			p.Geo = new Point(value, p.Geo.Y);
			p.InvalidateRenderCache();
			Canvas?.InvalidateVisual();
		}
	}

	partial void OnSelYChanged(double value)
	{
		if (_syncingSelection) return;
		if (SingleSelectedPoint() is { } p)
		{
			p.Geo = new Point(p.Geo.X, value);
			p.InvalidateRenderCache();
			Canvas?.InvalidateVisual();
		}
	}

	partial void OnSelRadiusChanged(double value)
	{
		if (_syncingSelection) return;
		if (SingleSelectedPoint() is { } p)
		{
			p.Style.Radius = value;
			p.InvalidateRenderCache();
			Canvas?.InvalidateVisual();
		}
	}

	partial void OnSelWidthChanged(double value)
	{
		if (_syncingSelection) return;
		if (SingleSelectedPath() is { } p)
		{
			p.Style.Width = value;
			p.InvalidateRenderCache();
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
}