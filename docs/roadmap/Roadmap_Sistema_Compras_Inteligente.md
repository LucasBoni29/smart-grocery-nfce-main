# Sistema de Compras Inteligente por NFC-e

## Visão Geral

Objetivo: criar um assistente de compras domésticas que aprende os hábitos do casal, importa automaticamente compras via QR Code da NFC-e e monta sozinho a próxima lista de mercado.

---

# Estratégia de Evolução

```text
Fase X → Validar a ideia
Fase Y → Tornar útil no dia a dia
Fase Z → Criar o diferencial real
```

Regra principal:

- Primeiro resolver o problema.
- Depois melhorar a experiência.
- Só então adicionar IA avançada.

---

# FASE X - MVP

## Estratégia de Captura da NFC-e

Antes de desenvolver aplicativos nativos para iOS, o foco será validar o fluxo de captura e processamento das notas fiscais.

### Versão 0 - Prova de Conceito (1 dia)

Objetivo:

```text
Comprovar que uma NFC-e pode ser capturada e enviada para a API automaticamente.
```

Fluxo:

```text
iPhone ↓ Atalho do iOS ↓ Escanear QR Code ↓ Capturar URL da NFC-e ↓ Enviar para API ↓ Salvar no banco
```

Responsabilidades:

Atalho:

- Ler QR Code.
- Capturar URL da NFC-e.
- Enviar para API.

Backend:

- Receber URL.
- Extrair chave da nota.
- Processar NFC-e.
- Salvar dados.

Resultado esperado:

- ✅ Confirmar que a URL chega corretamente à API.
- ✅ Confirmar que a nota pode ser processada.
- ✅ Eliminar a principal incerteza técnica do projeto.

### Versão 1 - MVP Real (PWA)

Objetivo:

```text
Eliminar a dependência dos Atalhos.
```

Fluxo:

```text
PWA ↓ Usuário clica em "Escanear Nota" ↓ Câmera do iPhone abre ↓ QR Code lido pelo navegador ↓ URL enviada para API ↓ Nota processada ↓ Compra salva
```

Responsabilidades:

Frontend (PWA):

- Abrir câmera.
- Escanear QR Code.
- Capturar URL.
- Enviar para API.

Backend:

- Processar NFC-e.
- Extrair produtos.
- Registrar compra.

Bibliotecas sugeridas:

- `html5-qrcode`
- `ZXing`

Resultado esperado:

- ✅ Experiência semelhante a um aplicativo.
- ✅ Sem necessidade de Mac.
- ✅ Sem App Store.
- ✅ Funciona para Lucas e esposa.

### Versão 2 - Experiência Otimizada

Fluxo:

```text
Atalho do iOS OU PWA ↓ Escanear NFC-e ↓ API processa ↓ Histórico atualizado ↓ Lista do próximo mês atualizada
```

Resultado:

O usuário não precisa mais cadastrar produtos manualmente.

Toda compra passa a alimentar automaticamente o histórico da família.

### Futuro

Somente após validar o projeto por alguns meses será avaliada a criação de:

- Aplicativo iOS nativo em SwiftUI.
- Notificações push.
- Widgets para tela inicial.
- Integração completa com recursos nativos da Apple.

Até esse momento, a PWA será considerada o produto oficial.

Prazo: 2 finais de semana.

Objetivo:

```text
Parar de registrar compras manualmente.
```

## Stack

### Backend

```text
ASP.NET Core 8
```

### Banco

```text
PostgreSQL
```

### Frontend

```text
Next.js ou Blazor
```

### Hospedagem

```text
Railway
Render
Azure
```

## Funcionalidades

### Nova Compra

```text
Escanear QR Code
↓
Capturar URL da NFC-e
↓
Enviar para API
↓
Extrair itens
↓
Salvar compra
```

### Banco de Dados

#### purchases

```sql
id
purchase_date
market_name
total_value
created_at
```

#### products

```sql
id
name
normalized_name
category
```

#### purchase_items

```sql
id
purchase_id
product_id
quantity
unit_price
```

### Histórico

Exemplo:

```text
20/08/2026
Assaí
47 itens
R$ 855,25
```

## Resultado Esperado

- Compra registrada sem digitação.
- Histórico centralizado.
- Base de dados para futuras análises.

---

# FASE Y - Lista Inteligente

Prazo: mais 2 finais de semana.

Objetivo:

```text
Montar automaticamente a próxima lista de compras.
```

## Novas Tabelas

```sql
shopping_lists
shopping_list_items
```

## Regras Iniciais

Sem IA.

```text
Comprado nos últimos 3 meses?
→ sugerir
```

```text
Comprado em quase todas as compras?
→ sugerir
```

```text
Não comprado há mais de 60 dias?
→ remover da sugestão
```

## Household

```text
Casa Lucas
 ├─ Lucas
 └─ Esposa
```

## Dashboard

```text
Gasto médio mensal
Categorias
Histórico de compras
```

## Resultado Esperado

- Lista gerada automaticamente.
- Compartilhamento entre usuários.
- Estatísticas financeiras básicas.

---

# FASE Z - Assistente Doméstico Inteligente

Prazo: após 2 ou 3 meses de uso.

Objetivo:

```text
Transformar o sistema em um assistente de consumo familiar.
```

## IA para Normalização

Exemplos:

```text
LEITE ITALAC INT
LEITE TP INT
LEITE INT 1L
```

Resultado:

```text
Leite Integral Italac 1L
```

## Categorização Automática

```text
Café → Alimentação
Sabão → Limpeza
Shampoo → Higiene
```

## Previsão de Consumo

Exemplo:

```text
Consumo médio:
12 unidades de leite por mês
```

Sugestão automática:

```text
Adicionar 12 unidades na próxima compra
```

## Assistente Conversacional

Perguntas:

```text
O que devo comprar este mês?
Quanto gastamos com alimentação?
Qual produto mais aumentou de preço?
```

## Comparação Entre Mercados

```text
Assaí → média R$ 850
Atacadão → média R$ 790
```

## Previsão da Próxima Compra

```text
Valor estimado:
R$ 920
```

---

# Arquitetura Final

```text
iPhone XR
    ↓
PWA
    ↓
ASP.NET Core
    ↓
PostgreSQL
    ↓
OpenAI ou Gemini
```

---

# Cronograma Prático

## Final de Semana 1

- Criar banco.
- Criar API.
- Criar entidades.
- Salvar compras.

## Final de Semana 2

- Leitura do QR Code.
- Integração NFC-e.
- Histórico de compras.
- Deploy inicial.

## Final de Semana 3

- Lista automática.
- Household.
- Compartilhamento.

## Final de Semana 4

- Dashboard financeiro.
- Categorias.
- Métricas de consumo.

## Mês 2+

- IA.
- Chat com o histórico.
- Previsão de compras.
- Comparação de preços.
- Insights personalizados.

---

# Conclusão

A ordem ideal é:

```text
X → Importar compras automaticamente
Y → Gerar automaticamente a lista do próximo mês
Z → Criar um assistente doméstico inteligente
```

O diferencial do produto não será a IA no início. O principal ativo será o histórico de consumo gerado pelas NFC-es. Depois de alguns meses acumulando dados, a IA passa a gerar recomendações realmente úteis e personalizadas.
