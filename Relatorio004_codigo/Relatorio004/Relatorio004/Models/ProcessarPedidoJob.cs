namespace Relatorio004.Models;

// job imutavel, so tem dados simples (nada de contexto do EF)
public record ProcessarPedidoJob(Guid Id, string Cliente, decimal Valor, DateTime CriadoEm);
