namespace RelatorioFinal.Fila;

// job imutavel com o minimo de dados e sem contexto do EF
public record ProcessarPedidoJob(Guid PedidoId, DateTime EnfileiradoEm);
