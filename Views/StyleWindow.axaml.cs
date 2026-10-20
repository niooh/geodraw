using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
            WidthSlider.Value = vm.CurrentStyle.Width;
            RadiusSlider.Value = vm.CurrentStyle.Radius;
            WidthLabel.Text = $"{vm.CurrentStyle.Width:0}";
            RadiusLabel.Text = $"{vm.CurrentStyle.Radius:0}";
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

    private void OnWidthChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.CurrentStyle.Width = e.NewValue;
            WidthLabel.Text = $"{e.NewValue:0}";
        }
    }

    private void OnRadiusChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.CurrentStyle.Radius = e.NewValue;
            RadiusLabel.Text = $"{e.NewValue:0}";
        }
    }
}
