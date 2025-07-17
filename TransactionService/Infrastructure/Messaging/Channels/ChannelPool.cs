using System.Collections.Concurrent;
using System.Diagnostics;
using RabbitMQ.Client;

namespace TransactionService.Infrastructure.Messaging.Channels;

public sealed class ChannelPool : IChannelPool
{
    private readonly IConnection _connection;
    private readonly ConcurrentQueue<(IChannel Channel, long LastUse)> _idle = new();
    private readonly TimeSpan _idleCutoff = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim _slots;
    private readonly Task _sweeper;
    private readonly PeriodicTimer _sweepTimer;

    private readonly int poolSize = 500; // Adjust the pool size as needed
    private readonly int minSize = 1;
    

    public ChannelPool(IConnection connection)
    {
        _connection = connection;
        _slots = new SemaphoreSlim(poolSize, poolSize);
        _sweepTimer = new(TimeSpan.FromMinutes(1));
        _sweeper = Task.Run(SweepWatchAsync);
    }

    /// Acquire a channel (async-friendly). DisposeAsync() returns it.
    public async ValueTask<ChannelLease> RentAsync(CancellationToken ct = default)
    {
        // waits if every channel is busy
        await _slots.WaitAsync(ct);

        if (!_idle.TryDequeue(out var tuple) || !tuple.Channel.IsOpen)
        {
            // none idle ⇒ create a new one (still within poolSize because of the semaphore)
            var channel = await _connection.CreateChannelAsync(cancellationToken: ct);

            tuple = (channel, Stopwatch.GetTimestamp());
            // any per-channel QoS (prefetch) or confirms mode here
        }

        return new ChannelLease(this, tuple.Channel);
    }

    public async Task Return(IChannel channel)
    {
        if (!channel.IsOpen)
        {
            try
            {
                await channel.DisposeAsync();
            }
            finally
            {
                channel = await _connection.CreateChannelAsync();
            }
        }

        _idle.Enqueue((channel, Stopwatch.GetTimestamp()));
        _slots.Release();
    }

    private async Task SweepWatchAsync()
    {
        try
        {
            while(await _sweepTimer.WaitForNextTickAsync())
            {
                await SweepAsync();
            }
        }finally{ }
    }

    private async Task SweepAsync()
    {
        var nowTicks = Stopwatch.GetTimestamp();

        var idleNow = _idle.Count;
        if (idleNow >= minSize &&
            idleNow > 0 &&
            _idle.TryPeek(out var head) &&
            TimestampOlderThanCutoff(head.LastUse, nowTicks) &&
            _idle.TryDequeue(out var old))
        {
            await old.Channel.DisposeAsync();
        }
    }

    private bool TimestampOlderThanCutoff(long lastUse, long nowTicks)
    {
        return nowTicks - lastUse >= _idleCutoff.Ticks;
    }

    public async ValueTask DisposeAsync()
    {
        while (_idle.TryDequeue(out var tuple))
        {
            await tuple.Channel.DisposeAsync();
        }
        _slots.Dispose();
        await _connection.DisposeAsync();
        await ValueTask.CompletedTask;
    }
}

