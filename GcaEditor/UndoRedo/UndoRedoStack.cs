using System;
using System.Collections.Generic;

namespace GcaEditor.UndoRedo;

public sealed class UndoRedoStack<T>
{
    private readonly Stack<T> _undo = new();
    private readonly Stack<T> _redo = new();
    private readonly Func<T, T> _clone;

    public UndoRedoStack(Func<T, T> clone)
    {
        _clone = clone;
    }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    // On push l’état "avant action"
    public void PushUndoSnapshot(T beforeState)
    {
        _undo.Push(_clone(beforeState));
        _redo.Clear();
    }

    public T Undo(T currentState)
    {
        if (!CanUndo) throw new InvalidOperationException("Nothing to undo");
        _redo.Push(_clone(currentState));
        return _undo.Pop();
    }

    public T Redo(T currentState)
    {
        if (!CanRedo) throw new InvalidOperationException("Nothing to redo");
        _undo.Push(_clone(currentState));
        return _redo.Pop();
    }
}