using Microsoft.Extensions.Options;
using Relatorio005.Hub;

namespace Relatorio005.Tests;

public class FilaMensagensTests
{
    [Fact]
    public void Fila_cheia_rejeita_novas_mensagens()
    {
        using var metricas = new HubMetricas();
        var fila = new FilaMensagens(Options.Create(new HubOptions { CapacidadeFila = 2 }), metricas);

        PedidoMensagem Novo() => new(Guid.NewGuid(), "ti", "conteudo", 1, 0);

        Assert.True(fila.TentarEnfileirar(Novo()));
        Assert.True(fila.TentarEnfileirar(Novo()));
        Assert.False(fila.TentarEnfileirar(Novo()));
        Assert.Equal(2, fila.Profundidade);
    }

    [Fact]
    public void Store_respeita_capacidade()
    {
        var store = new MensagemStore(2);

        Mensagem Nova(long sequencia) => new(Guid.NewGuid(), sequencia, "ti", "x", 0, DateTimeOffset.UtcNow);

        Assert.True(store.TentarAdicionar(Nova(1)));
        Assert.True(store.TentarAdicionar(Nova(2)));
        Assert.False(store.TentarAdicionar(Nova(3)));
        Assert.Equal(2, store.Total);
    }
}
