namespace relatorio007.Services;

public class Assinaturas
{
    private readonly List<(string Conexao, string Usuario, int Pedido)> _itens = new();
    private readonly object _lock = new();

    public void Adicionar(string conexao, string usuario, int pedido)
    {
        lock (_lock)
        {
            if (!_itens.Any(i => i.Conexao == conexao && i.Pedido == pedido))
                _itens.Add((conexao, usuario, pedido));
        }
    }

    public void RemoverConexao(string conexao)
    {
        lock (_lock)
        {
            _itens.RemoveAll(i => i.Conexao == conexao);
        }
    }

    public List<string> RemoverUsuarioDoPedido(string usuario, int pedido)
    {
        lock (_lock)
        {
            var conexoes = _itens
                .Where(i => i.Usuario == usuario && i.Pedido == pedido)
                .Select(i => i.Conexao)
                .ToList();

            _itens.RemoveAll(i => i.Usuario == usuario && i.Pedido == pedido);
            return conexoes;
        }
    }
}
