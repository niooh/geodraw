using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using GeoDraw.ViewModels;

namespace GeoDraw.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainViewModel vm)
            vm.Canvas = DrawCanvas;
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

    private void OnCliKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (DataContext is MainViewModel vm)
            vm.RunCliCommand.Execute(null);
        e.Handled = true;
    }

    private async void OnExportSvgClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export SVG",
            DefaultExtension = "svg",
            FileTypeChoices =
            [
                new FilePickerFileType("SVG") { Patterns = ["*.svg"] }
            ]
        });
        if (file == null) return;

        var svg = vm.BuildSvg((int)DrawCanvas.Bounds.Width, (int)DrawCanvas.Bounds.Height);
        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(svg);
    }
}