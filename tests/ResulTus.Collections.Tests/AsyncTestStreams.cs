// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Collections.Tests;

/// <summary>
/// A hand-written async sequence over a fixed array. No mocking framework and no delays, so every
/// test is deterministic. It records what the consumer did to the stream: the token it passed to
/// <see cref="GetAsyncEnumerator"/>, how many elements were delivered and whether the enumerator
/// was disposed.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal sealed class TestAsyncStream<T> : IAsyncEnumerable<T>
{
    private readonly T[] _items;

    /// <summary>
    /// Creates the stream over <paramref name="items"/>, which it yields in the order given.
    /// </summary>
    /// <param name="items">The elements to yield.</param>
    internal TestAsyncStream(params T[] items) => _items = items;

    /// <summary>The token the consumer passed to <see cref="GetAsyncEnumerator"/>.</summary>
    internal CancellationToken EnumeratorToken { get; private set; }

    /// <summary>How many elements the consumer actually received.</summary>
    internal int Delivered { get; private set; }

    /// <summary>Whether the consumer disposed the enumerator.</summary>
    internal bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        EnumeratorToken = cancellationToken;
        Delivered = 0;
        IsDisposed = false;
        return new Enumerator(this);
    }

    private sealed class Enumerator : IAsyncEnumerator<T>
    {
        private readonly TestAsyncStream<T> _owner;
        private int _index = -1;

        internal Enumerator(TestAsyncStream<T> owner) => _owner = owner;

        public T Current => _owner._items[_index];

        public ValueTask<bool> MoveNextAsync()
        {
            if (_index + 1 >= _owner._items.Length)
            {
                return new ValueTask<bool>(false);
            }

            _index++;
            _owner.Delivered++;
            return new ValueTask<bool>(true);
        }

        public ValueTask DisposeAsync()
        {
            _owner.IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>
/// A hand-written async sequence that cancels the token source it was built with after a fixed
/// number of delivered elements, and then keeps yielding: it never looks at the token it receives,
/// so it stands in for a source that does not observe cancellation on its own. Only a consumer that
/// checks the token itself can turn that into an
/// <see cref="OperationCanceledException"/>.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal sealed class SelfCancellingStream<T> : IAsyncEnumerable<T>
{
    private readonly T[] _items;
    private readonly CancellationTokenSource _source;
    private readonly int _cancelAfter;

    /// <summary>
    /// Creates the stream over <paramref name="items"/>, cancelling <paramref name="source"/> after
    /// it delivered <paramref name="cancelAfter"/> elements.
    /// </summary>
    /// <param name="source">The token source this stream cancels from inside the enumeration.</param>
    /// <param name="cancelAfter">Delivered elements before the source is cancelled.</param>
    /// <param name="items">The elements to yield.</param>
    internal SelfCancellingStream(CancellationTokenSource source, int cancelAfter, params T[] items)
    {
        _source = source;
        _cancelAfter = cancelAfter;
        _items = items;
    }

    /// <summary>How many elements the consumer actually received.</summary>
    internal int Delivered { get; private set; }

    /// <inheritdoc />
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        Delivered = 0;
        return new Enumerator(this);
    }

    private sealed class Enumerator : IAsyncEnumerator<T>
    {
        private readonly SelfCancellingStream<T> _owner;
        private int _index = -1;

        internal Enumerator(SelfCancellingStream<T> owner) => _owner = owner;

        public T Current => _owner._items[_index];

        public ValueTask<bool> MoveNextAsync()
        {
            if (_index + 1 >= _owner._items.Length)
            {
                return new ValueTask<bool>(false);
            }

            _index++;
            _owner.Delivered++;
            if (_owner.Delivered == _owner._cancelAfter)
            {
                _owner._source.Cancel();
            }

            return new ValueTask<bool>(true);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
