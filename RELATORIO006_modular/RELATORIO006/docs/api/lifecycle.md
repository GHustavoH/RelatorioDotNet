# Ciclo de vida das versões da API

| Versão | Estado | Depreciada em | Sunset (remoção) |
|---|---|---|---|
| v1 | **Depreciada** (padrão quando a versão é omitida) | 2026-11-20 | 2027-06-30 |
| v2 | Atual | — | — |

Datas configuráveis em `appsettings.json` → `ApiVersioning:Deprecations`.

## Sinalização ao consumidor
Toda resposta de v1 inclui:
- `Deprecation: @<unix-seconds>` (RFC 9745)
- `Sunset: <HTTP-date>` (RFC 8594)
- `Link: <…>; rel="deprecation"`

Toda resposta em `/api` inclui `api-supported-versions`.

## Observação de uso
Métrica `hub.api.requests` (meter `RELATORIO006.ApiVersioning`, tag `api.version`), emitida pelo `ApiVersionMiddleware`.
Exportável por OpenTelemetry/`dotnet-counters monitor --counters RELATORIO006.ApiVersioning`.

## Critérios para remover v1 (todos obrigatórios)
1. Data de Sunset alcançada **e** comunicada com pelo menos 90 dias de antecedência.
2. `hub.api.requests{api.version=1}` igual a **0** por 30 dias consecutivos (ou tráfego residual aceito por escrito pelo responsável).
3. Todos os consumidores conhecidos confirmaram a migração para v2.
4. Etapa intermediária (opcional, 2 semanas antes): responder `410 Gone` em v1 em ambiente de homologação.

## Como remover
1. Remover `OrdersV1Controller`, `OrderV1Response`, `ToV1` e o documento `v1` de `ApiVersioningExtensions.DocumentNames`.
2. Remover `1` de `ApiVersioningOptions.SupportedVersions` e **mudar `DefaultVersion` para 2** (versão ausente passa a seguir a política explícita).
3. Atualizar/remover os testes de `V1CompatibilityTests` junto com o cliente fixture.
