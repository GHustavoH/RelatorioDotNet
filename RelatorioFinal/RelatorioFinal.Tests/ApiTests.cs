using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RelatorioFinal.Contratos;

namespace RelatorioFinal.Tests;

public class ApiTests(FabricaApi fabrica) : IClassFixture<FabricaApi>
{
    private async Task<HttpClient> ClienteAsync(string usuario, string senha)
    {
        var cliente = fabrica.CreateClient();
        var resposta = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(usuario, senha));
        resposta.EnsureSuccessStatusCode();

        var login = await resposta.Content.ReadFromJsonAsync<LoginResponse>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        return cliente;
    }

    private static async Task<PedidoResponse> CriarPedidoAsync(HttpClient cliente)
    {
        var req = new CriarPedidoRequest("qualquer", "BRASIL", "Normal", [new ItemRequest("Caneta", 2, 10m)]);
        var resposta = await cliente.PostAsJsonAsync("/api/pedidos", req);
        Assert.Equal(HttpStatusCode.Accepted, resposta.StatusCode);

        var pedido = await resposta.Content.ReadFromJsonAsync<PedidoResponse>();
        return pedido!;
    }

    [Fact]
    public async Task Sem_token_retorna_401()
    {
        var resposta = await fabrica.CreateClient().GetAsync("/api/pedidos");
        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Login_invalido_retorna_401()
    {
        var resposta = await fabrica.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("ana", "errada"));
        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Pedido_invalido_retorna_400()
    {
        var cliente = await ClienteAsync("ana", "123");
        var req = new CriarPedidoRequest("ana", "BRASIL", "Normal", [new ItemRequest("Caneta", -1, 10m)]);

        var resposta = await cliente.PostAsJsonAsync("/api/pedidos", req);
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Cria_e_consulta_nas_versoes_1_e_2()
    {
        var cliente = await ClienteAsync("ana", "123");
        var pedido = await CriarPedidoAsync(cliente);

        // o cliente v1 continua funcionando
        var v1 = await cliente.GetAsync($"/api/v1/pedidos/{pedido.Id}");
        Assert.Equal(HttpStatusCode.OK, v1.StatusCode);
        Assert.True(v1.Headers.Contains("Deprecation"));
        var corpoV1 = await v1.Content.ReadFromJsonAsync<PedidoV1Response>();
        Assert.Equal("ana", corpoV1!.Cliente);

        var v2 = await cliente.GetFromJsonAsync<PedidoV2Response>($"/api/v2/pedidos/{pedido.Id}");
        Assert.Equal("ana", v2!.Cliente.Nome);
        Assert.Equal(20m, v2.Valor.Quantia);

        var semVersao = await cliente.GetAsync($"/api/pedidos/{pedido.Id}");
        Assert.Equal(HttpStatusCode.OK, semVersao.StatusCode);

        var naoSuportada = await cliente.GetAsync($"/api/v3/pedidos/{pedido.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, naoSuportada.StatusCode);
    }

    [Fact]
    public async Task Etag_devolve_304_quando_nao_mudou_e_200_quando_obsoleto()
    {
        var cliente = await ClienteAsync("ana", "123");
        var pedido = await CriarPedidoAsync(cliente);

        // espera o worker terminar para a representacao ficar estavel
        await EsperarProcessadoAsync(cliente, pedido.Id);

        var primeira = await cliente.GetAsync($"/api/v1/pedidos/{pedido.Id}");
        var etag = primeira.Headers.ETag;
        Assert.NotNull(etag);

        var condicional = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/pedidos/{pedido.Id}");
        condicional.Headers.IfNoneMatch.Add(etag);
        var segunda = await cliente.SendAsync(condicional);
        Assert.Equal(HttpStatusCode.NotModified, segunda.StatusCode);

        var obsoleto = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/pedidos/{pedido.Id}");
        obsoleto.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"antigo\""));
        var terceira = await cliente.SendAsync(obsoleto);
        Assert.Equal(HttpStatusCode.OK, terceira.StatusCode);
    }

    [Fact]
    public async Task Pedido_e_processado_pela_fila()
    {
        var cliente = await ClienteAsync("ana", "123");
        var pedido = await CriarPedidoAsync(cliente);

        await EsperarProcessadoAsync(cliente, pedido.Id);
    }

    [Fact]
    public async Task Outro_cliente_nao_enxerga_o_pedido()
    {
        var ana = await ClienteAsync("ana", "123");
        var bruno = await ClienteAsync("bruno", "123");
        var pedido = await CriarPedidoAsync(ana);

        var resposta = await bruno.GetAsync($"/api/pedidos/{pedido.Id}");
        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Fact]
    public async Task Importacao_exige_admin()
    {
        var ana = await ClienteAsync("ana", "123");
        var resposta = await ana.PostAsync("/api/pedidos/importar", new StringContent("x"));
        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);

        var admin = await ClienteAsync("admin", "admin");
        var lote = new StringContent("ana;BRASIL;Normal;Caneta;3;4.50\nbruno;EXTERIOR;Alta;Caderno;10;12.90\n");
        var importado = await admin.PostAsync("/api/pedidos/importar", lote);
        var resumo = await importado.Content.ReadFromJsonAsync<ResumoImportacao>();

        Assert.Equal(HttpStatusCode.OK, importado.StatusCode);
        Assert.Equal(2, resumo!.Aceitos);
    }

    [Fact]
    public async Task Estatisticas_sao_publicas()
    {
        var resposta = await fabrica.CreateClient().GetAsync("/api/estatisticas");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    // consulta em intervalos curtos ate processar, sem atraso longo fixo
    private static async Task EsperarProcessadoAsync(HttpClient cliente, Guid id)
    {
        for (int i = 0; i < 100; i++)
        {
            var atual = await cliente.GetFromJsonAsync<PedidoV1Response>($"/api/v1/pedidos/{id}");
            if (atual!.Status == "Processado")
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.Fail("O pedido nao foi processado a tempo");
    }
}
