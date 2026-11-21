using relatorio007.Models;
using relatorio007.Services;
using Xunit;

namespace relatorio007.Tests;

public class EtagTests
{
    [Fact]
    public void Etag_E_Estavel_Para_A_Mesma_Representacao()
    {
        var a = new PedidoDto(1, "Pendente", 100m, 1);
        var b = new PedidoDto(1, "Pendente", 100m, 1);

        Assert.Equal(Etag.Gerar(a), Etag.Gerar(b));
    }

    [Fact]
    public void Etag_Muda_Quando_A_Representacao_Muda()
    {
        var a = new PedidoDto(1, "Pendente", 100m, 1);
        var b = new PedidoDto(1, "Pago", 100m, 2);

        Assert.NotEqual(Etag.Gerar(a), Etag.Gerar(b));
    }

    [Fact]
    public void Confere_Aceita_Valor_Igual_Fraco_Lista_E_Asterisco()
    {
        var etag = Etag.Gerar(new PedidoDto(1, "Pendente", 100m, 1));

        Assert.True(Etag.Confere(etag, etag));
        Assert.True(Etag.Confere("W/" + etag, etag));
        Assert.True(Etag.Confere("\"outro\", " + etag, etag));
        Assert.True(Etag.Confere("*", etag));
    }

    [Fact]
    public void Confere_Rejeita_Valor_Invalido_Obsoleto_Ou_Vazio()
    {
        var etag = Etag.Gerar(new PedidoDto(1, "Pendente", 100m, 1));
        var obsoleto = Etag.Gerar(new PedidoDto(1, "Pendente", 100m, 0));

        Assert.False(Etag.Confere("lixo", etag));
        Assert.False(Etag.Confere(obsoleto, etag));
        Assert.False(Etag.Confere("", etag));
        Assert.False(Etag.Confere(null, etag));
    }
}
