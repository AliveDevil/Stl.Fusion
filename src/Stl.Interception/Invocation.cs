using Stl.Interception.Internal;
using Stl.OS;

namespace Stl.Interception;

public readonly record struct Invocation(
    object Proxy,
    object? ProxyTarget,
    MethodInfo Method,
    ArgumentList Arguments,
    Delegate InterceptedDelegate)
{
    private static readonly MethodInfo InterceptedAsObjectMethod = typeof(Invocation)
        .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
        .Single(m => StringComparer.Ordinal.Equals(m.Name, nameof(InterceptedAsObject)));
    private static readonly ConcurrentDictionary<Type, Func<Invocation, object?>> InterceptedUntypedCache 
        = new(HardwareInfo.GetProcessorCountPo2Factor(4), 256);

    public void Intercepted()
    {
        if (InterceptedDelegate is Action<ArgumentList> action)
            action.Invoke(Arguments);
        else
            throw Errors.InvalidInterceptedDelegate();
    }

    public TResult Intercepted<TResult>()
    {
        return InterceptedDelegate is Func<ArgumentList, TResult> func
            ? func.Invoke(Arguments)
            : throw Errors.InvalidInterceptedDelegate();
    }

    public object? InterceptedUntyped()
        => InterceptedUntypedCache
            .GetOrAdd(Method.ReturnType, static returnType => {
                return returnType == typeof(void)
                    ? invocation => {
                        invocation.Intercepted();
                        return null;
                    }
                    : (Func<Invocation, object?>)InterceptedAsObjectMethod
                        .MakeGenericMethod(returnType)
                        .CreateDelegate(typeof(Func<Invocation, object?>));
            }).Invoke(this);

    // Private methods

    private static object? InterceptedAsObject<T>(Invocation invocation)
        // ReSharper disable once HeapView.PossibleBoxingAllocation
        => invocation.Intercepted<T>();
};
