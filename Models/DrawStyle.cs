using Avalonia.Media;

namespace GeoDraw.Models;

public sealed class DrawStyle
{
    public Color StrokeColor { get; set; } = Colors.Black;
    public double Width { get; set; } = 2;
    public double Radius { get; set; } = 5;

    public DrawStyle Clone() => new()
    {
        StrokeColor = StrokeColor,
        Width = Width,
        Radius = Radius
    };
}