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

    /// <summary>一次性取当前有效条目，单帧渲染用，避免逐对象加锁。</summary>
    public IReadOnlyDictionary<string, Color> Snapshot()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var result = new Dictionary<string, Color>(_entries.Count);
            foreach (var (id, entry) in _entries)
                if (now <= entry.until)
                    result[id] = entry.color;
            return result;
        }
    }

    public void Clear(string id)
    {
        lock (_lock) _entries.Remove(id);
    }
}
