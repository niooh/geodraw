using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GeoDraw.Controls;
using GeoDraw.Models;

namespace GeoDraw.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public Document Document { get; } = new();
    public ObservableCollection<DrawObject> SelectedObjects { get; } = new();

    [ObservableProperty]
    private EditorTool _tool = EditorTool.Select;

    [ObservableProperty]
    private DrawStyle _currentStyle = new();

    [ObservableProperty]
    private IdViewMode _viewMode = IdViewMode.None;

    [ObservableProperty]
    private string _logText = "";

    [ObservableProperty]
    private string _selectedSummary = "none";

    [ObservableProperty]
    private string _cliInput = "";

    /// <summary>由 MainWindow 注入，用于属性变更后主动触发重绘。</summary>
    public DrawingCanvas? Canvas { get; set; }

    public MainViewModel()
    {
        SelectedObjects.CollectionChanged += (_, _) => UpdateSelectionSummary();
    }

    private void UpdateSelectionSummary()
    {
        if (SelectedObjects.Count == 0)
        {
            SelectedSummary = "none";
            return;
        }

        if (SelectedObjects.Count > 1)
        {
            SelectedSummary = $"{SelectedObjects.Count} objects: " +
                              string.Join(", ", SelectedObjects.Select(o => o.Id));
            return;
        }

        var o = SelectedObjects[0];
        SelectedSummary = o.Type == DrawObjectType.Point
            ? $"{o.Id} · point  ({o.Geo.X:0.#}, {o.Geo.Y:0.#})"
            : $"{o.Id} · path   ({o.Path?.Segments.Count ?? 0} segments)";
    }

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
            case "clear":
                ClearAll();
                break;
            case "help":
                Log("commands: select <id...> | clear | help");
                break;
            default:
                Log($"unknown command: {parts[0]}");
                break;
        }
    }

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

    private void Log(string line)
    {
        LogText = LogText.Length == 0 ? line : LogText + "\n" + line;
    }
}