# Casa Inteligente de Compras

Sistema inteligente de compras domésticas que captura dados de compras a partir do QR Code da NFC-e, processa e organiza automaticamente os produtos e valores. O projeto centraliza o histórico de consumo familiar, permitindo gerar listas de compras e, futuramente, oferecer previsões, comparações de preços e recomendações personalizadas.

## Visão Geral

O **Casa Inteligente de Compras** é composto por:

| Camada | Tecnologia | Status |
|--------|-----------|--------|
| API | ASP.NET Core 8 | ✅ Implementado |
| Banco de Dados | PostgreSQL 16 + EF Core 8 | ✅ Implementado |
| PWA (Frontend) | A definir | 🔜 Planejado |
| Processamento NFC-e | A definir | 🔜 Planejado |
| IA / Recomendações | A definir | 🔜 Planejado |

## Estrutura do Projeto

```
smart-grocery-nfce/
├── src/
│   └── SmartGrocery.Api/         # API ASP.NET Core 8
│       ├── Controllers/          # Endpoints REST
│       ├── Data/                 # DbContext (EF Core)
│       ├── Migrations/           # Migrações do banco
│       └── Models/               # Entidades: Purchase, Product, PurchaseItem
├── pwa/                          # Pasta reservada para a futura PWA
├── docker-compose.yml            # PostgreSQL + API em container
└── README.md
```

## Entidades

- **Product** – produto identificado pelo nome, marca, código de barras e unidade de medida.
- **Purchase** – compra realizada em uma loja, com data, valor total e URL da NFC-e.
- **PurchaseItem** – item da compra que relaciona `Purchase` e `Product` com quantidade e preço.

## Endpoints da API

| Método | Rota | Descrição |
|--------|------|-----------|
| GET | `/api/purchases` | Histórico de compras |
| GET | `/api/purchases/{id}` | Detalhes de uma compra |
| POST | `/api/purchases` | Registrar nova compra |
| DELETE | `/api/purchases/{id}` | Remover compra |
| GET | `/api/products` | Listar produtos |
| GET | `/api/products/{id}` | Detalhes de um produto |
| POST | `/api/products` | Cadastrar produto |
| PUT | `/api/products/{id}` | Atualizar produto |
| DELETE | `/api/products/{id}` | Remover produto |

## Como Rodar Localmente

### Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker & Docker Compose](https://docs.docker.com/compose/)

### Com Docker Compose

```bash
# Defina a senha do PostgreSQL
export POSTGRES_PASSWORD=sua_senha_aqui

# Suba o banco e a API
docker compose up --build
```

A API estará disponível em `http://localhost:5000` e o Swagger em `http://localhost:5000/swagger`.

### Desenvolvimento Local

```bash
# Suba apenas o banco de dados
export POSTGRES_PASSWORD=sua_senha_aqui
docker compose up db -d

# Configure a connection string via user secrets (não commite senhas)
cd src/SmartGrocery.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5432;Database=smart_grocery;Username=postgres;******"

# Aplique as migrações
dotnet ef database update

# Execute a API
dotnet run
```

## Funcionalidades Futuras

- [ ] Leitura de QR Code da NFC-e via câmera (PWA)
- [ ] Processamento automático do XML da NFC-e
- [ ] Histórico de compras com filtros e gráficos
- [ ] Geração de lista de compras baseada no histórico
- [ ] Comparação de preços entre lojas
- [ ] Sugestões e previsões com IA

## Licença

MIT

