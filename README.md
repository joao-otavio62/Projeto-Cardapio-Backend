# PizzaFlow — Backend (.NET 8 · Clean Architecture)

API de pedidos para pizzarias/restaurantes: cardápio, checkout (PIX + cartão via Mercado Pago), painel admin (JWT),
SignalR para notificações em tempo real, chatbot de IA, relatórios e upload de mídia no Cloudinary.

```
src/
  PizzaFlow.Domain          entidades, enums, CPF, regras de status/estoque/preço (sem dependências)
  PizzaFlow.Application     serviços (casos de uso), DTOs, validadores, contratos (repositórios/gateways)
  PizzaFlow.Infrastructure  EF Core + Postgres, repositórios, JWT, Mercado Pago, Cloudinary, Anthropic, SignalR
  PizzaFlow.API             controllers, middleware, rate limiting, Swagger, Program.cs
tests/PizzaFlow.Domain.Tests
env/                        .env.{local,dev,prod}.example
```

## Primeira execução (local)

```bash
# 1. banco
docker compose up -d

# 2. variáveis
cp env/.env.local.example env/.env.local     # edite: Jwt__Secret, Seed__Admin*, chaves (MP, Cloudinary, Anthropic)

# 3. gerar a migration inicial (uma vez; o EF cria as tabelas, índices GIN/pg_trgm e constraints)
./scripts/migrate.sh add Initial

# 4. rodar (aplica migrations e cria loja demo + admin automaticamente em Local/Dev)
dotnet run --project src/PizzaFlow.API
# Swagger: http://localhost:5080/swagger
```

Teste rápido: `POST /api/admin/auth/login` com o admin do seed → copie o token → **Authorize** no Swagger.
Loja demo: slug `pizzaria-demo` (`GET /api/public/stores/pizzaria-demo/menu?search=calabresa`).

Ambientes: `APP_ENV=local|dev|prod` escolhe `env/.env.<x>` e o `appsettings.<Env>.json`.
Variáveis do servidor sobrescrevem o arquivo. Produção: `./scripts/migrate.sh update` no deploy (AutoMigrate fica desligado).

## Rotas

| Área | Rotas |
|---|---|
| Público | `GET /api/public/stores/{slug}` · `GET …/menu?search=&categoryId=` · `POST …/checkout` · `GET …/orders/{code}` · `POST …/orders/lookup` (CPF) · `POST …/chat` |
| Webhook | `POST /api/webhooks/mercadopago` |
| Admin (JWT) | `auth/login` · `store` · `categories` · `promotions` · `products` (+`/stock`, `/media`) · `orders` (+`/unseen`, `/seen`, `PATCH /status`) · `reports/summary` |
| Tempo real | SignalR `/hubs/admin-notifications` → eventos `NewOrder` e `OrderStatusChanged` (token em `?access_token=`) |

## Fluxo de pagamento
1. `POST checkout` valida CPF/itens, **recalcula preços no servidor**, reserva estoque, cria pedido (`PendingPayment`) e o pagamento no gateway.
2. **PIX**: resposta traz QR/copia-e-cola; o webhook do MP confirma → pedido `Received` → evento `NewOrder` no sino.
   **Cartão**: o frontend envia só o `token` do SDK do MP; aprovação imediata já dispara o `NewOrder`.
3. Pagamento rejeitado/expirado → pedido `Cancelled` e estoque devolvido.
4. Webhook: assinatura `x-signature` validada, status reconsultado na API do MP, idempotente (`external_id` único + `xmin`).

Status: `PendingPayment → Received → Preparing → OutForDelivery | ReadyForPickup → Finished` (`Cancelled` até `Preparing`).

## Segurança
JWT (HS256, claim `store_id` isola cada loja) · rota protegida por padrão (`FallbackPolicy`) · rate limit por IP
(global 120/min; login 5/min; checkout/lookup 10/min; chat 15/min) · FluentValidation + CPF · BCrypt · sem dados de cartão ·
consulta pública mascarada (só primeiro nome, sem CPF/endereço) · headers de segurança · HSTS em produção.

## Observações importantes
- **Não compilado no ambiente em que foi gerado** (sem SDK .NET). Rode `dotnet build`; se algum erro aparecer, me envie a saída.
- Busca fuzzy: coluna `name_search` (sem acento/minúscula, mantida pela aplicação) + índice GIN `pg_trgm` — não depende de `unaccent`.
- Atrás de proxy, `ForwardedHeaders` confia em qualquer proxy; em produção restrinja `KnownProxies` à rede do seu host.
- Cancelar pedido pago **não estorna** automaticamente no MP ainda (próxima iteração).
- LGPD: CPF está em texto puro; para produção, criptografar + coluna hash para busca.
- Pacotes em .NET 8; para .NET 10 troque `TargetFramework` e as versões dos pacotes `Microsoft.*`/Npgsql.
