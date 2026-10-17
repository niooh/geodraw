using Avalonia.Controls;
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

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();
}
