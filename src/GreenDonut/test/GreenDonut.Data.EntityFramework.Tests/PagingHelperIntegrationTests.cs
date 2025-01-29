using GreenDonut.Data.TestContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Squadron;

namespace GreenDonut.Data;

[Collection(PostgresCacheCollectionFixture.DefinitionName)]
public class IntegrationPagingHelperTests(PostgreSqlResource resource)
{
    public PostgreSqlResource Resource { get; } = resource;

    private string CreateConnectionString()
        => Resource.GetConnectionString($"db_{Guid.NewGuid():N}");

    [Fact]
    public async Task Paging_Empty_PagingArgs()
    {
        // Arrange
        var connectionString = CreateConnectionString();
        await SeedAsync(connectionString);
        var queries = new List<QueryInfo>();
        using var capture = new CapturePagingQueryInterceptor(queries);

        // Act
        await using var context = new CatalogContext(connectionString);

        var pagingArgs = new PagingArguments();
        var result = await context.Brands.OrderBy(t => t.Name).ThenBy(t => t.Id).ToPageAsync(pagingArgs);

        // Assert
        await Snapshot.Create()
            .AddQueries(queries)
            .Add(
                new
                {
                    result.HasNextPage,
                    result.HasPreviousPage,
                    First = result.First?.Id,
                    FirstCursor = result.First is not null ? result.CreateCursor(result.First) : null,
                    Last = result.Last?.Id,
                    LastCursor = result.Last is not null ? result.CreateCursor(result.Last) : null
                })
            .Add(result.Items)
            .MatchMarkdownAsync();
    }

    [Fact]
    public async Task Paging_First_5()
    {
        // Arrange
        var connectionString = CreateConnectionString();
        await SeedAsync(connectionString);
        var queries = new List<QueryInfo>();
        using var capture = new CapturePagingQueryInterceptor(queries);

        // Act
        await using var context = new CatalogContext(connectionString);

        var pagingArgs = new PagingArguments { First = 5 };
        var result = await context.Brands.OrderBy(t => t.Name).ThenBy(t => t.Id).ToPageAsync(pagingArgs);

        // Assert
        await Snapshot.Create()
            .AddQueries(queries)
            .Add(
                new
                {
                    result.HasNextPage,
                    result.HasPreviousPage,
                    First = result.First?.Id,
                    FirstCursor = result.First is not null ? result.CreateCursor(result.First) : null,
                    Last = result.Last?.Id,
                    LastCursor = result.Last is not null ? result.CreateCursor(result.Last) : null
                })
            .Add(result.Items)
            .MatchMarkdownAsync();
    }

    [Fact]
    public async Task Paging_First_5_After_Id_13()
    {
        // Arrange
        var connectionString = CreateConnectionString();
        await SeedAsync(connectionString);
        var queries = new List<QueryInfo>();
        using var capture = new CapturePagingQueryInterceptor(queries);

        // Act
        await using var context = new CatalogContext(connectionString);

        var pagingArgs = new PagingArguments
        {
            First = 5,
            After = "QnJhbmQxMjoxMw=="
        };
        var result = await context.Brands.OrderBy(t => t.Name).ThenBy(t => t.Id).ToPageAsync(pagingArgs);

        // Assert
        await Snapshot.Create()
            .AddQueries(queries)
            .Add(
                new
                {
                    result.HasNextPage,
                    result.HasPreviousPage,
                    First = result.First?.Id,
                    FirstCursor = result.First is not null ? result.CreateCursor(result.First) : null,
                    Last = result.Last?.Id,
                    LastCursor = result.Last is not null ? result.CreateCursor(result.Last) : null
                })
            .Add(result.Items)
            .MatchMarkdownAsync();
    }

