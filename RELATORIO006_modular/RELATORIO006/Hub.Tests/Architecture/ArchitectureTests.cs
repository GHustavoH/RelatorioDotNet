using System.Reflection;
using System.Xml.Linq;
using Hub.Notifications;
using Hub.Orders.Domain;
using Hub.Orders.Ports;
using Hub.Processing;

namespace Hub.Tests.Architecture;

//Torna a direção das dependências VERIFICÁVEL
// inspeciona os .csproj (referências declaradas) e os assemblies (referências reais).
public class ArchitectureTests
{
    private static readonly Assembly OrdersAsm = typeof(Order).Assembly;
    private static readonly Assembly ProcessingAsm = typeof(ProcessOrderHandler).Assembly;
    private static readonly Assembly NotificationsAsm = typeof(NotificationService).Assembly;
    private static readonly Assembly InfraAsm = typeof(Hub.Infrastructure.Modules.OrdersModule).Assembly;

    // projeto = projetos que ele PODE referenciar
    private static readonly Dictionary<string, string[]> AllowedProjectReferences = new()
    {
        ["Hub.Orders"] = [],
        ["Hub.Notifications"] = [],
        ["Hub.Processing"] = ["Hub.Orders"],
        ["Hub.Infrastructure"] = ["Hub.Orders", "Hub.Processing", "Hub.Notifications"],
        ["RELATORIO006"] = ["Hub.Orders", "Hub.Infrastructure"],
    };

    [Theory]
    [InlineData("Hub.Orders")]
    [InlineData("Hub.Notifications")]
    [InlineData("Hub.Processing")]
    [InlineData("Hub.Infrastructure")]
    [InlineData("RELATORIO006")]
    public void ProjectReferences_FollowAllowedDirection(string project)
    {
        var csproj = LoadCsproj(project);
        var actual = csproj.Descendants("ProjectReference")
            .Select(e => Path.GetFileNameWithoutExtension((string)e.Attribute("Include")!.Value.Replace('\\', '/')))
            .OrderBy(x => x)
            .ToArray();

        var allowed = AllowedProjectReferences[project];
        var forbidden = actual.Except(allowed).ToArray();

        Assert.True(forbidden.Length == 0,
            $"{project} referencia projetos proibidos: {string.Join(", ", forbidden)}");
    }

    [Theory]
    [InlineData("Hub.Orders")]
    [InlineData("Hub.Notifications")]
    [InlineData("Hub.Processing")]
    public void ModuleProjects_HaveNoPackagesNorFrameworkReferences(string project)
    {
        // Garante: domínio/módulos compilam sem API (ASP.NET) e sem EF Core.
        var csproj = LoadCsproj(project);

        Assert.Empty(csproj.Descendants("PackageReference"));
        Assert.Empty(csproj.Descendants("FrameworkReference"));
        Assert.Equal("Microsoft.NET.Sdk", (string?)csproj.Root!.Attribute("Sdk"));
    }

    [Fact]
    public void OrdersDomain_DoesNotReferenceApiOrEfCoreOrOtherModules()
    {
        var forbiddenPrefixes = new[]
        {
            "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Microsoft.Extensions.DependencyInjection",
            "Hub.Processing", "Hub.Notifications", "Hub.Infrastructure", "RELATORIO006"
        };

        AssertNoReferences(OrdersAsm, forbiddenPrefixes);
    }

    [Fact]
    public void Notifications_DoesNotReferenceOtherModulesOrApi()
    {
        AssertNoReferences(NotificationsAsm,
            "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Hub.Orders", "Hub.Processing",
            "Hub.Infrastructure", "RELATORIO006");
    }

    [Fact]
    public void Processing_DoesNotReferenceNotificationsInfraOrApi()
    {
        AssertNoReferences(ProcessingAsm,
            "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Hub.Notifications",
            "Hub.Infrastructure", "RELATORIO006");
    }

    [Fact]
    public void Ports_AreDeclaredInConsumerBoundary_AndImplementedOnlyInInfrastructure()
    {
        // Interfaces nas fronteiras que as consomem
        Assert.Same(OrdersAsm, typeof(IOrderRepository).Assembly);
        Assert.Same(OrdersAsm, typeof(IOrderEventPublisher).Assembly);
        Assert.Same(ProcessingAsm, typeof(IProcessingNotifier).Assembly);
        Assert.Same(NotificationsAsm, typeof(INotificationSender).Assembly);

        // e implementações na infraestrutura.
        var ports = new[]
        {
            typeof(IOrderRepository), typeof(IOrderEventPublisher),
            typeof(IProcessingNotifier), typeof(INotificationSender)
        };

        foreach (var port in ports)
        {
            var implementers = new[] { OrdersAsm, ProcessingAsm, NotificationsAsm }
                .SelectMany(a => a.GetTypes())
                .Where(t => t is { IsClass: true, IsAbstract: false } && port.IsAssignableFrom(t))
                .ToArray();

            Assert.True(implementers.Length == 0,
                $"{port.Name} não pode ser implementada dentro de módulos: {string.Join(", ", implementers.Select(t => t.Name))}");

            Assert.Contains(InfraAsm.GetTypes(), t => t is { IsClass: true, IsAbstract: false } && port.IsAssignableFrom(t));
        }
    }

    [Fact]
    public void BusinessCode_DoesNotResolveServicesManually()
    {
        // Nenhum construtor/serviço de módulo recebe IServiceProvider / IServiceScopeFactory (service locator).
        var offenders = new[] { OrdersAsm, ProcessingAsm, NotificationsAsm }
            .SelectMany(a => a.GetTypes())
            .SelectMany(t => t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            .SelectMany(c => c.GetParameters())
            .Where(p => p.ParameterType == typeof(IServiceProvider)
                        || p.ParameterType.Name == "IServiceScopeFactory"
                        || p.ParameterType.Name == "IServiceProviderIsService")
            .Select(p => $"{p.Member.DeclaringType?.Name}({p.ParameterType.Name})")
            .ToArray();

        Assert.Empty(offenders);
    }

    // ---------- helpers ----------

    private static void AssertNoReferences(Assembly assembly, params string[] forbiddenPrefixes)
    {
        var referenced = assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToArray();
        var violations = referenced
            .Where(name => forbiddenPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)))
            .ToArray();

        Assert.True(violations.Length == 0,
            $"{assembly.GetName().Name} referencia assemblies proibidos: {string.Join(", ", violations)}");
    }

    private static XDocument LoadCsproj(string project)
    {
        var root = FindSolutionRoot();
        var path = Path.Combine(root, project, project + ".csproj");
        Assert.True(File.Exists(path), $"csproj não encontrado: {path}");
        return XDocument.Load(path);
    }

    internal static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "RELATORIO006.slnx")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new InvalidOperationException("RELATORIO006.slnx não encontrado acima de " + AppContext.BaseDirectory);
    }
}
