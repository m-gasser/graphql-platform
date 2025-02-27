using HotChocolate.Execution.Caching;
using HotChocolate.Types;
using Microsoft.Extensions.DependencyInjection;

namespace HotChocolate.Execution;

public class RequestExecutorResolverTests
{
    [Fact]
    public async Task Operation_Cache_Should_Be_Scoped_To_Executor()
    {
        // arrange
        var executorEvictedResetEvent = new AutoResetEvent(false);

        var resolver = new ServiceCollection()
            .AddGraphQL()
            .AddQueryType(d => d.Field("foo").Resolve(""))
            .Services.BuildServiceProvider()
            .GetRequiredService<IRequestExecutorResolver>();

        resolver.Events.Subscribe(new RequestExecutorEventObserver(@event =>
        {
            if (@event.Type == RequestExecutorEventType.Evicted)
            {
                executorEvictedResetEvent.Set();
            }
        }));

        // act
        var firstExecutor = await resolver.GetRequestExecutorAsync();
        var firstOperationCache = firstExecutor.Services.GetCombinedServices()
            .GetRequiredService<IPreparedOperationCache>();

        resolver.EvictRequestExecutor();
        executorEvictedResetEvent.WaitOne(1000);

        var secondExecutor = await resolver.GetRequestExecutorAsync();
        var secondOperationCache = secondExecutor.Services.GetCombinedServices()
            .GetRequiredService<IPreparedOperationCache>();

        // assert
        Assert.NotSame(secondOperationCache, firstOperationCache);
    }

    [Fact]
    public async Task Executor_Should_Only_Be_Switched_Once_It_Is_Warmed_Up()
    {
        // arrange
        var warmupResetEvent = new AutoResetEvent(true);
        var executorEvictedResetEvent = new AutoResetEvent(false);

        var resolver = new ServiceCollection()
            .AddGraphQL()
            .InitializeOnStartup(
                keepWarm: true,
                warmup: (_, _) =>
                {
                    warmupResetEvent.WaitOne(1000);

                    return Task.CompletedTask;
                })
            .AddQueryType(d => d.Field("foo").Resolve(""))
            .Services.BuildServiceProvider()
            .GetRequiredService<IRequestExecutorResolver>();

        resolver.Events.Subscribe(new RequestExecutorEventObserver(@event =>
        {
            if (@event.Type == RequestExecutorEventType.Evicted)
            {
                executorEvictedResetEvent.Set();
            }
        }));

        // act
        // assert
        var initialExecutor = await resolver.GetRequestExecutorAsync();
        warmupResetEvent.Reset();

        resolver.EvictRequestExecutor();

        var executorAfterEviction = await resolver.GetRequestExecutorAsync();

        Assert.Same(initialExecutor, executorAfterEviction);

        warmupResetEvent.Set();
        executorEvictedResetEvent.WaitOne(1000);
        var executorAfterWarmup = await resolver.GetRequestExecutorAsync();

        Assert.NotSame(initialExecutor, executorAfterWarmup);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public async Task WarmupSchemaTasks_Are_Applied_Correct_Number_Of_Times(
        bool keepWarm, int expectedWarmups)
    {
        // arrange
        var warmups = 0;
        var executorEvictedResetEvent = new AutoResetEvent(false);

        var resolver = new ServiceCollection()
            .AddGraphQL()
            .InitializeOnStartup(
                keepWarm: keepWarm,
                warmup: (_, _) =>
                {
                    warmups++;
                    return Task.CompletedTask;
                })
            .AddQueryType(d => d.Field("foo").Resolve(""))
            .Services.BuildServiceProvider()
            .GetRequiredService<IRequestExecutorResolver>();

        resolver.Events.Subscribe(new RequestExecutorEventObserver(@event =>
        {
            if (@event.Type == RequestExecutorEventType.Evicted)
            {
                executorEvictedResetEvent.Set();
            }
        }));

        // act
        // assert
        var initialExecutor = await resolver.GetRequestExecutorAsync();

        resolver.EvictRequestExecutor();
        executorEvictedResetEvent.WaitOne(1000);

        var executorAfterEviction = await resolver.GetRequestExecutorAsync();

        Assert.NotSame(initialExecutor, executorAfterEviction);
        Assert.Equal(expectedWarmups, warmups);
    }

    [Fact]
    public async Task Calling_GetExecutorAsync_Multiple_Times_Only_Creates_One_Executor()
    {
        // arrange
        var resolver = new ServiceCollection()
            .AddGraphQL()
            .AddQueryType(d =>
            {
                d.Field("foo").Resolve("");
            })
            .Services.BuildServiceProvider()
            .GetRequiredService<IRequestExecutorResolver>();

        // act
        var executor1Task = Task.Run(async () => await resolver.GetRequestExecutorAsync());
        var executor2Task = Task.Run(async () => await resolver.GetRequestExecutorAsync());

        var executor1 = await executor1Task;
        var executor2 = await executor2Task;

        // assert
        Assert.Same(executor1, executor2);
    }

    [Fact]
    public async Task Executor_Resolution_Should_Be_Parallel()
    {
        // arrange
        var schema1CreationResetEvent = new AutoResetEvent(false);

        var services = new ServiceCollection();
        services
            .AddGraphQL("schema1")
            .AddQueryType(d =>
            {
                schema1CreationResetEvent.WaitOne(1000);
                d.Field("foo").Resolve("");
            });
        services
            .AddGraphQL("schema2")
            .AddQueryType(d =>
            {
                d.Field("foo").Resolve("");
            });
        var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<IRequestExecutorResolver>();

        // act
        var executor1Task = Task.Run(async () => await resolver.GetRequestExecutorAsync("schema1"));
        var executor2Task = Task.Run(async () => await resolver.GetRequestExecutorAsync("schema2"));

        // assert
        await executor2Task;

        schema1CreationResetEvent.Set();

        await executor1Task;
    }
}
