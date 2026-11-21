using HubPedidos.Application.Processamento;
using HubPedidos.Domain.Classificacao;
using HubPedidos.Domain.Pedidos;
using Microsoft.Extensions.DependencyInjection;

namespace HubPedidos.Tests;

public class CatalogoProcessadoresTests
{
    private static readonly System.Reflection.Assembly AssemblyConhecido = typeof(IProcessadorPedido).Assembly;

    [Fact]
    public void Construir_AssemblyConhecido_CobreTodasAsCategoriasDeNegocio()
    {
        var catalogo = CatalogoProcessadores.Construir(AssemblyConhecido, exigirCobertura: true);

        foreach (var categoria in Enum.GetValues<CategoriaProcessamento>().Where(c => c != CategoriaProcessamento.Invalido))
        {
            Assert.True(catalogo.TryObter(categoria, out _), $"Sem processador para {categoria}");
        }

        Assert.False(catalogo.TryObter(CategoriaProcessamento.Invalido, out _));
    }

    [Fact]
    public void Construir_DuasVezes_ProduzOrdemIdentica()
    {
        var a = CatalogoProcessadores.Construir(AssemblyConhecido, true).Descritores;
        var b = CatalogoProcessadores.Construir(AssemblyConhecido, true).Descritores;

        Assert.Equal(a, b);
        Assert.Equal(
            a.OrderBy(d => d.Categoria).ThenBy(d => d.Versao).ToArray(),
            a.ToArray());
    }

    [Fact]
    public void Construir_MaisDeUmaVersao_SelecionaAMaisRecente()
    {
        var catalogo = CatalogoProcessadores.Construir(AssemblyConhecido, true);

        Assert.True(catalogo.TryObter(CategoriaProcessamento.Expresso, out var vigente));
        Assert.Equal(2, vigente.Versao);
        Assert.Equal(typeof(ProcessadorExpressoV2), vigente.Tipo);
        Assert.Contains(catalogo.Descritores, d => d.Tipo == typeof(ProcessadorExpressoV1) && !catalogo.EhVigente(d));
    }

    [Fact]
    public void Construir_ProcessadoresDoAssembly_ImplementamAInterface()
    {
        var catalogo = CatalogoProcessadores.Construir(AssemblyConhecido, true);

        Assert.All(catalogo.Descritores, d => Assert.True(typeof(IProcessadorPedido).IsAssignableFrom(d.Tipo)));
    }

    [Fact]
    public void Construir_NaoVarreOutrosAssemblies_IgnoraTiposDoAssemblyDeTestes()
    {
        var catalogo = CatalogoProcessadores.Construir(AssemblyConhecido, true);

        Assert.DoesNotContain(catalogo.Descritores, d => d.Tipo.Assembly == typeof(CatalogoProcessadoresTests).Assembly);
    }

    [Fact]
    public void Construir_Duplicidade_FalhaListandoOsTipos()
    {
        var ex = Assert.Throws<CatalogoProcessadoresException>(() =>
            CatalogoProcessadores.Construir(new[] { typeof(Falsos.PadraoA), typeof(Falsos.PadraoB) }));

        Assert.Contains("Duplicidade", ex.Message);
        Assert.Contains(nameof(Falsos.PadraoA), ex.Message);
        Assert.Contains(nameof(Falsos.PadraoB), ex.Message);
    }

    [Fact]
    public void Construir_MesmaCategoriaComVersoesDiferentes_NaoEhDuplicidade()
    {
        var catalogo = CatalogoProcessadores.Construir(new[] { typeof(Falsos.PadraoA), typeof(Falsos.PadraoV2) });

        Assert.True(catalogo.TryObter(CategoriaProcessamento.Padrao, out var vigente));
        Assert.Equal(typeof(Falsos.PadraoV2), vigente.Tipo);
    }

    [Fact]
    public void Construir_ImplementaInterfaceSemAtributo_Falha()
    {
        var ex = Assert.Throws<CatalogoProcessadoresException>(() =>
            CatalogoProcessadores.Construir(new[] { typeof(Falsos.SemAtributo) }));

        Assert.Contains("não declara [ProcessadorPedido]", ex.Message);
    }

    [Fact]
    public void Construir_AtributoSemInterface_Falha()
    {
        var ex = Assert.Throws<CatalogoProcessadoresException>(() =>
            CatalogoProcessadores.Construir(new[] { typeof(Falsos.SemInterface) }));

        Assert.Contains("não implementa IProcessadorPedido", ex.Message);
    }

    [Fact]
    public void Construir_CategoriaInvalida_Falha()
    {
        var ex = Assert.Throws<CatalogoProcessadoresException>(() =>
            CatalogoProcessadores.Construir(new[] { typeof(Falsos.CategoriaInvalida) }));

        Assert.Contains("categoria", ex.Message);
    }

    [Fact]
    public void Construir_VersaoNaoPositiva_Falha()
    {
        var ex = Assert.Throws<CatalogoProcessadoresException>(() =>
            CatalogoProcessadores.Construir(new[] { typeof(Falsos.VersaoZero) }));

        Assert.Contains("versão", ex.Message);
    }

