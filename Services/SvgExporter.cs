using System.Text;
using GeoDraw.Models;

namespace GeoDraw.Services;

/// <summary>SVG 导出，格式与 Go 原型一致。</summary>
public static class SvgExporter
{
    public static string Build(Document doc, int width, int height)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        sb.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">""");
        sb.AppendLine("""<rect width="100%" height="100%" fill="#ffffff"/>""");

        foreach (var o in doc.Objects)
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
}