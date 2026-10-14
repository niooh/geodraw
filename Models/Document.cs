using System.Collections.ObjectModel;
using System.Linq;

namespace GeoDraw.Models;

public sealed class Document
{
    public ObservableCollection<DrawObject> Objects { get; } = new();

    private int _pCount;
    private int _lCount;

    public string NextId(DrawObjectType type) =>
        type == DrawObjectType.Point ? $"p{++_pCount}" : $"l{++_lCount}";

    public DrawObject? Find(string id) =>
        Objects.FirstOrDefault(o => o.Id == id);

    public bool Remove(DrawObject obj) => Objects.Remove(obj);
}