    [Fact]
    public async Task Paging_Last_5()
    {
        // Arrange
        var connectionString = CreateConnectionString();
        await SeedAsync(connectionString);
        var queries = new List<QueryInfo>();
        using var capture = new CapturePagingQueryInterceptor(queries);

        // Act
        await using var context = new CatalogContext(connectionString);

        var pagingArgs = new PagingArguments { Last = 5 };
        var result = await context.Brands.OrderBy(t => t.Name).ThenBy(t => t.Id).ToPageAsync(pagingArgs);

        // Assert
        await Snapshot.Create()
            .AddQueries(queries)
            .Add(
                new
                {
                    result.HasNextPage,
                    result.HasPreviousPage,
                    First = result.First?.Id,
                    FirstCursor = result.First is not null ? result.CreateCursor(result.First) : null,
                    Last = result.Last?.Id,
                    LastCursor = result.Last is not null ? result.CreateCursor(result.Last) : null
                })
            .Add(result.Items)
            .MatchMarkdownAsync();
    }

    [Fact]
    public async Task Paging_First_5_Before_Id_96()
    {
        // Arrange
        var connectionString = CreateConnectionString();
        await SeedAsync(connectionString);
        var queries = new List<QueryInfo>();
        using var capture = new CapturePagingQueryInterceptor(queries);

        // Act
        await using var context = new CatalogContext(connectionString);

        var pagingArgs = new PagingArguments
        {
            Last = 5,
            Before = "QnJhbmQ5NTo5Ng=="
        };
        var result = await context.Brands.OrderBy(t => t.Name).ThenBy(t => t.Id).ToPageAsync(pagingArgs);

        // Assert
        await Snapshot.Create()
            .AddQueries(queries)
            .Add(
                new
                {
                    result.HasNextPage,
                    result.HasPreviousPage,
                    First = result.First?.Id,
                    FirstCursor = result.First is not null ? result.CreateCursor(result.First) : null,
                    Last = result.Last?.Id,
                    LastCursor = result.Last is not null ? result.CreateCursor(result.Last) : null
                })
            .Add(result.Items)
            .MatchMarkdownAsync();
    }

    [Fact]
    public async Task BatchPaging_First_5()
    {
        // Arrange
#if NET8_0
        var snapshot = Snapshot.Create();
#else
        var snapshot = Snapshot.Create("NET9_0");
#endif

        var connectionString = CreateConnectionString();
        await SeedAsync(connectionString);
        var queries = new List<QueryInfo>();
        using var capture = new CapturePagingQueryInterceptor(queries);

        // Act
        await using var context = new CatalogContext(connectionString);

        var pagingArgs = new PagingArguments { First = 2 };

        var results = await context.Products
            .Where(t => t.BrandId == 1 || t.BrandId == 2 || t.BrandId == 3)
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .ToBatchPageAsync(k => k.BrandId, pagingArgs);

        // Assert
        foreach (var page in results)
        {
            snapshot.Add(
                new
                {
                    First = page.Value.CreateCursor(page.Value.First!),
                    Last = page.Value.CreateCursor(page.Value.Last!),
                    page.Value.Items
                },
                name: page.Key.ToString());
        }

        snapshot.AddQueries(queries);
        snapshot.MatchMarkdownSnapshot();
    }

    [Fact]
    public async Task BatchPaging_Last_5()
    {
        // Arrange
#if NET8_0
        var snapshot = Snapshot.Create();
#else
        var snapshot = Snapshot.Create("NET9_0");
#endif

        var connectionString = CreateConnectionString();
        await SeedAsync(connectionString);
        var queries = new List<QueryInfo>();
        using var capture = new CapturePagingQueryInterceptor(queries);

        // Act
        await using var context = new CatalogContext(connectionString);

        var pagingArgs = new PagingArguments { Last = 2 };

        var results = await context.Products
            .Where(t => t.BrandId == 1 || t.BrandId == 2 || t.BrandId == 3)
            .OrderBy(p => p.Id)
            .ToBatchPageAsync(k => k.BrandId, pagingArgs);

        // Assert
        foreach (var page in results)
        {
            snapshot.Add(
                new
                {
                    First = page.Value.CreateCursor(page.Value.First!),
                    Last = page.Value.CreateCursor(page.Value.Last!),
                    page.Value.Items
                },
                name: page.Key.ToString());
        }

        snapshot.AddQueries(queries);
        snapshot.MatchMarkdownSnapshot();
    }

