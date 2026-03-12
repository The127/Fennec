using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Fennec.Api.Tests;

internal static class DbSetMockExtensions
{
    public static DbSet<T> BuildMockDbSet<T>(this IEnumerable<T> data) where T : class
    {
        var queryable = new TestAsyncEnumerable<T>(data);
        var mockSet = Substitute.For<DbSet<T>, IQueryable<T>, IAsyncEnumerable<T>>();

        ((IQueryable<T>)mockSet).Provider.Returns(queryable.AsQueryable().Provider);
        ((IQueryable<T>)mockSet).Expression.Returns(queryable.AsQueryable().Expression);
        ((IQueryable<T>)mockSet).ElementType.Returns(queryable.AsQueryable().ElementType);
        ((IQueryable<T>)mockSet).GetEnumerator().Returns(_ => queryable.AsQueryable().GetEnumerator());
        ((IAsyncEnumerable<T>)mockSet).GetAsyncEnumerator(Arg.Any<CancellationToken>())
            .Returns(_ => queryable.GetAsyncEnumerator());

        return mockSet;
    }
}
