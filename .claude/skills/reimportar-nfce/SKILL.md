---
name: reimportar-nfce
description: >
  Reseta e reimporta uma NFC-e na API local pra testar uma correção no parser, sem apagar dados
  na mão no banco. Apaga a compra pela API, reimporta a mesma nota e mostra um relatório
  (loja, data, total, itens, conferência de valores, produtos órfãos). Use quando o usuário disser
  "reimportar nota", "reimporta essa NFC-e", "testar de novo essa nota", "apagar e importar de novo",
  "/reimportar-nfce", ou colar uma URL de QR Code / chave de 44 dígitos pra reprocessar.
---

# /reimportar-nfce — Reimportar uma NFC-e sem mexer no banco

Existe pra cortar o ciclo manual de teste: ajustar o parser, apagar a compra no banco, reescanear,
importar de novo. Tudo passa pela API, e o banco não é tocado direto.

**Por que funciona:** `DELETE /api/purchases/{id}` apaga a compra e os itens em cascata, e o
`POST /api/purchases/import-nfce` reimporta a mesma chave depois. Os produtos ficam no banco
(`OnDelete Restrict` nos itens), então nomes errados de um parser com bug sobram como produtos
órfãos. Esta skill lista os órfãos no final.

## Input

Uma URL completa do QR Code **ou** a chave de 44 dígitos.
- URL completa: sempre funciona.
- Só a chave: funciona se a nota já foi importada antes (a URL é lida da compra salva).
  Se a nota é nova e o usuário passou só a chave, pedir a URL completa.

Se o usuário não passou nada, perguntar: "Qual nota? Cola a URL do QR Code ou a chave de 44 dígitos."

## Regras de segurança

- **Só localhost.** Nunca rodar contra uma API publicada. O snippet testa só `localhost:5000` e `localhost:5170`.
- **Nunca apagar produto sem confirmação.** Produto pode ter sido cadastrado à mão. Só listar os órfãos
  da importação anterior e perguntar antes de apagar.
- Se a importação falhar depois de a compra antiga ser apagada, avisar isso claramente: a compra antiga
  não existe mais e a nova não entrou.
- Não mexer no banco direto (psql, SQL). Tudo via API.

## Passos

### 1. Rodar o snippet

Preencher `$nota` e rodar no PowerShell (colar direto, sem salvar em arquivo):

```powershell
$ErrorActionPreference = 'Stop'; [Console]::OutputEncoding = [Text.Encoding]::UTF8
$nota = '<URL do QR Code ou chave de 44 dígitos>'
# Se a API tiver a trava ligada (variável ApiKey), define antes: $env:SMARTGROCERY_API_KEY = '...'
$hdr = if ($env:SMARTGROCERY_API_KEY) { @{ 'X-Api-Key' = $env:SMARTGROCERY_API_KEY } } else { @{} }
$candidatos = @('http://localhost:5000', 'http://localhost:5170')   # 5000 = docker, 5170 = dotnet run
$key = [regex]::Match($nota, '(?<!\d)\d{44}(?!\d)').Value
$base = $candidatos | Where-Object { try { (Invoke-WebRequest "$_/api/purchases" -Headers $hdr -UseBasicParsing -TimeoutSec 3).StatusCode -eq 200 } catch { $false } } | Select-Object -First 1
if (-not $base) { 'API fora do ar (ou recusou por causa da X-Api-Key — confere $env:SMARTGROCERY_API_KEY)' } else {
    $old = @(Invoke-RestMethod "$base/api/purchases" -Headers $hdr) | Where-Object { $_.nfceAccessKey -eq $key } | Select-Object -First 1
    $url = if ($nota -match '^https?://') { $nota } else { $old.nfceUrl }
    $oldIds = @($old.items.productId | Sort-Object -Unique)
    if ($old) { Invoke-RestMethod -Method Delete "$base/api/purchases/$($old.id)" -Headers $hdr | Out-Null; "Compra $($old.id) apagada ($(@($old.items).Count) itens)" }
    try { $new = Invoke-RestMethod -Method Post "$base/api/purchases/import-nfce" -Headers $hdr -ContentType 'application/json' -Body (@{ nfceUrl = $url } | ConvertTo-Json) }
    catch { "Falhou: HTTP $([int]$_.Exception.Response.StatusCode) $($_.ErrorDetails.Message)"; $new = $null }
    if ($new) {
        $p = Invoke-RestMethod "$base/api/purchases/$($new.id)" -Headers $hdr
        $items = @($p.items)
        $soma = [math]::Round(($items | Measure-Object totalPrice -Sum).Sum, 2)
        "Compra $($p.id) | $($p.storeName) | $($p.purchasedAt) | $($items.Count) itens | total $($p.totalAmount) | soma $soma | dif $([math]::Round($p.totalAmount - $soma, 2))"
        $items | Select-Object @{N='Produto';E={$_.product.name}}, quantity, unit, unitPrice, totalPrice, @{N='EAN';E={$_.product.barcode}} | Format-Table -AutoSize | Out-String -Width 200
        $ruins = $items | Where-Object { [math]::Abs($_.quantity * $_.unitPrice - $_.totalPrice) -gt 0.05 }
        if ($ruins) { 'qtd x unitário != total em:'; $ruins | ForEach-Object { "  - $($_.product.name)" } }
        $usados = @(Invoke-RestMethod "$base/api/purchases" -Headers $hdr) | ForEach-Object { $_.items.productId }
        $orfaos = $oldIds | Where-Object { $_ -notin $usados }
        if ($orfaos) { "Produtos órfãos (ids): $($orfaos -join ', ')" }
    }
}
```

