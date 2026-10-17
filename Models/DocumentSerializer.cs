using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Media;
using GeoDraw.Models;

namespace GeoDraw.Services;

/// <summary>文档 JSON 序列化，格式与 Go 原型互通。</summary>
public static class DocumentSerializer
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static string Serialize(Document doc)
    {
        var root = new JsonObject();
        var objects = new JsonArray();

        foreach (var o in doc.Objects)
        {
            var jo = new JsonObject
            {
                ["id"] = o.Id,
                ["stroke"] = ColorToHex(o.Style.StrokeColor)
            };

            if (o.Type == DrawObjectType.Point)
            {
                jo["x"] = o.Geo.X;
                jo["y"] = o.Geo.Y;
                jo["radius"] = o.Style.Radius;
            }
            else
            {
                if (o.Path == null)
                    continue;
                jo["d"] = o.Path.ToJsonTokens();
                jo["width"] = o.Style.Width;
            }

            objects.Add(jo);
        }

        root["objects"] = objects;
        root["pCount"] = doc.PCounter;
        root["lCount"] = doc.LCounter;

        return root.ToJsonString(Indented);
    }

    public static Document Deserialize(string json)
    {
        var root = JsonNode.Parse(json)?.AsObject()
            ?? throw new InvalidDataException("root must be a JSON object");

        var doc = new Document();

        if (root["objects"] is JsonArray arr)
        {
            foreach (var node in arr)
            {
                var jo = node?.AsObject()
                    ?? throw new InvalidDataException("object must be a JSON object");

                var id = jo["id"]?.GetValue<string>()
                    ?? throw new InvalidDataException("object missing id");

                var type = id.Length > 0 && id[0] == 'l'
                    ? DrawObjectType.Path
                    : DrawObjectType.Point;

                var obj = new DrawObject
                {
                    Id = id,
                    Type = type,
                    Style = new DrawStyle
                    {
                        StrokeColor = HexToColor(jo["stroke"]?.GetValue<string>())
                    }
                };

                if (type == DrawObjectType.Point)
                {
                    obj.Geo = new Point(
                        jo["x"]?.GetValue<double>() ?? 0,
                        jo["y"]?.GetValue<double>() ?? 0);
                    obj.Style.Radius = jo["radius"]?.GetValue<double>() ?? 5;
                    obj.Style.Width = 2;
                }
                else
                {
                    if (jo["d"] is not JsonArray d)
                        throw new InvalidDataException($"{id}: path missing d");
                    obj.Path = PathData.FromJsonTokens(d);
                    obj.Style.Width = jo["width"]?.GetValue<double>() ?? 2;
                    obj.Style.Radius = 5;
                }

                doc.Objects.Add(obj);
            }
        }

        doc.PCounter = root["pCount"]?.GetValue<int>() ?? 0;
        doc.LCounter = root["lCount"]?.GetValue<int>() ?? 0;

        return doc;
    }

    private static string ColorToHex(Color c) => $"#{c.R:x2}{c.G:x2}{c.B:x2}";

    private static Color HexToColor(string? hex)
    {
        if (hex is not { Length: 7 } || hex[0] != '#')
            return Colors.Black;

        if (byte.TryParse(hex.AsSpan(1, 2), NumberStyles.HexNumber, null, out var r)
            && byte.TryParse(hex.AsSpan(3, 2), NumberStyles.HexNumber, null, out var g)
            && byte.TryParse(hex.AsSpan(5, 2), NumberStyles.HexNumber, null, out var b))
            return Color.FromRgb(r, g, b);

        return Colors.Black;
    }
}