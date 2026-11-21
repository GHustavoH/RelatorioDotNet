# ADR-003 — Política de versionamento da API

**Status:** aceita • **Endpoint selecionado:** `GET /api/orders/{id}` (consulta de pedido)

## Consumidores conhecidos
| Consumidor | Versão | Observação |
|---|---|---|
| Front-end anterior (cliente v1) | v1 | Nunca enviou versão; depende de `total` numérico |
| Novos clientes | v2 | Devem informar a versão explicitamente |

## Política
- **Leitura:** header `X-Api-Version` **ou** query `api-version` (inteiro). Ambos presentes e diferentes ⇒ `400`.
- **Padrão (ausente):** **v1**, para que o cliente anterior continue funcionando sem alteração.
- **Não suportada / mal formada / ambígua:** `400 application/problem+json` com `supportedVersions`.
- **Seleção:** `ApiVersionMiddleware` resolve a versão antes do roteamento; `[MapToApiVersion(n)]` (action constraint) escolhe a ação.
- **OpenAPI por versão:** `/openapi/v1.json` e `/openapi/v2.json` (somente em Development).
- **Comando `POST /api/orders`:** não versionado (idêntico nas duas versões) e presente nos dois documentos.

## Alternativas descartadas
- **Versão na URL (`/v2/...`):** o cliente v1 precisaria mudar de URL para qualquer migração e a versão padrão seria menos explícita.
- **Media type (`Accept: ...;v=2`):** mais difícil de testar/depurar e de observar em logs.
- **Pacote Asp.Versioning:** válido, mas o mecanismo próprio é pequeno, sem dependências e totalmente testado aqui.
- **Alterar v1 in-place:** quebraria o consumidor existente.
