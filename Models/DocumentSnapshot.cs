using System.Collections.Generic;

namespace GeoDraw.Models;

/// <summary>文档快照，Undo/Redo 用；对象在 Capture 时深拷贝。</summary>
public sealed class DocumentSnapshot
{
    public IReadOnlyList<DrawObject> Objects { get; }
    public int PCounter { get; }
    public int LCounter { get; }

    private DocumentSnapshot(List<DrawObject> objects, int p, int l)
    {
        Objects = objects;
        PCounter = p;
        LCounter = l;
    }

    public static DocumentSnapshot Capture(Document doc)
    {
        var list = new List<DrawObject>(doc.Objects.Count);
        foreach (var o in doc.Objects)
            list.Add(o.Clone());
        return new DocumentSnapshot(list, doc.PCounter, doc.LCounter);
    }

    /// <summary>写回文档；对象再拷贝一次，避免快照被后续编辑污染。</summary>
    public void RestoreTo(Document doc)
    {
        doc.Objects.Clear();
        foreach (var o in Objects)
            doc.Objects.Add(o.Clone());
        doc.PCounter = PCounter;
        doc.LCounter = LCounter;
    }
}