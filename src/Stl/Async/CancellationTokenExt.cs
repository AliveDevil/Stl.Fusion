namespace Stl.Async;

public static class CancellationTokenExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CancellationTokenSource LinkWith(this CancellationToken token1, CancellationToken token2)
        => CancellationTokenSource.CreateLinkedTokenSource(token1, token2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CancellationTokenSource CreateLinkedTokenSource(this CancellationToken cancellationToken)
        => CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

    // ToTask

    public static Disposable<Task, (TaskCompletionSource<Unit>, CancellationTokenRegistration)> ToTask(
        this CancellationToken token,
        TaskCreationOptions taskCreationOptions = default)
    {
        var tcs = TaskCompletionSourceExt.New<Unit>(taskCreationOptions);
        var r = token.Register(() => tcs.TrySetCanceled(token));
#if NETSTANDARD
        return Disposable.New((Task)tcs.Task, (tcs, r), (_, state) => {
            state.r.Dispose();
            state.tcs.TrySetCanceled();
        });
#else
        return Disposable.New((Task)tcs.Task, (tcs, r), (_, state) => {
            state.r.Unregister();
            state.tcs.TrySetCanceled();
        });
#endif
    }

    public static Disposable<Task<T>, (TaskCompletionSource<T>, CancellationTokenRegistration)> ToTask<T>(
        this CancellationToken token,
        TaskCreationOptions taskCreationOptions = default)
    {
        var tcs = TaskCompletionSourceExt.New<T>(taskCreationOptions);
        var r = token.Register(() => tcs.TrySetCanceled(token));
#if NETSTANDARD
        return Disposable.New(tcs.Task, (tcs, r), (_, state) => {
            state.r.Dispose();
            state.tcs.TrySetCanceled();
        });
#else
        return Disposable.New(tcs.Task, (tcs, r), (_, state) => {
            state.r.Unregister();
            state.tcs.TrySetCanceled();
        });
#endif
    }

    // ToTaskUnsafe

    internal static Task ToTaskUnsafe(
        this CancellationToken token,
        TaskCreationOptions taskCreationOptions = default)
    {
        var tcs = TaskCompletionSourceExt.New<Unit>(taskCreationOptions);
        token.Register(() => tcs.TrySetCanceled(token));
        return tcs.Task;
    }

    internal static Task<T> ToTaskUnsafe<T>(
        this CancellationToken token,
        TaskCreationOptions taskCreationOptions = default)
    {
        var tcs = TaskCompletionSourceExt.New<T>(taskCreationOptions);
        token.Register(() => tcs.TrySetCanceled(token));
        return tcs.Task;
    }
}
