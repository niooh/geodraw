using System;
using System.Collections.Generic;
using Avalonia.Media;

namespace GeoDraw.Services;

/// <summary>对象 ID 的临时高亮，到期后不再返回颜色。</summary>
public sealed class Highlighter
{
    private readonly Dictionary<string, (Color color, DateTime until)> _entries = new();
    private readonly object _lock = new();

    public void Set(string id, Color color, TimeSpan duration)
    {
        lock (_lock)
            _entries[id] = (color, DateTime.UtcNow + duration);
    }

    public Color? Get(string id)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(id, out var e) && DateTime.UtcNow <= e.until)
                return e.color;
            return null;
        }
    }

    public void Clear(string id)
    {
        lock (_lock) _entries.Remove(id);
    }
}