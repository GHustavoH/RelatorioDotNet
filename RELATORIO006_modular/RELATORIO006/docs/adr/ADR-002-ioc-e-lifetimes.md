# ADR-002 — IoC, composition root e tempos de vida

**Status:** aceita

## Decisão
- `Program.cs` é o único composition root e chama uma extensão por módulo:
  `AddOrdersModule()`, `AddNotificationsModule()`, `AddProcessingModule()` (em `Hub.Infrastructure/Modules`).
- Nenhum código de módulo recebe `IServiceProvider`/`IServiceScopeFactory`; assinantes de evento chegam como `IEnumerable<IOrderPlacedHandler>`.

## Tempos de vida

| Serviço | Lifetime | Justificativa |
|---|---|---|
| `TimeProvider` | Singleton | Sem estado |
| `IOrderRepository` (`InMemoryOrderRepository`) | Singleton | Mantém o estado em memória; não depende de scoped |
| `IOrderEventPublisher` | Scoped | Depende de assinantes Scoped |
| `IOrderService` | Scoped | Depende do publicador Scoped (unidade de trabalho por requisição) |
| `IOrderPlacedHandler` (`ProcessOrderHandler`) | Scoped | Participa da unidade de trabalho da requisição |
| `IProcessingNotifier` | Transient | Adaptador sem estado |
| `INotificationSender` / `INotificationService` | Singleton | Sem estado; só dependem de singleton |

## Como é verificado
`Hub.Tests/Container/ContainerTests.cs`: `ValidateOnBuild` + `ValidateScopes`, resolução dos fluxos principais,
varredura de dependência cativa (incluindo prova de que o detector acusa um caso propositalmente inválido)
e subida do host real com validação estrita.

## Alternativas descartadas
- **Tudo Scoped:** repositório em memória perderia o estado a cada requisição.
- **Tudo Singleton:** capturaria serviços de unidade de trabalho (dependência cativa).
- **Resolver serviços via `IServiceProvider` no caso de uso:** esconde dependências e dificulta teste.
