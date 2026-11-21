using HubPedidos.Domain.Pedidos;

namespace HubPedidos.Tests;

public class PedidoIdTests
{
    [Fact]
    public void Criar_GuidVazio_Falha()
    {
        var resultado = PedidoId.Criar(Guid.Empty);

        Assert.False(resultado.Sucesso);
        Assert.Single(resultado.Erros);
    }

    [Fact]
    public void Criar_GuidValido_Sucesso()
    {
        var guid = Guid.NewGuid();

        var resultado = PedidoId.Criar(guid);

        Assert.True(resultado.TryObter(out var id));
        Assert.Equal(guid, id.Valor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void TryParse_Invalido_RetornaFalse(string? texto)
    {
        Assert.False(PedidoId.TryParse(texto, null, out var id));
        Assert.Null(id);
    }

    [Fact]
    public void Parse_Invalido_LancaFormatException()
    {
        Assert.Throws<FormatException>(() => PedidoId.Parse("abc", null));
    }

    [Fact]
    public void Parse_ToString_IdaEVolta_PreservaIgualdade()
    {
        var original = PedidoId.Novo();

        var copia = PedidoId.Parse(original.ToString(), null);

        Assert.Equal(original, copia);
        Assert.Equal(original.GetHashCode(), copia.GetHashCode());
    }

    [Fact]
    public void IdsDistintos_NaoSaoIguais()
    {
        Assert.NotEqual(PedidoId.Novo(), PedidoId.Novo());
    }
}
