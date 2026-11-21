# ADR-001 — Módulos e direção das dependências

**Status:** aceita

## Decisão
Três módulos com responsabilidades distintas, cada um em um projeto próprio:

| Módulo | Projeto | Responsabilidade | Pode referenciar |
|---|---|---|---|
| Pedidos | `Hub.Orders` | Agregado `Order`, caso de uso de criar/consultar, **portas** de repositório e de eventos | nada |
| Processamento | `Hub.Processing` | Reage a `OrderPlaced`, processa o pedido | `Hub.Orders` |
| Notificações | `Hub.Notifications` | Monta e envia notificações | nada |

`Hub.Infrastructure` implementa as portas e contém os registros de DI. `RELATORIO006` (API) é o composition root.

Regra de fronteira: **a interface fica no módulo que a consome; a implementação fica na infraestrutura.**
Ex.: `IProcessingNotifier` é de Processamento; o adaptador `ProcessingNotifierAdapter` (infra) delega a Notificações.

## Como é verificado
`Hub.Tests/Architecture/ArchitectureTests.cs`: lê os `.csproj` (referências permitidas), inspeciona assemblies
(sem ASP.NET, EF Core ou outros módulos), garante que portas são implementadas só na infraestrutura e que não há service locator.

## Alternativas descartadas
- **Pastas em um único projeto + diagrama:** a direção não seria imposta pelo compilador.
- **Processamento referenciando Notificações diretamente:** acopla módulos; trocado por porta + adaptador.
- **NetArchTest/ArchUnitNET:** adicionaria pacote; reflexão + leitura de `.csproj` cobre a necessidade.
