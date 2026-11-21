# Matriz de mudanças v1 → v2 — `GET /api/orders/{id}`

| Campo | v1 (congelado) | v2 | Compatível? |
|---|---|---|---|
| `id` | string (guid) | string (guid) | ✅ |
| `customerName` | string | string | ✅ |
| **`total`** | **número** (`150.75`) | **objeto** (`{ "amount": 150.75, "currency": "BRL" }`) | ❌ **mudança incompatível deliberada** |
| `status` | string (`Pending`/`Processed`/`Cancelled`) | igual | ✅ |
| `createdAt` | string (ISO 8601) | igual | ✅ |

**Motivo da mudança:** o número simples não informa a moeda; v2 torna a moeda explícita.
**Semântica preservada em v1:** `total` continua sendo o valor numérico do pedido (`Order.Total.Amount`).

## Exemplos
v1 (padrão quando a versão é omitida)
```json
{ "id": "…", "customerName": "Maria", "total": 150.75, "status": "Processed", "createdAt": "2026-11-20T03:00:00+00:00" }
```
v2 (`X-Api-Version: 2`)
```json
{ "id": "…", "customerName": "Maria", "total": { "amount": 150.75, "currency": "BRL" }, "status": "Processed", "createdAt": "2026-11-20T03:00:00+00:00" }
```

## Teste que protege esta matriz
`Hub.Tests/Compatibility/V1CompatibilityTests.cs` (cliente v1 congelado + snapshot do contrato) e
`Hub.Tests/Api/VersioningTests.cs`.
