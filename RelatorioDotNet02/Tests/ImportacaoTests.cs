using System.Buffers;
using System.Text;
using HubPedidos.Application.Importacao;
using HubPedidos.Domain.Pedidos;

namespace HubPedidos.Tests;

// Roteiro 2 - Etapas 1 e 2: testes funcionais do baseline e de equivalência com a versão otimizada.
public class ImportacaoTests
{
    private const string G1 = "00000001-0000-0000-0000-000000000001";
    private const string G2 = "00000002-0000-0000-0000-000000000002";

    // ---------- Etapa 1: comportamento do baseline ----------

    [Fact]
    public void Baseline_LinhaValida_CriaPedidoComTotais()
    {
        var r = ImportadorPedidosBaseline.Importar($"{G1};Sul;Alta;ABC:2:10.50|XYZ:1:5.00");

        Assert.True(r.Sucesso);
        var pedido = Assert.Single(r.Pedidos);
        Assert.Equal(Regiao.Sul, pedido.Regiao);
        Assert.Equal(Prioridade.Alta, pedido.Prioridade);
        Assert.Equal(3, pedido.QuantidadeItens);
        Assert.Equal(26.00m, pedido.ValorTotal);
    }

    [Fact]
    public void Baseline_IgnoraLinhasEmBranco_EAceitaCrLf()
    {
        var conteudo = $"\r\n{G1};Sul;Alta;ABC:1:1.00\r\n\r\n{G2};Norte;Baixa;DEF:3:2.00\r\n";

        var r = ImportadorPedidosBaseline.Importar(conteudo);

        Assert.True(r.Sucesso);
        Assert.Equal(2, r.Pedidos.Count);
    }

    [Fact]
    public void Baseline_LinhaInvalida_NaoImpedeAsDemais_EInformaNumeroDaLinha()
    {
        var conteudo = $"{G1};Sul;Alta;ABC:1:1.00\nlixo\n{G2};Norte;Baixa;DEF:3:2.00";

        var r = ImportadorPedidosBaseline.Importar(conteudo);

        Assert.Equal(2, r.Pedidos.Count);
        var erro = Assert.Single(r.Erros);
        Assert.StartsWith("linha 2:", erro);
    }

    // ---------- Etapa 2: otimizado == baseline ----------

    [Theory]
    [InlineData("")]
    [InlineData("\n\n  \n")]
    [InlineData(G1 + ";Sul;Alta;ABC:2:10.50|XYZ:1:5.00")]
    [InlineData(G1 + ";sul;urgente;ABC:2:10.50\r\n" + G2 + ";Exterior;Baixa;Z:1:1")]
    [InlineData("abc;Sul;Alta;ABC:1:1.00")]                       // id inválido
    [InlineData(G1 + ";Marte;Alta;ABC:1:1.00")]                   // região inválida
    [InlineData(G1 + ";Sul;2;ABC:1:1.00")]                        // prioridade numérica
    [InlineData(G1 + ";Sul;Alta")]                                // campos de menos
    [InlineData(G1 + ";Sul;Alta;ABC:1:1.00;extra")]               // campos demais
    [InlineData(G1 + ";Sul;Alta;ABC:abc:1.00")]                   // quantidade não numérica
    [InlineData(G1 + ";Sul;Alta;ABC:1:1.00:9")]                   // item com 4 partes
    [InlineData(G1 + ";Sul;Alta;")]                               // sem itens
    [InlineData(G1 + ";Sul;Alta;ABC:0:1.00")]                     // quantidade fora da faixa
    [InlineData(G1 + ";Sul;Alta;ABC:1:1.005")]                    // 3 casas decimais
    [InlineData(G1 + ";Sul;Alta;ABC:1:1.00|abc:2:3.00")]          // SKU repetido (case-insensitive)
    [InlineData(G1 + ";Sul;Alta; ABC : 1 : 1.00 ")]               // espaços
    [InlineData("x;Marte;9;:0:-1|ABC")]                           // vários erros juntos
    public void Otimizado_ProduzMesmoResultadoQueBaseline(string conteudo)
    {
        var baseline = ImportadorPedidosBaseline.Importar(conteudo);
        var otimizado = ImportadorPedidosOtimizado.Importar(conteudo);

        Assert.Equal(baseline.Pedidos, otimizado.Pedidos);
        Assert.Equal(baseline.Erros, otimizado.Erros);
    }

