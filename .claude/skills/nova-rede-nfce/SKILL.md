---
name: nova-rede-nfce
description: >
  Guia a adaptação do leitor de NFC-e pra uma rede de mercado (ou estado) nova. Recebe a URL de uma
  nota, diagnostica em qual camada a leitura quebra (portal diferente, seletores diferentes, nomes
  abreviados) e decide se precisa mexer só no parser ou também no banco, antes de propor qualquer
  código. Use quando o usuário disser "nota de outro mercado", "rede nova", "o scanner não lê essa nota",
  "adaptar o leitor pra <mercado>", "suportar outro estado", "/nova-rede-nfce", ou colar uma URL de
  NFC-e que a API recusou.
---

# /nova-rede-nfce — Adaptar o leitor de NFC-e a uma rede nova

O leitor atual (`NfceDocumentReader`, em `src/SmartGrocery.Api/Services/`) foi feito e validado no
Assaí Sacomã/SP. Esta skill evita o retrabalho de sair refatorando o banco a cada rede nova.

**Princípio:** o leitor traduz a página da nota pro modelo normalizado `NfceDocument` /
`NfceDocumentItem`. O banco só enxerga esse modelo. **Rede nova mexe no leitor, não no banco.**
O banco só muda se a rede traz um dado que o modelo não tem onde guardar, e isso só com aprovação do usuário.

**Hipótese a confirmar com a nota real:** o layout da página da NFC-e tende a variar por SEFAZ
(estado), não por rede de mercado. Duas redes diferentes em SP passam pelo mesmo portal. Já uma
rede em outro estado cai num portal diferente. Confirmar isso na etapa 1 antes de concluir.

## Input

A URL completa do QR Code de uma nota da rede nova. Se o usuário não passou, perguntar:
"Cola a URL do QR Code de uma nota dessa rede (a URL inteira, como o QR gera)."

Uma nota só já basta pra diagnosticar. Pedir uma segunda da mesma rede na etapa 4, pra confirmar.

## Regras

- **Diagnosticar antes de codar.** Mostrar o diagnóstico e a proposta e esperar aprovação antes de editar `src/`.
- **Não quebrar o Assaí.** Qualquer mudança no leitor precisa passar pela reimportação de uma nota do Assaí (etapa 4).
- **Não gravar dicionário de abreviações por rede dentro do leitor.** Normalização de nomes é outro
  problema (roadmap, Fase Z) e tem que ficar separada da leitura da página.
- **Amostras ficam locais.** O HTML baixado vai pra `dados/nfce-samples/` (ignorado pelo Git). Pode
  conter CPF do consumidor e dados da compra. Nunca commitar, nunca colar em serviço externo.
- **Uma consulta por amostra.** Não ficar batendo no portal da SEFAZ em loop.
- Se o portal exigir captcha, login ou renderizar os itens só por JavaScript, parar e explicar que o
  leitor atual (HTTP GET + HTML) não resolve. Não tentar burlar captcha.

## Etapas

### 1. Decodificar a chave e comparar com o leitor

Extrair a chave de 44 dígitos da URL (`(?<!\d)\d{44}(?!\d)`) e decodificar:

| Posições | Campo |
|---|---|
| 1-2 | Código da UF |
| 3-6 | Ano e mês da emissão (AAMM) |
| 7-20 | CNPJ do emitente |
| 21-22 | Modelo (65 = NFC-e) |
| 23-25 | Série |
| 26-34 | Número da nota |
| 35 | Tipo de emissão |
| 36-43 | Código numérico |
| 44 | Dígito verificador |

Códigos de UF: 11 RO, 12 AC, 13 AM, 14 RR, 15 PA, 16 AP, 17 TO, 21 MA, 22 PI, 23 CE, 24 RN,
25 PB, 26 PE, 27 AL, 28 SE, 29 BA, 31 MG, 32 ES, 33 RJ, 35 SP, 41 PR, 42 SC, 43 RS, 50 MS,
51 MT, 52 GO, 53 DF.

Depois responder três perguntas:
1. **A UF é 35 (SP)?** Se não, o leitor atual recusa a nota antes de ler qualquer coisa: ele só aceita hosts `*.fazenda.sp.gov.br`.
2. **O host da URL termina em `.fazenda.sp.gov.br`?** Se sim e a leitura ainda falha, o problema é a nota específica, não o estado.
3. **O modelo é 65?** Se for 55, é NF-e comum, fora do escopo.

### 2. Rodar a leitura atual e baixar a amostra

Primeiro tentar a importação de verdade com `/reimportar-nfce <url>` e guardar a mensagem de erro
da API. Ela diz quais campos faltaram (emitente, data de emissão, valor a pagar, itens).

Depois baixar a página, do jeito que o leitor a vê:

```powershell
$url = '<URL do QR Code>'
New-Item -ItemType Directory -Force dados/nfce-samples | Out-Null
Invoke-WebRequest $url -UseBasicParsing -OutFile "dados/nfce-samples/<uf>-<cnpj>-<aaaamm>.html"
```

