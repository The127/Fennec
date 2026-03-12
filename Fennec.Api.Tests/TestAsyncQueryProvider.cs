using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace Fennec.Api.Tests;

internal class TestAsyncQueryProvider<T>(IQueryProvider inner) : IAsyncQueryProvider
{
    public IQueryable CreateQuery(Expression expression) => new TestAsyncEnumerable<T>(expression);
    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new TestAsyncEnumerable<TElement>(expression);
    public object? Execute(Expression expression) => inner.Execute(expression);
    public TResult Execute<TResult>(Expression expression) => inner.Execute<TResult>(expression);

    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        var resultType = typeof(TResult).GetGenericArguments()[0];
        var executionResult = typeof(IQueryProvider)
            .GetMethod(nameof(IQueryProvider.Execute), 1, [typeof(Expression)])!
            .MakeGenericMethod(resultType)
            .Invoke(inner, [expression]);

        return (TResult)typeof(Task)
            .GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(resultType)
            .Invoke(null, [executionResult])!;
    }
}

internal class TestAsyncEnumerable<T> : IQueryable<T>, IAsyncEnumerable<T>
{
    private readonly EnumerableQuery<T> _inner;
    private readonly TestAsyncQueryProvider<T> _provider;

    public TestAsyncEnumerable(IEnumerable<T> enumerable)
    {
        _inner = new EnumerableQuery<T>(enumerable);
        _provider = new TestAsyncQueryProvider<T>(((IQueryable<T>)_inner).Provider);
    }

    public TestAsyncEnumerable(Expression expression)
    {
        _inner = new EnumerableQuery<T>(expression);
        _provider = new TestAsyncQueryProvider<T>(((IQueryable<T>)_inner).Provider);
    }

    public Type ElementType => ((IQueryable<T>)_inner).ElementType;
    public Expression Expression => ((IQueryable<T>)_inner).Expression;
    public IQueryProvider Provider => _provider;

    public IEnumerator<T> GetEnumerator() => _inner.AsEnumerable().GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => new TestAsyncEnumerator<T>(_inner.AsEnumerable().GetEnumerator());
}

internal class TestAsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
{
    public T Current => inner.Current;
    public ValueTask DisposeAsync() { inner.Dispose(); return ValueTask.CompletedTask; }
    public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(inner.MoveNext());
}
