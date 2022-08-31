
using System.Threading.Channels;

Task.Run(async delegate
{
    while (true)
    {
        // Use aggressive GC to help bring out object lifetime issues.
        GC.Collect();
        GC.WaitForPendingFinalizers();

        await Task.Delay(TimeSpan.FromSeconds(1));
    }
});

_ = Task.Run(async () =>
{
    var foo = new Foo();

    try
    {
        Console.WriteLine("Starting work");
        
        //await foo.DoStuffAsync(CancellationToken.None);

        // ALTERNATIVE: If I instead provide a CT from a fresh CTS, everything works OK.
        //using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        //await foo.DoStuffAsync(cts.Token);
    }
    finally
    {
        await foo.DisposeAsync();
        Console.WriteLine("Work done, Foo disposed");
    }
});

// Just wait.
Console.ReadLine();

sealed class Foo : IAsyncDisposable
{
    ~Foo()
    {
        Console.WriteLine("!!!Finalizer!!!");
    }

    private readonly Channel<IntPtr> _channel = Channel.CreateBounded<IntPtr>(new BoundedChannelOptions(100)
    {
    });

    public async Task DoStuffAsync(CancellationToken cancel = default)
    {
        try
        {
            // Nobody writes into channel, so this should stay in the wait until cancelled.
            while (await _channel.Reader.WaitToReadAsync(cancel))
            {
                Console.WriteLine("Channel wait returned true."); // Never happens.
            }

            // ALTERNATIVE: If I instead do this, finalizer is not executed and we get to DisposeAsync().
            //await Task.Delay(TimeSpan.FromSeconds(5));
        }
        finally
        {
            await DisposeAsync();
        }
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        return new ValueTask();
    }

    // ALTERNATIVE: If I uncomment this, the finalizer is not executed and we get to DisposeAsync().
    //public Foo()
    //{
    //    Task.Run(async delegate
    //    {
    //        while (true)
    //        {
    //            if (File.Exists("C:\\temp\\foo"))
    //                _channel.Writer.TryComplete();
    //        }
    //    });
    //}
}