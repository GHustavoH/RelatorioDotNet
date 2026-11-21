namespace Relatorio005.Hub;

public sealed record Mensagem(
    Guid Id,
    long Sequencia,
    string Canal,
    string Conteudo,
    int Prioridade,
    DateTimeOffset CriadaEm);