Se a API estiver com a trava ligada (`ApiKey` configurada), definir `$env:SMARTGROCERY_API_KEY` antes de rodar o snippet. Sem isso, toda chamada volta com HTTP 401.

Se a saída for "API fora do ar", avisar como subir (README do projeto):
- Docker, dentro do WSL: `docker compose up -d` (API em `http://localhost:5000`)
- Sem Docker: `cd src/SmartGrocery.Api` e `dotnet run --launch-profile http` (API em `http://localhost:5170`)

### 2. Ler a saída e reportar

Responder curto, sem repetir a tabela inteira se ela já está na tela. Dizer:
- Se importou (loja, data, nº de itens) ou o motivo da falha:
  - HTTP 400: a URL não tem chave NFC-e válida de 44 dígitos.
  - HTTP 409: nota já importada (só acontece se a compra existente não foi achada pela chave).
  - HTTP 422: a consulta foi recusada ou o formato não é suportado. A mensagem da API diz o que faltou
    (emitente, data de emissão, valor a pagar, itens).
- Se total e soma dos itens batem. Diferença pode ser desconto ou acréscimo da nota, não
  necessariamente bug do parser.
- Itens onde quantidade x unitário não bate com o total. Costuma indicar erro de leitura de quantidade
  ou de unidade (ex.: peso lido como inteiro).
- Nomes de produto que parecem truncados ou estranhos (abreviações). Isso é assunto de normalização,
  não de reimportação. Só apontar.

### 3. Produtos órfãos

Se a saída trouxe "Produtos órfãos", mostrar os ids e os nomes (`GET /api/products/{id}`) e perguntar:
"Esses produtos ficaram da importação anterior e nenhuma compra usa mais. Quer que eu apague?"

Só com "sim":

```powershell
$base = '<a API que respondeu>'
$hdr = if ($env:SMARTGROCERY_API_KEY) { @{ 'X-Api-Key' = $env:SMARTGROCERY_API_KEY } } else { @{} }
foreach ($id in @(<ids confirmados>)) {
    try { Invoke-RestMethod -Method Delete "$base/api/products/$id" -Headers $hdr | Out-Null; "Produto $id apagado" }
    catch { "Produto $id não apagado: HTTP $([int]$_.Exception.Response.StatusCode)" }
}
```

## Depois de mudar código do parser

A API precisa subir de novo com o código novo antes de reimportar:
- Docker: `docker compose up --build -d api` (dentro do WSL)
- `dotnet run`: reiniciar a API (ou usar `dotnet watch run --launch-profile http`)

Sem isso, a reimportação roda o código antigo e o teste não prova nada.

## Dependências

- API e banco de pé (Docker no WSL ou `dotnet run`)
- PowerShell 5.1 ou superior
- Nenhum arquivo de `_memoria/` ou `identidade/`
