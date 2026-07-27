using Avalonia.Headless;

namespace Pica.Tests.Common;

public static class HeadlessTestSessionDispatcher
{
    public static void Dispatch(Type testApplicationType, Action action)
    {
        ArgumentNullException.ThrowIfNull(testApplicationType);
        ArgumentNullException.ThrowIfNull(action);

        DispatchCore(testApplicationType, action);
    }

    public static void Dispatch(
        Type testApplicationType,
        SemaphoreSlim sessionLock,
        Action action)
    {
        ArgumentNullException.ThrowIfNull(testApplicationType);
        ArgumentNullException.ThrowIfNull(sessionLock);
        ArgumentNullException.ThrowIfNull(action);

        sessionLock.Wait();

        try
        {
            DispatchCore(testApplicationType, action);
        }
        finally
        {
            sessionLock.Release();
        }
    }

    public static async Task DispatchAsync(
        Type testApplicationType,
        Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(testApplicationType);
        ArgumentNullException.ThrowIfNull(action);

        await DispatchCoreAsync(testApplicationType, action).ConfigureAwait(false);
    }

    public static async Task DispatchAsync(
        Type testApplicationType,
        SemaphoreSlim sessionLock,
        Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(testApplicationType);
        ArgumentNullException.ThrowIfNull(sessionLock);
        ArgumentNullException.ThrowIfNull(action);

        await sessionLock.WaitAsync().ConfigureAwait(false);

        try
        {
            await DispatchCoreAsync(testApplicationType, action).ConfigureAwait(false);
        }
        finally
        {
            sessionLock.Release();
        }
    }

    private static void DispatchCore(Type testApplicationType, Action action)
    {
        using HeadlessUnitTestSession session =
            HeadlessUnitTestSession.StartNew(testApplicationType);

        session.Dispatch(action, CancellationToken.None);
    }

    private static async Task DispatchCoreAsync(
        Type testApplicationType,
        Func<Task> action)
    {
        await using HeadlessUnitTestSession session =
            HeadlessUnitTestSession.StartNew(testApplicationType);
        await session
            .Dispatch(
                async () =>
                {
                    await action();

                    return true;
                },
                CancellationToken.None)
            .ConfigureAwait(false);
    }
}