### 3. Comparar a estrutura com o que o leitor espera

Abrir o HTML salvo (ler só os trechos relevantes, a página é grande) e montar esta tabela, uma linha
por item:

| Dado | Como o leitor procura hoje (Assaí/SP) | O que a página nova tem |
|---|---|---|
| Emitente | elemento com `id="u20"` | |
| Data de emissão | texto `Emissão: dd/MM/yyyy HH:mm:ss` | |
| Total | `#totalNota`, label "Valor a pagar", `span.totalNumb` | |
| Linhas de itens | `table#tabResult` > `tr` | |
| Nome do item | `span.txtTit` | |
| Quantidade | `span.Rqtd` (texto com rótulo antes do `:`, que o leitor remove) | |
| Unidade | `span.RUN` (idem, rótulo removido) | |
| Valor unitário | `span.RvlUnit` (idem, rótulo removido) | |
| Valor do item | `span.valor` | |
| Código de barras | `span.RCod` (idem, só os dígitos são usados) | |
| Formato numérico | decimal com vírgula, cultura pt-BR | |

Se algum desses seletores mudou de lugar, marcar exatamente qual. Conferir também se a página
lista os itens no HTML inicial ou se depende de JavaScript.

### 4. Classificar e propor

Classificar o caso e mostrar o diagnóstico ao usuário, curto:

- **A. Mesmo portal, seletor diferente.** Ajuste local no leitor: um ou mais seletores viram
  alternativas, sem estrutura nova. Banco: não muda.
- **B. Portal de outro estado.** O leitor atual é único e tem a trava de host de SP. Proposta: extrair
  uma interface (ex.: `INfceDocumentReader`) e criar um leitor por portal, escolhido pela UF da chave
  ou pelo host, todos devolvendo `NfceDocument`. O `PurchasesController` continua igual. Banco: não muda.
- **C. Dados extras que o modelo não tem** (ex.: desconto por item, CNPJ do emitente, forma de
  pagamento). Só aqui entra mudança de banco. Apresentar como opção e perguntar. Sugestão a considerar:
  guardar o CNPJ do emitente (que já vem na chave) pra separar filiais de uma mesma rede.
- **D. Leitura ok, nomes abreviados.** Não é problema de leitor. Registrar como item de normalização
  e seguir sem mexer no parser.
- **E. Portal inacessível por GET simples** (captcha, JS, autenticação). Parar e explicar.

Terminar o diagnóstico com: "Proposta: <o que mudaria>. Banco: <muda / não muda>. Posso aplicar?"

### 5. Aplicar e validar (só depois do "sim")

1. Fazer a menor mudança que resolve. Não reformatar o resto do leitor.
2. Compilar: `dotnet build src/SmartGrocery.Api`.
3. Reiniciar a API com o código novo (Docker: `docker compose up --build -d api` dentro do WSL).
4. Rodar `/reimportar-nfce <url da rede nova>`. Conferir: itens > 0, total batendo com a soma
   (ou diferença explicável), sem itens com quantidade x unitário errado.
5. **Regressão:** rodar `/reimportar-nfce` com uma nota do Assaí (se houver amostra ou URL à mão).
   Se não houver nota do Assaí disponível, dizer isso claramente em vez de declarar que está tudo ok.
6. Pedir uma segunda nota da mesma rede e repetir o passo 4. Uma nota só pode passar por acaso
   (ex.: só tem itens vendidos por unidade e nenhum por peso).

### 6. Oferecer, uma vez, o teste sem depender da SEFAZ

Hoje o leitor busca a página e lê no mesmo método (`ReadAsync`). Com poucas notas reais, cada teste
depende da SEFAZ e da nota ainda estar consultável. Oferecer:

> "Dá pra separar 'buscar a página' de 'interpretar o HTML' e testar o parser com os HTMLs que
> ficaram em dados/nfce-samples/, sem ir na SEFAZ. Quer que eu faça isso?"

Só oferecer uma vez por sessão e só aplicar se o usuário aceitar. Se for feito, lembrar que amostras
com CPF não podem ir pro repositório: anonimizar antes de virarem fixture de teste.

## Resumo final

Fechar com:
- Rede/estado analisado e o caso (A a E)
- O que foi alterado, ou "nada alterado, aguardando aprovação"
- Se o Assaí foi retestado
- Pendências (normalização de nomes, segunda nota, decisão de banco)

## Dependências

- `/reimportar-nfce` (para testar)
- Arquivos: `src/SmartGrocery.Api/Services/NfceDocumentReader.cs`, `NfceAccessKeyExtractor.cs`,
  `src/SmartGrocery.Api/Controllers/PurchasesController.cs`
- Uma nota real da rede nova e API rodando local
- `_memoria/estrategia.md` para lembrar o foco atual (leitura multi-mercado)
