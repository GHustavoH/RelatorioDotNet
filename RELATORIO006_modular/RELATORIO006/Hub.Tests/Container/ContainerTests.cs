using Hub.Infrastructure.Modules;
using Hub.Notifications;
using Hub.Orders.Application;
using Hub.Orders.Ports;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Hub.Tests.Container;

public class ContainerTests
{
    //Mesmos registros do composition root (Program.cs), sem o host web
    private static ServiceCollection BuildModuleCollection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOrdersModule();
        services.AddNotificationsModule();
        services.AddProcessingModule();
        return services;
    }

    [Fact]
    public void Graph_BuildsWithScopeAndBuildValidation_AndResolvesMainFlows()
    {
        var services = BuildModuleCollection();

        // ValidateOnBuild: todo grafo é construído; ValidateScopes: detecta scoped resolvido do root / cativo.
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IOrderService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IOrderEventPublisher>());
        Assert.Single(scope.ServiceProvider.GetServices<IOrderPlacedHandler>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<INotificationService>());
    }

    [Fact]
    public void NoSingleton_CapturesScopedService()
    {
        var captives = CaptiveDependencyScanner.Find(BuildModuleCollection());

        Assert.True(captives.Count == 0, "Dependências cativas: " + string.Join("; ", captives));
    }

    [Fact]
    public void Scanner_DetectsCaptiveDependency_WhenSingletonDependsOnScoped()
    {
        // Prova de que o detector funciona: cria propositalmente um singleton que captura um scoped.
        var services = new ServiceCollection();
        services.AddScoped<ScopedDependency>();
        services.AddSingleton<BadSingleton>();

        var captives = CaptiveDependencyScanner.Find(services);

        Assert.Single(captives);

        // E o próprio contêiner também recusa o grafo inválido.
        Assert.Throws<AggregateException>(() => services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        }));
    }

    [Fact]
    public void ComposedHost_StartsWithValidatedContainer()
    {
        // Constrói o composition root REAL (Program.cs) com validação estrita.
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseDefaultServiceProvider(o =>
            {
                o.ValidateScopes = true;
                o.ValidateOnBuild = true;
            });
        });

        using var client = factory.CreateClient(); // força a construção do host/contêiner
        using var scope = factory.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IOrderService>());
    }

    private sealed class ScopedDependency;

    private sealed class BadSingleton(ScopedDependency dependency)
    {
        public ScopedDependency Dependency { get; } = dependency;
    }
}

//Detecta singletons que, direta ou transitivamente, dependem de serviços scoped.
internal static class CaptiveDependencyScanner
{
    public static List<string> Find(IServiceCollection services)
    {
        var result = new List<string>();

        foreach (var singleton in services.Where(d => d.Lifetime == ServiceLifetime.Singleton && d.ImplementationType is not null))
        {
            Walk(services, singleton.ImplementationType!, singleton.ServiceType.Name, new HashSet<Type>(), result);
        }

        return result;
    }

    private static void Walk(IServiceCollection services, Type implementation, string root, HashSet<Type> visited, List<string> result)
    {
        if (!visited.Add(implementation))
            return;

        var constructor = implementation
            .GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault();

        if (constructor is null)
            return;

        foreach (var parameter in constructor.GetParameters())
        {
            var dependencyType = parameter.ParameterType;
            if (dependencyType.IsGenericType && dependencyType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                dependencyType = dependencyType.GetGenericArguments()[0];

            foreach (var dependency in services.Where(d => d.ServiceType == dependencyType))
            {
                if (dependency.Lifetime == ServiceLifetime.Scoped)
                    result.Add($"{root} -> {dependencyType.Name} (scoped)");
                else if (dependency.ImplementationType is not null)
                    Walk(services, dependency.ImplementationType, root, visited, result);
            }
        }
    }
}
