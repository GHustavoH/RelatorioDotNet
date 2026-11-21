using System.Text.Json;
using HubPedidos.Application.Contratos;
using HubPedidos.Application.Pedidos;
using Npgsql;
using NpgsqlTypes;

namespace HubPedidos.Api.Infraestrutura;

//Persistência em PostgreSQL. A connection string vem de configuração
public sealed class PedidoRepositorioPostgres : IPedidoRepositorio
{
    private const string SqlCriarTabela = """
        CREATE TABLE IF NOT EXISTS pedidos (
            id uuid PRIMARY KEY,
            categoria text NOT NULL,
            processador text NOT NULL,
            versao_processador integer NOT NULL,
            valor_total numeric(18,2) NOT NULL,
            valor_final numeric(18,2) NOT NULL,
            criado_em timestamptz NOT NULL DEFAULT now(),
            payload jsonb NOT NULL
        );
        """;

    private const string SqlInserir = """
        INSERT INTO pedidos (id, categoria, processador, versao_processador, valor_total, valor_final, payload)
        VALUES (@id, @categoria, @processador, @versao, @valor_total, @valor_final, @payload)
        ON CONFLICT (id) DO NOTHING;
        """;

    private readonly NpgsqlDataSource _dataSource;

    public PedidoRepositorioPostgres(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task InicializarAsync(CancellationToken cancellationToken)
    {
        await using var comando = _dataSource.CreateCommand(SqlCriarTabela);
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> SalvarAsync(PedidoResponse pedido, CancellationToken cancellationToken)
    {
        await using var comando = _dataSource.CreateCommand(SqlInserir);
        comando.Parameters.AddWithValue("id", Guid.Parse(pedido.Id));
        comando.Parameters.AddWithValue("categoria", pedido.Categoria);
        comando.Parameters.AddWithValue("processador", pedido.Processador);
        comando.Parameters.AddWithValue("versao", pedido.VersaoProcessador);
        comando.Parameters.AddWithValue("valor_total", pedido.ValorTotal);
        comando.Parameters.AddWithValue("valor_final", pedido.ValorFinal);
        comando.Parameters.Add(new NpgsqlParameter("payload", NpgsqlDbType.Jsonb)
        {
            Value = JsonSerializer.Serialize(pedido)
        });

        var linhas = await comando.ExecuteNonQueryAsync(cancellationToken);
        return linhas == 1;
    }
}