    private static async Task SeedAsync(string connectionString)
    {
        await using var context = new CatalogContext(connectionString);
        await context.Database.EnsureCreatedAsync();

        var type = new ProductType
        {
            Name = "T-Shirt",
        };
        context.ProductTypes.Add(type);

        for (var i = 0; i < 100; i++)
        {
            var brand = new Brand
            {
                Name = "Brand:" + i,
                DisplayName = i % 2 == 0 ? "BrandDisplay" + i : null,
                BrandDetails = new() { Country = new() { Name = "Country" + i } }
            };
            context.Brands.Add(brand);

            for (var j = 0; j < 100; j++)
            {
                var product = new Product
                {
                    Name = $"Product {i}-{j}",
                    Type = type,
                    Brand = brand,
                };
                context.Products.Add(product);
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedFooAsync(string connectionString)
    {
        await using var context = new FooBarContext(connectionString);
        await context.Database.EnsureCreatedAsync();

        context.Bars.Add(
            new Bar
            {
                Id = 1,
                Description = "Bar 1",
                SomeField1 = "abc",
                SomeField2 = null
            });

        context.Bars.Add(
            new Bar
            {
                Id = 2,
                Description = "Bar 2",
                SomeField1 = "def",
                SomeField2 = "ghi"
            });

        context.Foos.Add(
            new Foo
            {
                Id = 1,
                Name = "Foo 1",
                BarId = null
            });

        context.Foos.Add(
            new Foo
            {
                Id = 2,
                Name = "Foo 2",
                BarId = 1
            });

        await context.SaveChangesAsync();
    }

    public class BrandDto
    {
        public BrandDto(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public int Id { get; }

        public string Name { get; }
    }

    public class ProductsByBrandDataLoader : StatefulBatchDataLoader<int, Page<Product>>
    {
        private readonly IServiceProvider _services;

        public ProductsByBrandDataLoader(
            IServiceProvider services,
            IBatchScheduler batchScheduler,
            DataLoaderOptions options)
            : base(batchScheduler, options)
        {
            _services = services;
        }

        protected override async Task<IReadOnlyDictionary<int, Page<Product>>> LoadBatchAsync(
            IReadOnlyList<int> keys,
            DataLoaderFetchContext<Page<Product>> context,
            CancellationToken cancellationToken)
        {
            var pagingArgs = context.GetPagingArguments();
            var selector = context.GetSelector();

            await using var scope = _services.CreateAsyncScope();
            await using var catalogContext = scope.ServiceProvider.GetRequiredService<CatalogContext>();

            return await catalogContext.Products
                .Where(t => keys.Contains(t.BrandId))
                .Select(b => b.BrandId, selector)
                .OrderBy(t => t.Name).ThenBy(t => t.Id)
                .ToBatchPageAsync(t => t.BrandId, pagingArgs, cancellationToken);
        }
    }
}

file static class Extensions
{
    public static Snapshot AddQueries(
        this Snapshot snapshot,
        List<QueryInfo> queries)
    {
        for (var i = 0; i < queries.Count; i++)
        {
            snapshot
                .Add(queries[i].QueryText, $"SQL {i}", "sql")
                .Add(queries[i].ExpressionText, $"Expression {i}");
        }

        return snapshot;
    }
}

file sealed class CapturePagingQueryInterceptor(List<QueryInfo> queries) : PagingQueryInterceptor
{
    public override void OnBeforeExecute<T>(IQueryable<T> query)
    {
        queries.Add(
            new QueryInfo
            {
                ExpressionText = query.Expression.ToString(),
                QueryText = query.ToQueryString()
            });
    }
}
