using System.Threading.Channels;
using Relatorio004.Models;

namespace Relatorio004.Services;

public static class FilaFactory
{
    // Wait      -> quem escreve espera abrir vaga (nada e perdido)
    // DropWrite -> o item novo e descartado
    // DropNewest-> descarta o item mais novo que ja estava na fila
    // DropOldest-> descarta o item mais antigo da fila
    // O callback so e chamado nos modos de descarte, e e ali que contamos "descartados"
    public static Channel<ProcessarPedidoJob> Criar(int capacidade, BoundedChannelFullMode modo, Metricas metricas)
    {
        var opcoes = new BoundedChannelOptions(capacidade)
        {
            FullMode = modo
        };

        return Channel.CreateBounded<ProcessarPedidoJob>(opcoes, itemDescartado =>
        {
            metricas.Somar("descartados");
        });
    }
}
