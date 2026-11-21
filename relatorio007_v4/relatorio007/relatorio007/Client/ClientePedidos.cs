using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using relatorio007.Models;

namespace relatorio007.Client;

public enum EstadoConexao
{
    Conectado,
    Reconectando,
    Desconectado
}

public class ClientePedidos
{
    private readonly HubConnection _hub;
    private readonly HttpClient _http;
    private readonly HashSet<int> _pedidos = new();
    private readonly Dictionary<int, PedidoDto> _estado = new();
    private readonly Dictionary<int, string> _etags = new();

    public EstadoConexao Estado { get; private set; } = EstadoConexao.Desconectado;

    // acoes que dependem da conexao ficam desabilitadas fora do estado conectado
    public bool PodeAgir => Estado == EstadoConexao.Conectado;

    public event Action<EstadoConexao>? EstadoMudou;
    public event Action<PedidoDto>? PedidoRecebido;

    public ClientePedidos(HubConnection hub, HttpClient http)
    {
        _hub = hub;
        _http = http;

        _hub.On<PedidoAtualizado>("PedidoAtualizado", async evento =>
        {
            // o evento so avisa; o dado vem por HTTP
            if (_estado.TryGetValue(evento.PedidoId, out var atual) && atual.Versao >= evento.Versao)
                return;

            await Reconciliar(evento.PedidoId);
        });

        _hub.Reconnecting += erro =>
        {
            MudarEstado(EstadoConexao.Reconectando);
            return Task.CompletedTask;
        };

        _hub.Reconnected += async id =>
        {
            // grupos se perdem na reconexao: reentra (o servidor revalida) e depois reconcilia por HTTP
            foreach (var pedidoId in _pedidos.ToList())
            {
                try
                {
                    await _hub.InvokeAsync("Entrar", pedidoId);
                }
                catch (HubException)
                {
                    _pedidos.Remove(pedidoId);
                }
            }

            foreach (var pedidoId in _pedidos.ToList())
                await Reconciliar(pedidoId);

            MudarEstado(EstadoConexao.Conectado);
        };

        _hub.Closed += erro =>
        {
            MudarEstado(EstadoConexao.Desconectado);
            return Task.CompletedTask;
        };
    }

    public async Task Iniciar()
    {
        await _hub.StartAsync();
        MudarEstado(EstadoConexao.Conectado);
    }

    public async Task Acompanhar(int pedidoId)
    {
        await _hub.InvokeAsync("Entrar", pedidoId);
        _pedidos.Add(pedidoId);
        await Reconciliar(pedidoId);
    }

    public async Task Parar()
    {
        await _hub.StopAsync();
    }

    public PedidoDto? ObterLocal(int pedidoId)
    {
        return _estado.TryGetValue(pedidoId, out var p) ? p : null;
    }

    public async Task Reconciliar(int pedidoId)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, $"api/pedidos/{pedidoId}");
        if (_etags.TryGetValue(pedidoId, out var etag))
            req.Headers.TryAddWithoutValidation("If-None-Match", etag);

        var resp = await _http.SendAsync(req);

        if (resp.StatusCode == HttpStatusCode.NotModified)
            return;

        if (resp.StatusCode == HttpStatusCode.Forbidden || resp.StatusCode == HttpStatusCode.NotFound)
        {
            _pedidos.Remove(pedidoId);
            _estado.Remove(pedidoId);
            _etags.Remove(pedidoId);
            return;
        }

        resp.EnsureSuccessStatusCode();

        var dto = await resp.Content.ReadFromJsonAsync<PedidoDto>();
        if (dto == null)
            return;

        if (_estado.TryGetValue(pedidoId, out var atual) && atual.Versao > dto.Versao)
            return;

        _estado[pedidoId] = dto;
        if (resp.Headers.ETag != null)
            _etags[pedidoId] = resp.Headers.ETag.ToString();

        PedidoRecebido?.Invoke(dto);
    }

    private void MudarEstado(EstadoConexao novo)
    {
        Estado = novo;
        EstadoMudou?.Invoke(novo);
    }
}
