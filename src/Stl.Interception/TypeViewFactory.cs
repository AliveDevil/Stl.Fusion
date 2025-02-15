using Stl.Interception.Interceptors;

namespace Stl.Interception;

public interface ITypeViewFactory
{
    object CreateView(object implementation, Type viewType);
    TypeViewFactory<TView> For<TView>()
        where TView : class;
}

public class TypeViewFactory(TypeViewInterceptor interceptor) : ITypeViewFactory
{
    public static ITypeViewFactory Default { get; set; } =
        new TypeViewFactory(new TypeViewInterceptor(DependencyInjection.ServiceProviderExt.Empty));

    protected Interceptor Interceptor { get; } = interceptor;

    public object CreateView(object implementation, Type viewType)
    {
        if (!viewType.IsInterface)
            throw new ArgumentOutOfRangeException(nameof(viewType));

        return Proxies.New(viewType, Interceptor, implementation);
    }

    public TypeViewFactory<TView> For<TView>()
        where TView : class
        => new(this);
}

public readonly struct TypeViewFactory<TView>
    where TView : class
{
    public ITypeViewFactory Factory { get; }

    public TypeViewFactory(ITypeViewFactory factory) => Factory = factory;

    public TView CreateView(object implementation)
        => (TView) Factory.CreateView(implementation, typeof(TView));
}
