using Stl.Caching;

namespace Stl.Tests.Caching;

public class ComputingCacheTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public async Task ComputingCache_OrderByDependencyTest()
    {
        await OrderByDependencyTest(computer => new ComputingCache<char, char>(
            new MemoizingCache<char, char>(),
            computer));
    }

    [Fact]
    public async Task FastComputingCache_OrderByDependencyTest()
    {
        await OrderByDependencyTest(computer => new FastComputingCache<char, char>(computer));
    }

    private async Task OrderByDependencyTest(
        Func<
            Func<char, CancellationToken, ValueTask<char>>,
            IAsyncKeyResolver<char, char>> cacheFactory)
    {
        IEnumerable<char> DepSelector1(char c)
            => Enumerable
                .Range(0, c - '0')
                .Select(i => (char) ('0' + i));
        IEnumerable<char> BadDepSelector1(char c) => new [] {c};
        IEnumerable<char> BadDepSelector2(char c)
            => Enumerable
                .Range(1, 5)
                .Select(i => (char) ('0' + (c - '0' + i) % 10));


        async Task<string> OrderByDependency(string s, Func<char, IEnumerable<char>> depSelector)
        {
            var result = new List<char>();

            IAsyncKeyResolver<char, char>? cache = null;

            async ValueTask<char> Compute(char c, CancellationToken ct)
            {
#pragma warning disable MA0012
                if (cache == null)
                    throw new NullReferenceException();
#pragma warning restore MA0012
                foreach (var d in depSelector(c))
                    // ReSharper disable once AccessToModifiedClosure
                    await cache.Get(d, ct).ConfigureAwait(false);
                result.Add(c);
                return c;
            }

            cache = cacheFactory(Compute);
            await cache.GetManyAsync(s.ToAsyncEnumerable()).CountAsync();
            return result.ToDelimitedString("");
        }

        Assert.Equal("", await OrderByDependency("", DepSelector1));
        Assert.Equal("01", await OrderByDependency("1", DepSelector1));
        Assert.Equal("012", await OrderByDependency("12", DepSelector1));
        Assert.Equal("012", await OrderByDependency("21", DepSelector1));
        Assert.Equal("0123", await OrderByDependency("231", DepSelector1));

        await Assert.ThrowsAsync<InvalidOperationException>(async () => {
            _ = await OrderByDependency("0", BadDepSelector1);
        });
        await Assert.ThrowsAsync<InvalidOperationException>(async () => {
            _ = await OrderByDependency("0", BadDepSelector2);
        });
    }
}
