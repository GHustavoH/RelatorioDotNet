using System.Reflection;
using HubPedidos.Application.Contratos;
using HubPedidos.Application.Pedidos;
using HubPedidos.Domain.Pedidos;

namespace HubPedidos.Tests;

public class ArquiteturaTests
{
    private static readonly Assembly Dominio = typeof(Pedido).Assembly;
    private static readonly Assembly Aplicacao = typeof(ServicoPedidos).Assembly;

    private static readonly string[] PrefixosDeInfraestrutura =
    {
        "Npgsql",
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
        "System.Data"
    };

    [Fact]
    public void Dominio_NaoDependeDeAplicacaoNemInfraestrutura()
    {
        var referencias = Dominio.GetReferencedAssemblies().Select(a => a.Name!).ToArray();

        Assert.DoesNotContain(referencias, n => n.StartsWith("HubPedidos.Application") || n.StartsWith("HubPedidos.Api"));
        Assert.DoesNotContain(referencias, n => n.StartsWith("Microsoft.Extensions"));
        Assert.DoesNotContain(referencias, n => PrefixosDeInfraestrutura.Any(n.StartsWith));
    }

    [Fact]
    public void Aplicacao_NaoDependeDeInfraestruturaNemDaApi()
    {
        var referencias = Aplicacao.GetReferencedAssemblies().Select(a => a.Name!).ToArray();

        Assert.DoesNotContain(referencias, n => n.StartsWith("HubPedidos.Api"));
        Assert.DoesNotContain(referencias, n => PrefixosDeInfraestrutura.Any(n.StartsWith));
    }

    [Fact]
    public void Dtos_NaoExpoemTiposDoDominioNemDeInfraestrutura()
    {
        var dtos = Aplicacao.GetTypes()
            .Where(t => t.Namespace == typeof(CriarPedidoRequest).Namespace && !t.IsNested)
            .ToArray();

        Assert.NotEmpty(dtos);

        foreach (var dto in dtos)
        {
            var tiposUsados = dto
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.PropertyType)
                .Concat(dto.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType))
                .SelectMany(Achatar)
                .Distinct();

            foreach (var tipo in tiposUsados)
            {
                var ns = tipo.Namespace ?? string.Empty;
                Assert.False(ns.StartsWith("HubPedidos.Domain"), $"{dto.Name} expõe tipo do domínio: {tipo.FullName}");
                Assert.False(PrefixosDeInfraestrutura.Any(ns.StartsWith), $"{dto.Name} depende de infraestrutura: {tipo.FullName}");
            }
        }
    }

    private static IEnumerable<Type> Achatar(Type tipo)
    {
        yield return tipo;

        if (tipo.IsGenericType)
        {
            foreach (var argumento in tipo.GetGenericArguments().SelectMany(Achatar))
            {
                yield return argumento;
            }
        }

        if (tipo.IsArray && tipo.GetElementType() is { } elemento)
        {
            foreach (var interno in Achatar(elemento))
            {
                yield return interno;
            }
        }
    }
}
