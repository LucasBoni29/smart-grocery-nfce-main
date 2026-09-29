# Casa Inteligente de Compras

Sistema inteligente de compras domésticas que captura dados de compras a partir do QR Code da NFC-e, processa e organiza automaticamente os produtos e valores. O projeto centraliza o histórico de consumo familiar, permitindo gerar listas de compras e, futuramente, oferecer previsões, comparações de preços e recomendações personalizadas.

## Visão Geral

O **Casa Inteligente de Compras** é composto por:

| Camada | Tecnologia | Status |
|--------|-----------|--------|
| API | ASP.NET Core 8 | ✅ Implementado |
| Banco de Dados | PostgreSQL 16 + EF Core 8 | ✅ Implementado |
| PWA (Frontend) | Angular 18 | 🚧 Em desenvolvimento |
| Processamento NFC-e | HTML parsing (SEFAZ-SP) | ✅ Implementado |
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
| POST | `/api/purchases/import-nfce` | Capturar URL de uma NFC-e |
| DELETE | `/api/purchases/{id}` | Remover compra |
| GET | `/api/products` | Listar produtos |
| GET | `/api/products/{id}` | Detalhes de um produto |
| POST | `/api/products` | Cadastrar produto |
| PUT | `/api/products/{id}` | Atualizar produto |
| DELETE | `/api/products/{id}` | Remover produto |

### Captura de NFC-e (Versão 0)

Envie a URL obtida pelo QR Code para consultar a NFC-e publica. A API extrai e valida a chave de acesso de 44 digitos, processa os dados da nota e registra a compra, produtos e itens em uma unica operacao.

```http
POST /api/purchases/import-nfce
Content-Type: application/json

{
  "nfceUrl": "https://www.exemplo.gov.br/nfce?qrcode=..."
}
```

Uma importacao valida retorna `201 Created`. Uma URL sem chave NFC-e valida retorna `400 Bad Request`; uma consulta recusada ou em formato nao suportado retorna `422 Unprocessable Entity`; uma nota ja processada retorna `409 Conflict`. Uma captura antiga, sem itens, pode ser reenviada para ser processada.

### Autenticação por chave de API

A API aceita um header `X-Api-Key` como trava simples contra acesso não autorizado. Ela é uma
senha compartilhada, não um login por usuário — protege contra quem não conhece a chave, mas não
distingue quem está chamando.

- **Em desenvolvimento local** (`ASPNETCORE_ENVIRONMENT=Development`, o padrão do `docker compose`
  e do `dotnet run`): a variável `ApiKey` é opcional. Se ficar em branco, a API roda aberta, como
  sempre rodou.
- **Fora de desenvolvimento** (qualquer deploy, ex.: Railway): a variável `ApiKey` é obrigatória.
  Sem ela, a API recusa subir.

Pra testar a trava localmente, defina `API_KEY` no `.env` antes do `docker compose up` (mesmo
arquivo onde já fica o `POSTGRES_PASSWORD`), ou `ApiKey` via `dotnet user-secrets` no projeto da
API. Toda chamada precisa então do header:

```http
GET /api/purchases
X-Api-Key: <a mesma chave configurada na API>
```

O Swagger (`/swagger`) fica de fora da trava, porque só serve documentação, nunca dado.

**Na PWA:** `pwa/src/environments/environment.ts` também tem um campo `apiKey`. Ele é
versionado com valor vazio (`''`) — igual o `environment.prod.ts` — pra quem clonar o
repositório continuar compilando. Se você preencher esse arquivo localmente com a chave
real, rode `git update-index --skip-worktree pwa/src/environments/environment.ts` uma vez:
isso faz o Git parar de enxergar essa mudança pra sempre, então a chave nunca aparece num
`git status` nem é commitada por engano. Pra reverter (voltar a rastrear o arquivo normal):
`git update-index --no-skip-worktree pwa/src/environments/environment.ts`.

## Como Rodar Localmente

### Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Docker rodando dentro de uma distribuição WSL (ex.: `Ubuntu-24.04`)

### Primeira vez nesta máquina

Crie o arquivo `.env` na raiz do projeto (ele já está no `.gitignore`, não é commitado):

```bash
wsl -d Ubuntu-24.04
cd /mnt/d/Projects/smart-grocery-nfce
echo "POSTGRES_PASSWORD=sua_senha_aqui" > .env
```

Esse arquivo é lido automaticamente pelo `docker compose`, então a senha não precisa ser exportada novamente em outras sessões.

### Ligar a infra no dia a dia

Sempre que for continuar o desenvolvimento (após reiniciar o PC, o Docker ou o WSL):

```bash
# 1. Abra a distribuição onde o Docker roda
wsl -d Ubuntu-24.04

# 2. Entre na pasta do projeto
cd /mnt/d/Projects/smart-grocery-nfce

# 3. Suba o banco e a API (usa a senha do .env automaticamente)
docker compose up --build -d

# 4. Confirme que os containers estão de pé
docker ps
```

Você deve ver `smart_grocery_api` e `smart_grocery_db` com status `Up`. A API estará em `http://localhost:5000` e o Swagger em `http://localhost:5000/swagger`.

### Ver logs da API

```bash
docker compose logs -f api
```

### Desligar a infra

```bash
docker compose down
```

Os dados do PostgreSQL persistem no volume `pg_data` entre desligamentos; eles só são apagados com `docker compose down -v`.

### Desenvolvimento sem Docker para a API

```bash
# Suba apenas o banco de dados
docker compose up db -d

# Configure a connection string via user secrets (não commite senhas)
cd src/SmartGrocery.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5432;Database=smart_grocery;Username=postgres;Password=sua_senha_aqui"

# Aplique as migrações
dotnet tool restore
dotnet tool run dotnet-ef database update

# Execute a API
dotnet run --launch-profile http
```

### PWA (Angular)

Pré-requisito: [Node.js 20+](https://nodejs.org/).

```bash
cd pwa
npm install

# Ambiente de desenvolvimento (usa http://localhost:5000/api, ver src/environments/environment.ts)
npm start
```

A PWA abre em `http://localhost:4200`. Para testar no iPhone, conecte o computador e o celular à mesma rede Wi-Fi e abra `http://IP_DO_COMPUTADOR:4200` (por exemplo, `http://192.168.0.70:4200`). A tela inicial pede permissão de câmera, escaneia o QR Code da NFC-e e envia a URL lida para `POST /api/purchases/import-nfce`.

Para testar a câmera no iPhone, use uma URL HTTPS. O modo recomendado para desenvolvimento é iniciar a PWA e criar um túnel HTTPS:

```powershell
cd pwa
npm start
ngrok http 4200
```

Abra no iPhone a URL `https://*.ngrok-free.app` exibida pelo ngrok. O proxy em `proxy.conf.json` encaminha `/api` para a API local sem expor uma chamada HTTP ao navegador.


## Funcionalidades Futuras

- [ ] Leitura de QR Code da NFC-e via câmera (PWA)
- [ ] Processamento automático do XML da NFC-e
- [ ] Histórico de compras com filtros e gráficos
- [ ] Geração de lista de compras baseada no histórico
- [ ] Comparação de preços entre lojas
- [ ] Sugestões e previsões com IA

## Licença

MIT