    [Fact]
    public void Otimizado_LoteGrandeComErros_IgualAoBaseline()
    {
        var conteudo = GerarLote(500, comErros: true);

        var baseline = ImportadorPedidosBaseline.Importar(conteudo);
        var otimizado = ImportadorPedidosOtimizado.Importar(conteudo);

        Assert.NotEmpty(baseline.Erros);
        Assert.Equal(baseline.Pedidos, otimizado.Pedidos);
        Assert.Equal(baseline.Erros, otimizado.Erros);
    }

    // ---------- Etapas 3 e 4: assíncrono + pooling ----------

    [Fact]
    public async Task Assincrono_BufferPequeno_LinhasCruzandoBlocos_IgualAoBaseline()
    {
        var conteudo = GerarLote(300, comErros: true);
        var esperado = ImportadorPedidosBaseline.Importar(conteudo);

        // buffer de 256 chars força muitas linhas cortadas entre leituras
        var importador = new ImportadorAssincronoOtimizado(tamanhoBuffer: 256);
        var r = await importador.ImportarAsync(ComoStream(conteudo), long.MaxValue, null, CancellationToken.None);

        Assert.Equal(esperado.Pedidos, r.Pedidos);
        Assert.Equal(esperado.Erros, r.Erros);
    }

    [Fact]
    public async Task Assincrono_BaselineEOtimizado_MesmoResultadoELog()
    {
        var conteudo = GerarLote(200, comErros: true) + "\n" + $"{G1};Sul;Alta;ULT:1:1.00"; // sem \n final

        var logBase = new StringWriter();
        var logOtim = new StringWriter();

        var b = await ImportadorAssincronoBaseline.ImportarAsync(ComoStream(conteudo), long.MaxValue, logBase, CancellationToken.None);
        var o = await new ImportadorAssincronoOtimizado(tamanhoBuffer: 512)
            .ImportarAsync(ComoStream(conteudo), long.MaxValue, logOtim, CancellationToken.None);

        Assert.Equal(b.Pedidos, o.Pedidos);
        Assert.Equal(b.Erros, o.Erros);
        Assert.Equal(logBase.ToString(), logOtim.ToString());
    }

    [Fact]
    public async Task Assincrono_FlushPorItem_VersusAgrupado()
    {
        var conteudo = GerarLote(50, comErros: false);
        var logBase = new ContaFlush();
        var logOtim = new ContaFlush();

        await ImportadorAssincronoBaseline.ImportarAsync(ComoStream(conteudo), long.MaxValue, logBase, CancellationToken.None);
        await new ImportadorAssincronoOtimizado().ImportarAsync(ComoStream(conteudo), long.MaxValue, logOtim, CancellationToken.None);

        Assert.Equal(50, logBase.Flushes);
        Assert.Equal(1, logOtim.Flushes);
    }

    [Fact]
    public async Task Assincrono_LimiteTotal_Excedido_LancaInvalidDataException_EDevolveBuffer()
    {
        var pool = new PoolContador();
        var importador = new ImportadorAssincronoOtimizado(pool, tamanhoBuffer: 256);
        var conteudo = GerarLote(100, comErros: false);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            importador.ImportarAsync(ComoStream(conteudo), limiteCaracteres: 1_000, null, CancellationToken.None));