    [Fact]
    public void Construir_DoisConstrutoresPublicos_Falha()
    {
        var ex = Assert.Throws<CatalogoProcessadoresException>(() =>
            CatalogoProcessadores.Construir(new[] { typeof(Falsos.DoisConstrutores) }));

        Assert.Contains("construtor", ex.Message);
    }

    [Fact]
    public void Construir_AcumulaTodosOsProblemasEmUmaExcecao()
    {
        var ex = Assert.Throws<CatalogoProcessadoresException>(() =>
            CatalogoProcessadores.Construir(new[]
            {
                typeof(Falsos.SemAtributo),
                typeof(Falsos.VersaoZero),
                typeof(Falsos.SemInterface)
            }));

        Assert.Equal(3, ex.Problemas.Count);
    }

    [Fact]
    public void Construir_TiposAbstratosEClassesNaoRelacionadas_SaoIgnorados()
    {
        var catalogo = CatalogoProcessadores.Construir(new[]
        {
            typeof(Falsos.Abstrato),
            typeof(string),
            typeof(Falsos.PadraoA)
        });

        Assert.Single(catalogo.Descritores);
    }

    [Fact]
    public void Construir_ExigirCoberturaSemTodasAsCategorias_Falha()
    {
        var ex = Assert.Throws<CatalogoProcessadoresException>(() =>
            CatalogoProcessadores.Construir(new[] { typeof(Falsos.PadraoA) }, exigirCobertura: true));

        Assert.Contains("Premium", ex.Message);
    }

    [Fact]
    public void ValidarResolucao_DependenciaNaoRegistrada_FalhaComMensagemLegivel()
    {
        var catalogo = CatalogoProcessadores.Construir(new[] { typeof(Falsos.ComDependencia) });
        var provider = new ServiceCollection().AddSingleton<Falsos.ComDependencia>().BuildServiceProvider();

        var ex = Assert.Throws<CatalogoProcessadoresException>(() => catalogo.ValidarResolucao(provider));

        Assert.Contains(nameof(Falsos.ComDependencia), ex.Message);
    }

    [Fact]
    public void ValidarResolucao_TipoNaoRegistrado_Falha()
    {
        var catalogo = CatalogoProcessadores.Construir(new[] { typeof(Falsos.PadraoA) });
        var provider = new ServiceCollection().BuildServiceProvider();

        Assert.Throws<CatalogoProcessadoresException>(() => catalogo.ValidarResolucao(provider));
    }

    [Fact]
    public void ValidarResolucao_Registrado_NaoLanca()
    {
        var catalogo = CatalogoProcessadores.Construir(new[] { typeof(Falsos.PadraoA) });
        var provider = new ServiceCollection().AddSingleton<Falsos.PadraoA>().BuildServiceProvider();

        catalogo.ValidarResolucao(provider);
    }

//incosistentes
    public static class Falsos
    {
        private static ResultadoProcessamento Ok => new("fake", 0m);

        [ProcessadorPedido(CategoriaProcessamento.Padrao, 1)]
        public sealed class PadraoA : IProcessadorPedido
        {
            public ResultadoProcessamento Processar(Pedido pedido) => Ok;
        }

        [ProcessadorPedido(CategoriaProcessamento.Padrao, 1)]
        public sealed class PadraoB : IProcessadorPedido
        {
            public ResultadoProcessamento Processar(Pedido pedido) => Ok;
        }

        [ProcessadorPedido(CategoriaProcessamento.Padrao, 2)]
        public sealed class PadraoV2 : IProcessadorPedido
        {
            public ResultadoProcessamento Processar(Pedido pedido) => Ok;
        }

        public sealed class SemAtributo : IProcessadorPedido
        {
            public ResultadoProcessamento Processar(Pedido pedido) => Ok;
        }

        [ProcessadorPedido(CategoriaProcessamento.Padrao, 1)]
        public sealed class SemInterface
        {
        }

        [ProcessadorPedido(CategoriaProcessamento.Invalido, 1)]
        public sealed class CategoriaInvalida : IProcessadorPedido
        {
            public ResultadoProcessamento Processar(Pedido pedido) => Ok;
        }

        [ProcessadorPedido(CategoriaProcessamento.Padrao, 0)]
        public sealed class VersaoZero : IProcessadorPedido
        {
            public ResultadoProcessamento Processar(Pedido pedido) => Ok;
        }

        [ProcessadorPedido(CategoriaProcessamento.Padrao, 1)]
        public sealed class DoisConstrutores : IProcessadorPedido
        {
            public DoisConstrutores()
            {
            }

            public DoisConstrutores(int valor)
            {
            }

            public ResultadoProcessamento Processar(Pedido pedido) => Ok;
        }

        [ProcessadorPedido(CategoriaProcessamento.Padrao, 1)]
        public abstract class Abstrato : IProcessadorPedido
        {
            public abstract ResultadoProcessamento Processar(Pedido pedido);
        }

        [ProcessadorPedido(CategoriaProcessamento.Padrao, 1)]
        public sealed class ComDependencia : IProcessadorPedido
        {
            public ComDependencia(IFormatProvider dependenciaNaoRegistrada)
            {
            }

            public ResultadoProcessamento Processar(Pedido pedido) => Ok;
        }
    }
}

