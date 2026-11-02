using System.Collections.Generic;
using System.Linq;
using GeoDraw.Models;

namespace GeoDraw.Services;

/// <summary>基于文档快照的撤销栈。</summary>
public sealed class UndoStack
{
    private readonly Stack<DocumentSnapshot> _undo = new();
    private readonly Stack<DocumentSnapshot> _redo = new();
    private const int MaxDepth = 100;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Push(DocumentSnapshot snapshot)
    {
        _undo.Push(snapshot);
        _redo.Clear();

        if (_undo.Count > MaxDepth)
        {
            // Stack 无底端出栈，按栈顶保留重建
            var keep = _undo.ToArray().Take(MaxDepth).Reverse().ToArray();
            _undo.Clear();
            foreach (var s in keep) _undo.Push(s);
        }
    }

    public DocumentSnapshot? Undo(DocumentSnapshot current)
    {
        if (_undo.Count == 0) return null;
        _redo.Push(current);
        return _undo.Pop();
    }

    public DocumentSnapshot? Redo(DocumentSnapshot current)
    {
        if (_redo.Count == 0) return null;
        _undo.Push(current);
        return _redo.Pop();
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
