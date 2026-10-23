using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using GeoDraw.ViewModels;

namespace GeoDraw.Views;

public partial class StyleWindow : Window
{
    public StyleWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is MainViewModel vm)
        {
            WidthTextBox.Text = vm.CurrentStyle.Width.ToString("0");
            RadiusTextBox.Text = vm.CurrentStyle.Radius.ToString("0");
        }
    }

    private void OnColorClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string name }) return;
        if (DataContext is not MainViewModel vm) return;

        vm.CurrentStyle.StrokeColor = name switch
        {
            "Black"  => Colors.Black,
            "Red"    => Color.FromRgb(220, 30, 30),
            "Blue"   => Color.FromRgb(0, 100, 255),
            "Green"  => Color.FromRgb(0, 160, 0),
            "Yellow" => Color.FromRgb(240, 190, 0),
            "Purple" => Color.FromRgb(150, 0, 200),
            "Gray"   => Color.FromRgb(128, 128, 128),
            _        => Colors.Black
        };
        vm.LogExternal($"style: color={name}");
    }

    private void OnNumberTextInput(object? sender, TextInputEventArgs e)
    {
        if (e.Text is null) return;
        foreach (var c in e.Text)
        {
            if (!char.IsDigit(c))
            {
                e.Handled = true;
                return;
            }
        }
    }

    private void OnWidthTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (double.TryParse(WidthTextBox.Text, out var val))
        {
            val = Math.Clamp(val, 1, 20);
            vm.CurrentStyle.Width = val;
        }
    }

    private void OnWidthLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (!double.TryParse(WidthTextBox.Text, out var val))
            val = vm.CurrentStyle.Width;
        val = Math.Clamp(val, 1, 20);
        vm.CurrentStyle.Width = val;
        WidthTextBox.Text = val.ToString("0");
    }

    private void OnRadiusTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (double.TryParse(RadiusTextBox.Text, out var val))
        {
            val = Math.Clamp(val, 1, 20);
            vm.CurrentStyle.Radius = val;
        }
    }

    private void OnRadiusLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (!double.TryParse(RadiusTextBox.Text, out var val))
            val = vm.CurrentStyle.Radius;
        val = Math.Clamp(val, 1, 20);
        vm.CurrentStyle.Radius = val;
        RadiusTextBox.Text = val.ToString("0");
    }
}
