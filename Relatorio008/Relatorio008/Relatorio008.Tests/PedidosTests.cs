using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Relatorio008.Tests;

public class PedidosTests : TestesBase
{
    private static object NovoPedido() => new { cliente = "Maria", valor = 50.5m };

    [Fact]
    public async Task Criar_PedidoValido_Retorna201ESalvaNoBanco()
    {
        var resposta = await Cliente.PostAsJsonAsync("/api/v1/pedidos", NovoPedido());

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var criado = await resposta.Content.ReadFromJsonAsync<PedidoV1>();
        Assert.NotNull(criado);
        Assert.Equal("Maria", criado.Cliente);
        Assert.NotNull(resposta.Headers.Location);

        // confere direto no banco
        Pedido? salvo = null;
        Fabrica.ExecutarNoBanco(db => salvo = db.Pedidos.Find(criado.Id));
        Assert.NotNull(salvo);
        Assert.Equal(50.5m, salvo.Valor);
    }

    [Fact]
    public async Task Consultar_PedidoCriado_RetornaOsMesmosDados()
    {
        var post = await Cliente.PostAsJsonAsync("/api/v1/pedidos", NovoPedido());
        var criado = await post.Content.ReadFromJsonAsync<PedidoV1>();

        var get = await Cliente.GetAsync($"/api/v1/pedidos/{criado!.Id}");

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var lido = await get.Content.ReadFromJsonAsync<PedidoV1>();
        Assert.Equal(criado, lido);
    }

    [Fact]
    public async Task Consultar_PedidoInexistente_Retorna404()
    {
        var resposta = await Cliente.GetAsync("/api/v1/pedidos/999");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Fact]
    public async Task Criar_ValorInvalido_Retorna400ENaoPublicaNaFila()
    {
        var resposta = await Cliente.PostAsJsonAsync("/api/v1/pedidos", new { cliente = "Maria", valor = -5 });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.False(Fabrica.Fila.TemMensagem(), "pedido invalido nao deveria gerar mensagem na fila");
    }

    [Fact]
    public async Task Criar_ClienteVazio_Retorna400()
    {
        var resposta = await Cliente.PostAsJsonAsync("/api/v1/pedidos", new { cliente = "", valor = 10 });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Criar_SemChaveDeApi_Retorna401()
    {
        using var semChave = Fabrica.CriarCliente(comChave: false);

        var resposta = await semChave.PostAsJsonAsync("/api/v1/pedidos", NovoPedido());

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Consultar_ComChaveErrada_Retorna401()
    {
        using var cliente = Fabrica.CriarCliente(comChave: false);
        cliente.DefaultRequestHeaders.Add("X-Api-Key", "chave-errada");

        var resposta = await cliente.GetAsync("/api/v2/pedidos/1");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task V1eV2_RetornamFormatosDiferentes()
    {
        var v1 = await Cliente.GetStringAsync("/api/v1/pedidos/1");
        var v2 = await Cliente.GetStringAsync("/api/v2/pedidos/1");

        using var jsonV1 = JsonDocument.Parse(v1);
        using var jsonV2 = JsonDocument.Parse(v2);

        Assert.False(jsonV1.RootElement.TryGetProperty("status", out _), "v1 nao deve ter status");
        Assert.False(jsonV1.RootElement.TryGetProperty("criadoEm", out _), "v1 nao deve ter criadoEm");
        Assert.Equal("Novo", jsonV2.RootElement.GetProperty("status").GetString());
        Assert.True(jsonV2.RootElement.TryGetProperty("criadoEm", out _), "v2 deve ter criadoEm");
    }

    [Fact]
    public async Task V2_ComIfNoneMatchIgual_Retorna304()
    {
        var primeira = await Cliente.GetAsync("/api/v2/pedidos/1");
        Assert.Equal(HttpStatusCode.OK, primeira.StatusCode);
        var etag = primeira.Headers.ETag;
        Assert.NotNull(etag);

        var pedido = new HttpRequestMessage(HttpMethod.Get, "/api/v2/pedidos/1");
        pedido.Headers.IfNoneMatch.Add(etag);
        var segunda = await Cliente.SendAsync(pedido);

        Assert.Equal(HttpStatusCode.NotModified, segunda.StatusCode);
    }

    [Fact]
    public async Task V2_DepoisDeAlterarPedido_EtagMudaERetorna200()
    {
        var primeira = await Cliente.GetAsync("/api/v2/pedidos/1");
        var etagAntigo = primeira.Headers.ETag;
        Assert.NotNull(etagAntigo);

        Fabrica.ExecutarNoBanco(db =>
        {
            var p = db.Pedidos.Find(1)!;
            p.Status = "Pago";
            db.SaveChanges();
        });

        var pedido = new HttpRequestMessage(HttpMethod.Get, "/api/v2/pedidos/1");
        pedido.Headers.IfNoneMatch.Add(etagAntigo);
        var segunda = await Cliente.SendAsync(pedido);

        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        Assert.NotEqual(etagAntigo, segunda.Headers.ETag);
    }

    [Fact]
    public async Task Criar_PedidoValido_PublicaMensagemNaFila()
    {
        var resposta = await Cliente.PostAsJsonAsync("/api/v1/pedidos", NovoPedido());
        var criado = await resposta.Content.ReadFromJsonAsync<PedidoV1>();

        var mensagem = await Fabrica.Fila.AguardarMensagemAsync();

        Assert.Equal(criado!.Id, mensagem.Id);
        Assert.Equal("Maria", mensagem.Cliente);
        Assert.Equal(50.5m, mensagem.Valor);
    }
}

public class AmbienteTests : TestesBase
{
    [Fact]
    public void Banco_NaoEhODeDesenvolvimento()
    {
        string? conexao = null;
        Fabrica.ExecutarNoBanco(db => conexao = db.Database.GetConnectionString());

        Assert.NotNull(conexao);
        Assert.DoesNotContain("dev.db", conexao);
    }

    [Fact]
    public async Task Banco_EhIsoladoEntreTestes()
    {
        // se outro teste vazasse dados para ca, a contagem seria maior que 2
        await Cliente.PostAsJsonAsync("/api/v1/pedidos", new { cliente = "Ana", valor = 5 });

        var total = 0;
        Fabrica.ExecutarNoBanco(db => total = db.Pedidos.Count());

        Assert.Equal(2, total);
    }

    [Fact]
    public void FalhaProposital_SoQuandoPedido()
    {
        // usado pelo script de revisao para provar que um teste quebrado bloqueia
        if (Environment.GetEnvironmentVariable("PROVOCAR_FALHA_TESTE") == "1")
        {
            Assert.Fail("Falha proposital para validar o bloqueio da revisao automatizada");
        }
    }
}
