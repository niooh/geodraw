using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GeoDraw.Controls;
using GeoDraw.Models;
using GeoDraw.Services;

namespace GeoDraw.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public Document Document { get; } = new();
    public ObservableCollection<DrawObject> SelectedObjects { get; } = new();
    public Highlighter Highlighter { get; } = new();

    private readonly UndoStack _undoStack = new();

    // Canvas 走 backing field，以便订阅 BeforeDocumentEdit
    private DrawingCanvas? _canvas;
    public DrawingCanvas? Canvas
    {
        get => _canvas;
        set
        {
            if (_canvas == value) return;
            if (_canvas != null) _canvas.BeforeDocumentEdit -= OnCanvasBeforeEdit;
            _canvas = value;
            if (_canvas != null) _canvas.BeforeDocumentEdit += OnCanvasBeforeEdit;
        }
    }

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
    [ObservableProperty] private string _cliInput = "";

    public MainViewModel()
    {
        SelectedObjects.CollectionChanged += (_, _) => UpdateSelectionSummary();
    }

    // 工具
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

    // 撤销
    private void OnCanvasBeforeEdit() => PushUndo();

    public bool CanUndo => _undoStack.CanUndo;
    public bool CanRedo => _undoStack.CanRedo;

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        var current = DocumentSnapshot.Capture(Document);
        var target = _undoStack.Undo(current);
        if (target == null) return;
        RestoreSnapshot(target);
        Log("undo");
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        var current = DocumentSnapshot.Capture(Document);
        var target = _undoStack.Redo(current);
        if (target == null) return;
        RestoreSnapshot(target);
        Log("redo");
    }

    private void PushUndo()
    {
        _undoStack.Push(DocumentSnapshot.Capture(Document));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void RestoreSnapshot(DocumentSnapshot snapshot)
    {
        snapshot.RestoreTo(Document);
        SelectedObjects.Clear();
        Canvas?.InvalidateVisual();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    // 删除
    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedObjects.Count == 0) return;

        PushUndo();
        var ids = SelectedObjects.Select(o => o.Id).ToList();
        foreach (var o in ids.Select(Document.Find).Where(o => o != null).ToList())
            Document.Objects.Remove(o!);

        SelectedObjects.Clear();
        Log($"delete {string.Join(", ", ids)}");
    }

    [RelayCommand]
    private void ClearAll()
    {
        if (Document.Objects.Count == 0) return;

        PushUndo();
        Document.Objects.Clear();
        SelectedObjects.Clear();
        Log("cleared");
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

    // 导入导出
    public string ExportJson() => DocumentSerializer.Serialize(Document);

    public string BuildSvg(int width, int height) =>
        SvgExporter.Build(Document, width, height);

    /// <summary>用新文档替换当前文档，清空撤销历史。</summary>
    public void LoadDocument(Document newDoc)
    {
        Document.Objects.Clear();
        foreach (var o in newDoc.Objects)
            Document.Objects.Add(o);

        Document.PCounter = newDoc.PCounter;
        Document.LCounter = newDoc.LCounter;
        SelectedObjects.Clear();

        _undoStack.Clear();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();

        Log($"imported {Document.Objects.Count} object(s)");
    }

    // 日志
    private void Log(string line)
    {
        LogText = LogText.Length == 0 ? line : LogText + "\n" + line;
    }

    public void LogExternal(string line) => Log(line);
}