        Assert.Equal(1, pool.Alugados);
        Assert.Equal(1, pool.Devolvidos);
    }

    [Fact]
    public async Task Assincrono_LinhaMaiorQueBuffer_LancaInvalidDataException_EDevolveBuffer()
    {
        var pool = new PoolContador();
        var importador = new ImportadorAssincronoOtimizado(pool, tamanhoBuffer: 64);
        var linhaGigante = new string('A', 500);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            importador.ImportarAsync(ComoStream(linhaGigante), long.MaxValue, null, CancellationToken.None));

        Assert.Equal(pool.Alugados, pool.Devolvidos);
    }

    [Fact]
    public async Task Assincrono_CancelamentoAntesDeComecar_DevolveBuffer()
    {
        var pool = new PoolContador();
        var importador = new ImportadorAssincronoOtimizado(pool);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            importador.ImportarAsync(ComoStream(GerarLote(10, false)), long.MaxValue, null, cts.Token));

        Assert.Equal(1, pool.Alugados);
        Assert.Equal(1, pool.Devolvidos);
    }

    [Fact]
    public async Task Assincrono_CancelamentoDuranteALeitura_DevolveBuffer()
    {
        var pool = new PoolContador();
        var importador = new ImportadorAssincronoOtimizado(pool);
        using var cts = new CancellationTokenSource();
        var bytes = Encoding.UTF8.GetBytes(GerarLote(50, false));
        await using var stream = new CancelaNaPrimeiraLeitura(bytes, cts);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            importador.ImportarAsync(stream, long.MaxValue, null, cts.Token));

        Assert.Equal(1, pool.Alugados);
        Assert.Equal(1, pool.Devolvidos);
    }

    [Fact]
    public async Task Assincrono_FalhaNoLog_DevolveBuffer()
    {
        var pool = new PoolContador();
        var importador = new ImportadorAssincronoOtimizado(pool);

        await Assert.ThrowsAsync<IOException>(() =>
            importador.ImportarAsync(ComoStream(GerarLote(10, false)), long.MaxValue, new LogQueFalha(), CancellationToken.None));

        Assert.Equal(1, pool.Alugados);
        Assert.Equal(1, pool.Devolvidos);
    }

    [Fact]
    public async Task Assincrono_SucessoDevolveBufferUmaVez()
    {
        var pool = new PoolContador();

        await new ImportadorAssincronoOtimizado(pool)
            .ImportarAsync(ComoStream(GerarLote(10, false)), long.MaxValue, null, CancellationToken.None);

        Assert.Equal(1, pool.Alugados);
        Assert.Equal(1, pool.Devolvidos);
    }

    [Fact]
    public async Task Assincrono_NaoUsaBufferDepoisDeDevolver()
    {
        // O pool "suja" o array ao devolver (como outro código poderia fazer ao reutilizá-lo).
        // Se o resultado dependesse do buffer (ex.: strings/spans apontando para ele), ficaria errado.
        var conteudo = GerarLote(100, comErros: true);
        var esperado = ImportadorPedidosBaseline.Importar(conteudo);
        var pool = new PoolContador { SujarAoDevolver = true };

        var r = await new ImportadorAssincronoOtimizado(pool, tamanhoBuffer: 256)
            .ImportarAsync(ComoStream(conteudo), long.MaxValue, null, CancellationToken.None);

        Assert.Equal(esperado.Pedidos, r.Pedidos);
        Assert.Equal(esperado.Erros, r.Erros);
    }

    [Fact]
    public async Task Assincrono_DadoSensivel_SolicitaLimpezaNaDevolucao()
    {
        var poolLimpo = new PoolContador();
        var poolSemLimpar = new PoolContador();
        var conteudo = GerarLote(20, false);

        await new ImportadorAssincronoOtimizado(poolLimpo, limparBuffer: true)
            .ImportarAsync(ComoStream(conteudo), long.MaxValue, null, CancellationToken.None);
        await new ImportadorAssincronoOtimizado(poolSemLimpar, limparBuffer: false)
            .ImportarAsync(ComoStream(conteudo), long.MaxValue, null, CancellationToken.None);

        Assert.Equal(new[] { true }, poolLimpo.LimpezaSolicitada);
        Assert.All(poolLimpo.Ultimo!, c => Assert.Equal('\0', c));

        Assert.Equal(new[] { false }, poolSemLimpar.LimpezaSolicitada);
        Assert.Contains(poolSemLimpar.Ultimo!, c => c != '\0'); // dados ficaram no buffer
    }

    [Fact]
    public async Task Assincrono_StreamGrande_ParaNoLimite_SemLerOCorpoTodo()
    {
        var stream = new StreamGerado(200_000);            // ~12 MB se fosse lido por inteiro
        var pool = new PoolContador();

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new ImportadorAssincronoOtimizado(pool)
                .ImportarAsync(stream, limiteCaracteres: 100_000, null, CancellationToken.None));

        Assert.True(stream.Leituras > 1);                  // leitura incremental
        Assert.True(stream.BytesLidos < 1_000_000);        // parou perto do limite
        Assert.Equal(pool.Alugados, pool.Devolvidos);
    }

    // ---------- apoio ----------

    private static string GerarLote(int quantidade, bool comErros)
    {
        var sb = new StringBuilder();
        var regioes = new[] { "Sudeste", "Sul", "Nordeste", "CentroOeste", "Norte", "Exterior" };
        var prioridades = new[] { "Baixa", "Normal", "Alta", "Urgente" };

        for (var i = 1; i <= quantidade; i++)
        {
            var id = new Guid(i, 0, 0, new byte[8]);
            var regiao = comErros && i % 7 == 0 ? "Marte" : regioes[i % regioes.Length];
            var itens = comErros && i % 11 == 0
                ? "ABC:x:1.00"
                : $"SKU{i}:{(i % 5) + 1}:{(i % 90) + 10}.50|OUTRO{i}:1:5.00";

            sb.Append(id).Append(';').Append(regiao).Append(';').Append(prioridades[i % prioridades.Length])
              .Append(';').Append(itens);
            if (i < quantidade) sb.Append('\n');
        }

        return sb.ToString();
    }

    private static MemoryStream ComoStream(string texto) => new(Encoding.UTF8.GetBytes(texto));

    private sealed class PoolContador : ArrayPool<char>
    {
        public int Alugados { get; private set; }
        public int Devolvidos { get; private set; }
        public List<bool> LimpezaSolicitada { get; } = new();
        public bool SujarAoDevolver { get; init; }
        public char[]? Ultimo { get; private set; }

        public override char[] Rent(int minimumLength)
        {
            Alugados++;
            Ultimo = new char[minimumLength];
            // simula o "lixo" de um buffer reaproveitado (nunca assumir que vem zerado)
            Array.Fill(Ultimo, '?');
            return Ultimo;
        }

        public override void Return(char[] array, bool clearArray = false)
        {
            Devolvidos++;
            LimpezaSolicitada.Add(clearArray);

            if (SujarAoDevolver)
            {
                Array.Fill(array, '#');
            }
            else if (clearArray)
            {
                Array.Clear(array);
            }
        }
    }

    private sealed class ContaFlush : StringWriter
    {
        public int Flushes { get; private set; }

        public override Task FlushAsync()
        {
            Flushes++;
            return Task.CompletedTask;
        }

        public override Task FlushAsync(CancellationToken cancellationToken) => FlushAsync();
    }

    private sealed class LogQueFalha : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override Task WriteAsync(StringBuilder? value, CancellationToken cancellationToken = default) =>
            Task.FromException(new IOException("falha simulada no log"));

        public override Task FlushAsync() => Task.FromException(new IOException("falha simulada no log"));
    }

    private sealed class CancelaNaPrimeiraLeitura : MemoryStream
    {
        private readonly CancellationTokenSource _cts;

        public CancelaNaPrimeiraLeitura(byte[] bytes, CancellationTokenSource cts) : base(bytes) => _cts = cts;

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _cts.Cancel();
            return base.ReadAsync(buffer, cancellationToken);
        }
    }

    // Stream que GERA as linhas sob demanda (nunca guarda o lote inteiro na memória)
    // e conta quantas leituras e bytes foram pedidos.
    private sealed class StreamGerado : Stream
    {
        private readonly int _totalLinhas;
        private int _proximaLinha = 1;
        private byte[] _atual = Array.Empty<byte>();
        private int _posicao;

        public StreamGerado(int totalLinhas) => _totalLinhas = totalLinhas;

        public int Leituras { get; private set; }
        public long BytesLidos { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            Preencher(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer) => Preencher(buffer);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<int>(Preencher(buffer.Span));
        }

        private int Preencher(Span<byte> destino)
        {
            Leituras++;
            var escritos = 0;

            while (escritos < destino.Length)
            {
                if (_posicao >= _atual.Length)
                {
                    if (_proximaLinha > _totalLinhas)
                    {
                        break; // fim do "arquivo"
                    }

                    var id = new Guid(_proximaLinha, 0, 0, new byte[8]);
                    _atual = Encoding.UTF8.GetBytes($"{id};Sul;Alta;SKU{_proximaLinha}:1:10.50\n");
                    _posicao = 0;
                    _proximaLinha++;
                }

                var copiar = Math.Min(destino.Length - escritos, _atual.Length - _posicao);
                _atual.AsSpan(_posicao, copiar).CopyTo(destino[escritos..]);
                _posicao += copiar;
                escritos += copiar;
            }

            BytesLidos += escritos;
            return escritos;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}