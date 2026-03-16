namespace GcaEditor.UndoRedo;

public sealed class UndoRedoStack<T>
{
    private readonly Stack<T> _undo = new();
    private readonly Stack<T> _redo = new();
    private readonly Func<T, T> _clone;
    private readonly int _maxUndo;

    public UndoRedoStack(Func<T, T> clone, int maxUndo = 100)
    {
        if (maxUndo < 1)
            throw new ArgumentOutOfRangeException(nameof(maxUndo), "maxUndo must be >= 1.");

        _clone = clone ?? throw new ArgumentNullException(nameof(clone));
        _maxUndo = maxUndo;
    }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public int UndoCount => _undo.Count;
    public int RedoCount => _redo.Count;
    public int MaxUndo => _maxUndo;

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    public void PushUndoSnapshot(T beforeState)
    {
        _undo.Push(_clone(beforeState));
        TrimUndoToMax();
        _redo.Clear();
    }

    public T Undo(T currentState)
    {
        if (!CanUndo)
            throw new InvalidOperationException("Nothing to undo");

        _redo.Push(_clone(currentState));
        return _undo.Pop();
    }

    public T Redo(T currentState)
    {
        if (!CanRedo)
            throw new InvalidOperationException("Nothing to redo");

        _undo.Push(_clone(currentState));
        TrimUndoToMax();
        return _redo.Pop();
    }

    private void TrimUndoToMax()
    {
        if (_undo.Count <= _maxUndo)
            return;

        var items = _undo.ToArray(); // index 0 = top of stack
        _undo.Clear();

        for (int i = _maxUndo - 1; i >= 0; i--)
            _undo.Push(items[i]);
    }
}
