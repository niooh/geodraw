using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GeoDraw.ViewModels;

namespace GeoDraw.Views;

public partial class RotateWindow : Window
{
    public RotateWindow()
    {
        InitializeComponent();
    }

    private void OnApplyClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.RotateCommand.Execute(null);
        Close();
    }

    private void OnAngleTextInput(object? sender, TextInputEventArgs e)
    {
        if (e.Text is null) return;
        foreach (var c in e.Text)
        {
            if (!char.IsDigit(c) && c != '.' && c != '-')
            {
                e.Handled = true;
                return;
            }
        }
    }

    private void OnRepeatTextInput(object? sender, TextInputEventArgs e)
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

    private void OnRepeatLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        if (!int.TryParse(RepeatTextBox.Text, out var n) || n < 1)
            n = vm.RotateCount;

        n = Math.Max(1, n);
        vm.RotateCount = n;
        RepeatTextBox.Text = n.ToString();
    }
}