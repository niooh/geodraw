using System;
using System.IO;
using System.Linq;
using System.ComponentModel;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using GeoDraw.ViewModels;
using GeoDraw.Services;

namespace GeoDraw.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // 隧道阶段捕获 Ctrl+方向键，先于子控件和键盘导航
        AddHandler(KeyDownEvent, OnGlobalKeyDown, RoutingStrategies.Tunnel);
    }

	protected override void OnDataContextChanged(EventArgs e)
	{
		base.OnDataContextChanged(e);
		if (DataContext is MainViewModel vm)
		{
			vm.Canvas = DrawCanvas;
			DrawCanvas.Highlighter = vm.Highlighter;

			vm.PropertyChanged -= OnViewModelPropertyChanged;
			vm.PropertyChanged += OnViewModelPropertyChanged;
		}
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(MainViewModel.LogText))
		{
			Dispatcher.UIThread.Post(() => LogScrollViewer.ScrollToEnd());
		}
	}

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (DataContext is not MainViewModel vm) return;

        switch (e.Key)
        {
            case Key.Delete or Key.Back:
                vm.DeleteSelectedCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                vm.SelectedObjects.Clear();
                vm.SetToolCommand.Execute("Select");
                e.Handled = true;
                break;
        }
    }

	private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
	{
		if (DataContext is not MainViewModel vm) return;

		if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)
			|| e.Key is not (Key.Right or Key.Left))
			return;

		// 焦点在文本框里时放行
		if (e.Source is TextBox) return;

		if (e.Key == Key.Right)
			vm.SelectNextCommand.Execute(null);
		else
			vm.SelectPrevCommand.Execute(null);

		e.Handled = true;
	}

    private void OnCliKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (DataContext is MainViewModel vm)
            vm.RunCliCommand.Execute(null);
        e.Handled = true;
    }

    private void OnNumberLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb || DataContext is not MainViewModel vm) return;

        tb.Text = tb.Name switch
        {
            "SelXBox"      => vm.SelX.ToString("0.##"),
            "SelYBox"      => vm.SelY.ToString("0.##"),
            "SelRadiusBox" => vm.SelRadius.ToString("0.#"),
            "SelWidthBox"  => vm.SelWidth.ToString("0.#"),
            _              => tb.Text
        };
    }

    // 菜单项
    private void OnToolMenuClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is MenuItem { Tag: string name })
            vm.SetToolCommand.Execute(name);
    }

    private void OnViewMenuClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is MenuItem { Tag: string name })
            vm.SetViewCommand.Execute(name);
    }

    private async void OnStyleClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var w = new StyleWindow { DataContext = vm };
        await w.ShowDialog(this);
    }
	
    private void OnOperateMenuClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (sender is not MenuItem { Tag: string name }) return;

        switch (name)
        {
            case "ConnectOneCurve":
                vm.ConnectOneCurveCommand.Execute(null);
                break;
            case "ConnectM":
                vm.ConnectMCommand.Execute(null);
                break;
            case "ConnectQ":
                vm.ConnectQCommand.Execute(null);
                break;
            case "ConnectC":
                vm.ConnectCCommand.Execute(null);
                break;
			case "Rotate":
                OnRotateClicked(sender, e);
                break;
            case "ReflectPoint":
                vm.ReflectAcrossPointCommand.Execute(null);
                break;
            case "ReflectLine":
                vm.ReflectAcrossLineCommand.Execute(null);
                break;
            case "Duplicate":
                vm.DuplicateCommand.Execute(null);
                break;
            case "SelectNext":
                vm.SelectNextCommand.Execute(null);
                break;
            case "SelectPrev":
                vm.SelectPrevCommand.Execute(null);
                break;
			case "Delete":
                vm.DeleteSelectedCommand.Execute(null);
                break;
            case "Clear":
                vm.ClearAllCommand.Execute(null);
                break;
        }
    }

    private void OnFileMenuClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string tag }) return;
        switch (tag)
        {
            case "ImportJson":  OnImportJsonClicked(sender, e);  break;
            case "ExportJson":  OnExportJsonClicked(sender, e);  break;
            case "ExportSvg":   OnExportSvgClicked(sender, e);   break;
            case "ExportPng":   OnExportPngClicked(sender, e);   break;
        }
    }

    private async void OnImportJsonClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import JSON",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("GeoDraw JSON") { Patterns = ["*.json"] }
            ]
        });
        if (files.Count == 0) return;

        try
        {
            await using var stream = await files[0].OpenReadAsync();
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            var doc = DocumentSerializer.Deserialize(json);
            vm.LoadDocument(doc);
        }
        catch (Exception ex)
        {
            vm.LogText = (vm.LogText.Length == 0 ? "" : vm.LogText + "\n")
                       + $"import failed: {ex.Message}";
        }
    }

    private async void OnExportJsonClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export JSON",
            DefaultExtension = "json",
            FileTypeChoices =
            [
                new FilePickerFileType("GeoDraw JSON") { Patterns = ["*.json"] }
            ]
        });
        if (file == null) return;

        try
        {
            var json = vm.ExportJson();
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(json);
            vm.LogText += (vm.LogText.Length == 0 ? "" : "\n") + "exported JSON";
        }
        catch (Exception ex)
        {
            vm.LogText += (vm.LogText.Length == 0 ? "" : "\n")
                        + $"export failed: {ex.Message}";
        }
    }

    private async void OnExportPngClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export PNG",
            DefaultExtension = "png",
            FileTypeChoices =
            [
                new FilePickerFileType("PNG") { Patterns = ["*.png"] }
            ]
        });
        if (file == null) return;

        try
        {
            var width = Math.Max(1, (int)DrawCanvas.Bounds.Width);
            var height = Math.Max(1, (int)DrawCanvas.Bounds.Height);

            // 临时清除选择，避免选中高亮进入导出图
            var saved = vm.SelectedObjects.ToList();
            vm.SelectedObjects.Clear();

            using var bitmap = new RenderTargetBitmap(
                new PixelSize(width, height),
                new Vector(96, 96));
            bitmap.Render(DrawCanvas);

            await using (var stream = await file.OpenWriteAsync())
            {
                bitmap.Save(stream, PngBitmapEncoderOptions.Default);
            }

            foreach (var o in saved)
                vm.SelectedObjects.Add(o);

            vm.LogText += (vm.LogText.Length == 0 ? "" : "\n")
                        + $"exported PNG {width}×{height}";
        }
        catch (Exception ex)
        {
            vm.LogText += (vm.LogText.Length == 0 ? "" : "\n")
                        + $"export failed: {ex.Message}";
        }
    }
	
    // 旋转对话框
    private async void OnRotateClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var w = new RotateWindow { DataContext = vm };
        await w.ShowDialog(this);
    }

    // SVG 导出
    private async void OnExportSvgClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export SVG",
            DefaultExtension = "svg",
            FileTypeChoices = [new FilePickerFileType("SVG") { Patterns = ["*.svg"] }]
        });
        if (file == null) return;

        var svg = vm.BuildSvg((int)DrawCanvas.Bounds.Width, (int)DrawCanvas.Bounds.Height);
        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(svg);
    }
}