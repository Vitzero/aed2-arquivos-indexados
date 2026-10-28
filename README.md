# AED II — Arquivos de dados com índices parciais

Aplicação de console em C# para estudar a organização **sequencial-indexada** de arquivos. O programa transforma eventos de uma loja virtual em arquivos binários de produtos e acessos, mantendo os registros ordenados pela chave.

O objetivo é implementar as operações diretamente nos arquivos, usando registros de tamanho fixo e `Seek`, sem carregar a base inteira na memória RAM.

## 1. Executar o projeto

É necessário ter o **SDK do .NET 10** instalado. Não é preciso instalar banco de dados nem pacotes NuGet adicionais.

Na pasta que contém `faculCsharp.csproj`, execute:

```powershell
dotnet run
```

Para apenas compilar:

```powershell
dotnet build
```

O projeto acompanha uma amostra pronta em `dados/`. Não é necessário importar o CSV para começar a usar o menu.

```text
1 - Importar dados iniciais do CSV
2 - Mostrar registros
3 - Buscar por ID diretamente no arquivo
4 - Buscar por ID usando o índice
5 - Inserir registro
6 - Remover registro
7 - Consultas por produto
8 - Esvaziar a base (todos os produtos e acessos)
0 - Sair
```

Nas operações de exibição, busca, inserção e remoção, o programa pergunta se a operação será em **produtos (1)** ou **acessos (2)**. As consultas da opção 7 recebem o ID de um produto.

O console usa UTF-8 para preservar acentos. Na inserção manual de preços, use o separador decimal configurado no sistema: em um sistema brasileiro, por exemplo, `35,79`. A leitura do CSV trata separadamente o ponto decimal utilizado na fonte.

## 2. Contexto e dados utilizados

A fonte é o dataset [eCommerce behavior data from multi category store](https://www.kaggle.com/datasets/mkechinov/ecommerce-behavior-data-from-multi-category-store).

Cada linha do CSV descreve um evento de um usuário relacionado a um produto. Os eventos utilizados são:

| Valor | Significado |
|---|---|
| `view` | Visualização de um produto |
| `cart` | Evento de adição ao carrinho |
| `purchase` | Evento de compra |

A base que acompanha o projeto foi gerada com os **primeiros 10.000 eventos de cada um dos dois CSVs**, sem contar os cabeçalhos. Outubro é importado primeiro; novembro vem depois:

| CSV utilizado | Eventos importados | Visualizações | Carrinho | Compras |
|---|---:|---:|---:|---:|
| `2019-Oct.csv` | 10.000 | 9.785 | 97 | 118 |
| `2019-Nov.csv` | 10.000 | 9.805 | 93 | 102 |

| Informação | Quantidade |
|---|---:|
| Produtos únicos entre os dois meses | 7.510 |
| Acessos/eventos | 20.000 |
| Visualizações | 19.590 |
| Eventos de carrinho | 190 |
| Eventos de compra | 220 |

Os resultados das consultas representam esses dois recortes, e não todos os dados de outubro e novembro ou do dataset completo. Produtos repetidos entre os meses aparecem uma única vez no cadastro.

Os CSVs originais ficam fora do repositório por seu tamanho. A pasta `dados/` contém os arquivos binários e índices já gerados a partir deles.

## 3. Organização do projeto

```text
faculCsharp/
|-- Program.cs              Menu e interação com o usuário
|-- Produto.cs              Campos de um produto
|-- Acesso.cs               Campos de um evento
|-- ArquivoProdutos.cs      Leitura, gravação, busca, inserção e remoção de produtos
|-- ArquivoAcessos.cs       As mesmas operações para acessos
|-- IndiceProdutos.cs       Criação, conferência e busca no índice de produtos
|-- IndiceAcessos.cs        Criação, conferência e busca no índice de acessos
|-- LeitorCsv.cs            Conversão e importação das linhas do CSV
|-- Consultas.cs            Contagens de acessos e compras por produto
|-- faculCsharp.csproj      Configuração do projeto .NET
|-- README.md
|-- .gitignore
`-- dados/
    |-- produtos.bin
    |-- acessos.bin
    |-- produtos.idx
    `-- acessos.idx
```

Durante o desenvolvimento, o programa encontra a raiz do projeto a partir da pasta do executável e usa `dados/` nessa raiz. Dessa forma, os dados não dependem da pasta de onde o terminal foi aberto.

Ao publicar com `dotnet publish -c Release`, o projeto copia os quatro arquivos de dados e índices para `dados/` junto da aplicação publicada. Nessa versão, o programa usa a pasta ao lado do executável. `bin/`, `obj/` e os CSVs não devem entrar no repositório; os quatro arquivos de `dados/` devem entrar.

## 4. Por que há arquivos separados?

O cadastro do produto e o histórico de eventos são informações diferentes. Um produto aparece uma vez em `produtos.bin`, mas pode estar relacionado a muitos registros de `acessos.bin`.

```text
produtos.bin                acessos.bin
+-------------+             +----+-----------+----------+
| Id: 100     |             | Id | ProdutoId | Evento   |
| Marca: ABC  |             +----+-----------+----------+
| Preço: 50   | <---------- | 1  | 100       | view     |
+-------------+ <---------- | 2  | 100       | cart     |
                <---------- | 3  | 100       | purchase |
                            +----+-----------+----------+
```

O desenho é ilustrativo: as setas representam a relação pelo identificador, não ponteiros gravados no arquivo.

| Arquivo | Conteúdo | Chave/ordenação |
|---|---|---|
| `produtos.bin` | Cadastro dos produtos sem IDs repetidos | `Produto.Id` |
| `acessos.bin` | Histórico de eventos | `Acesso.Id` |
| `produtos.idx` | Algumas chaves e posições de produtos | ID do produto |
| `acessos.idx` | Algumas chaves e posições de acessos | ID do acesso |

**O índice de acessos usa `Acesso.Id`, não `ProdutoId`.** No arquivo de acessos, `ProdutoId` pode se repetir. Não há um terceiro índice por produto.

## 5. Organização sequencial-indexada

Neste projeto, “sequencial-indexado” descreve a combinação de duas estruturas:

1. Um **arquivo de dados sequencial e ordenado**, em que os registros estão fisicamente em ordem crescente de chave.
2. Um **índice parcial**, armazenado em outro arquivo, que indica o início de alguns blocos do arquivo de dados.

```text
Índice parcial                Arquivo de dados ordenado

chave -> posição              posição 0  [registro]
primeira do bloco 0 -> 0  ---> ...        [registro]
primeira do bloco 1 -> 10 ---> posição 10 [registro]
primeira do bloco 2 -> 20 ---> posição 20 [registro]
```

Portanto, não há um arquivo adicional chamado “índice sequencial”. Os dois arquivos `.idx` são os índices parciais da organização sequencial-indexada. Os registros dentro de cada `.idx` também ficam ordenados pelas chaves que representam.

## 6. Armazenamento binário e registros de tamanho fixo

Os arquivos não guardam linhas de texto com números separados por vírgulas. `BinaryWriter` grava os valores numéricos em formato binário; os textos são convertidos em bytes UTF-8.

Por exemplo, o valor inteiro `100` é gravado como um `long` de 8 bytes. Ele não é convertido para o texto `"100"`.

### Produto: 175 bytes

As posições em bytes são contadas a partir de zero dentro de cada registro.

| Campo | Tipo/formato | Bytes | Posições |
|---|---|---:|---|
| `Id` | `long` | 8 | 0 a 7 |
| `Categoria` | Texto UTF-8 com espaços ao final | 100 | 8 a 107 |
| `Marca` | Texto UTF-8 com espaços ao final | 50 | 108 a 157 |
| `Preco` | `decimal` | 16 | 158 a 173 |
| Terminador | Byte `\n` | 1 | 174 |
| **Total** | | **175** | |

```text
| Id: 8 | Categoria: 100 | Marca: 50 | Preço: 16 | \n: 1 |
<---------------------- 175 bytes --------------------->
```

### Acesso: 56 bytes

| Campo | Tipo/formato | Bytes | Posições |
|---|---|---:|---|
| `Id` | `long` | 8 | 0 a 7 |
| `ProdutoId` | `long` | 8 | 8 a 15 |
| `UsuarioId` | `long` | 8 | 16 a 23 |
| `TipoEvento` | Texto UTF-8 com espaços ao final | 8 | 24 a 31 |
| `DataHora` | Texto UTF-8 com espaços ao final | 23 | 32 a 54 |
| Terminador | Byte `\n` | 1 | 55 |
| **Total** | | **56** | |

O evento mais longo aceito no menu é `purchase`, com 8 bytes. A data segue o formato `2019-10-01 00:00:00 UTC`, com 23 bytes.

### Como os textos mantêm o tamanho fixo

`PrepararTexto` converte o texto para bytes, verifica o limite e completa o espaço restante com o byte de espaço (`0x20`). Exemplo reduzido para um campo de 8 bytes:

```text
Texto:       view
Gravação:    [v][i][e][w][ ][ ][ ][ ]
Quantidade:   1  2  3  4  5  6  7  8 bytes
```

Caracteres e bytes não são sempre a mesma coisa: em UTF-8, uma letra acentuada pode ocupar mais de um byte. Por isso, o limite é conferido depois da conversão com `Encoding.UTF8.GetBytes`. Textos acima do limite são recusados, sem truncamento.

Na leitura, `ReadBytes` lê a quantidade reservada, `Encoding.UTF8.GetString` converte para texto e `TrimEnd(' ')` retira os espaços de preenchimento. Por essa regra, espaços finais originais também não são preservados.

O código grava os textos como `byte[]`, evitando o prefixo de comprimento que seria escrito por `BinaryWriter.Write(string)`.

Cada registro termina com um byte `\n`, mas a leitura usa o tamanho fixo. **Não se deve separar um arquivo binário por quebras de linha**, pois um valor numérico também pode conter um byte com o mesmo valor do terminador.

## 7. ID, posição do registro e endereço em bytes

Esses três conceitos têm funções diferentes:

| Conceito | Exemplo | Uso |
|---|---|---|
| ID | Produto `44600062` | Identifica o registro |
| Posição | Registro de número `10` | Indica sua ordem física, começando em zero |
| Endereço em bytes | `10 × 175 = 1750` | Indica onde começa o registro no arquivo |

```text
produtos.bin

Posição do registro:   0          1          2          ... 10
Início em bytes:       0          175        350        ... 1750
                      | produto  | produto  | produto  | ... | produto |
```

A fórmula é:

```text
endereço em bytes = posição do registro × tamanho do registro
```

Para acessar a posição 10 dos produtos:

```csharp
arquivo.Seek(10 * TamanhoRegistro, SeekOrigin.Begin);
```

O `Seek` reposiciona o cursor do arquivo. Não precisamos ler os dez registros anteriores para alcançar esse endereço. A posição do registro não pode ser calculada diretamente pelo ID: IDs podem ter intervalos e lacunas.

O número de registros também não exige uma leitura completa:

```text
quantidade de produtos = tamanho de produtos.bin / 175
quantidade de acessos  = tamanho de acessos.bin / 56
```

Se houver resto nessa divisão, o código identifica um arquivo com registro incompleto.

## 8. Como o programa usa a RAM

O processamento usa memória para os objetos e buffers necessários à operação atual. Não há uma lista contendo todos os produtos ou acessos no programa.

```text
DISCO                                 RAM

produtos.bin [milhares de registros] --Seek--> [um produto lido]
acessos.bin  [milhares de registros] --Seek--> [um acesso lido]
produtos.idx [entradas de 16 bytes]  --------> [chave e posição atuais]
```

- A importação lê uma linha do CSV por vez com `TextFieldParser`.
- A leitura de dados materializa um registro por chamada de `Ler`.
- `PrepararRegistro` usa um `MemoryStream` temporário contendo somente um registro: 175 ou 56 bytes.
- Inserção e remoção usam um buffer de um registro para deslocar os dados.
- A busca no índice lê uma entrada por comparação.
- As consultas de contagem percorrem os acessos um por vez, mantendo um contador.

Além disso, .NET e sistema operacional podem usar buffers e cache de arquivo. A regra da implementação é não carregar a base completa em uma coleção da aplicação.

O `using` fecha e libera os recursos dos streams ao terminar a função.

## 9. Pesquisa binária diretamente nos dados

A pesquisa binária depende da ordenação das chaves. A cada rodada, lê o registro do meio do intervalo e escolhe em qual metade continuar.

Exemplo ilustrativo: procurar o ID `60`.

```text
IDs ordenados:  10  20  30  [40]  50  60  70
Procurado: 60 > 40
Descartados:    10  20  30   40

Restaram:      50 [60] 70
Encontrado!
```

O código calcula o meio e chama `Ler`, que faz `Seek` no arquivo. `inicio = meio + 1` e `fim = meio - 1` excluem a metade já descartada; não significam percorrer todo o arquivo de um em um.

Em um arquivo de um milhão de registros, são necessárias no máximo cerca de 20 rodadas. Se a chave não existir, a pesquisa termina quando `inicio > fim`, retornando `null`.

## 10. Índices parciais

Cada índice tem **um nível** e uma entrada para cada bloco de **10 registros**. É parcial porque não possui uma entrada para cada registro de dados.

Cada entrada ocupa 16 bytes:

| Campo | Tipo | Bytes |
|---|---|---:|
| Chave do primeiro registro do bloco | `long` | 8 |
| Posição desse registro no arquivo de dados | `long` | 8 |

As entradas não têm o terminador dos registros de dados. O endereço de uma entrada no índice é `número da entrada × 16`.

Na amostra entregue:

| Arquivo | Registros/entradas | Tamanho |
|---|---:|---:|
| `produtos.bin` | 7.510 | 1.314.250 bytes |
| `acessos.bin` | 20.000 | 1.120.000 bytes |
| `produtos.idx` | 751 | 12.016 bytes |
| `acessos.idx` | 2.000 | 32.000 bytes |

Os 7.510 produtos formam 751 blocos completos. Se a quantidade não for múltipla de 10, o último bloco pode ter menos registros. Um bloco é um agrupamento lógico de registros; não há cabeçalho de bloco inserido no `.bin`.

### Consulta pelo índice

Exemplo com chaves ilustrativas:

```text
ÍNDICE                       ARQUIVO DE PRODUTOS
chave | posição              posições
100   | 0  ----------------> 0 a 9
300   | 10 ----------------> 10 a 19
500   | 20 ----------------> 20 a 29
```

Para procurar o ID `350`:

1. Fazemos busca binária **no arquivo de índice**.
2. Escolhemos a maior chave que não ultrapassa 350: nesse exemplo, 300.
3. A entrada aponta para a posição 10 do arquivo de produtos.
4. `Ler` usa `Seek` e procura nas posições 10 a 19, lendo até 10 produtos.
5. A busca para ao encontrar o ID, ultrapassá-lo ou terminar o bloco.

```text
ID 350 -> busca binária em produtos.idx
       -> entrada 300 / posição 10
       -> Seek para byte 1750 de produtos.bin
       -> leitura do bloco -> produto ou null
```

No índice de acessos, os IDs da importação começam em 1, enquanto as posições começam em zero:

```text
Id do acesso | posição
1            | 0
11           | 10
21           | 20
```

## 11. Importação inicial

A opção 1 aceita um ou dois CSVs e uma quantidade máxima de eventos **por CSV**. Ela exige os dois arquivos de dados vazios, para não sobrescrever uma base existente. Como a entrega já contém dados, não é necessário executar essa opção.

Para experimentar uma importação do zero, prefira uma cópia de teste do projeto. Escolha a opção 8 para esvaziar a base e digite `APAGAR` para confirmar. Ela remove todos os produtos e acessos, inclusive o histórico, e recria os índices vazios. Qualquer outra resposta cancela sem alterar os dados. Os CSVs originais não são modificados.

Depois escolha a opção 1, a quantidade de CSVs, os caminhos e o limite por arquivo, por exemplo `10000`. Os arquivos são processados na ordem informada; o mesmo caminho não pode ser informado duas vezes. Diferentemente da opção 6, que remove um registro por ID, a opção 8 esvazia os dois arquivos de dados por completo.

```text
CSV 1: 2019-Oct.csv -> acessos 1 até 10.000
CSV 2: 2019-Nov.csv -> acessos 10.001 até 20.000
                      |
                      `--> mesmo acessos.bin e mesmo produtos.bin
```

O contador de ID do acesso é iniciado apenas uma vez, antes de percorrer os CSVs. Já o contador do limite de eventos reinicia para cada arquivo. Assim, os IDs dos acessos não se repetem entre os meses.

O processo é:

```text
Uma linha do CSV
       |
       +--> Produto -> inserir em ordem se o ID ainda não existir
       |
       `--> Acesso  -> acrescentar com um novo ID sequencial
```

Produtos repetidos mantêm os campos da primeira ocorrência, inclusive o preço. Isso também vale quando o mesmo produto aparece em outubro e novembro: a ocorrência de outubro prevalece, pois esse CSV foi importado primeiro. Cada evento gera um acesso, mesmo que produto ou usuário já tenham aparecido. Categoria e marca vazias são aceitas quando a fonte não fornece esses valores.

Os campos `category_id` e `user_session` não são armazenados; a categoria utilizada é o texto de `category_code`.

Durante a importação, o índice não é reconstruído a cada linha. O menu reconstrói os dois índices ao terminar a tentativa de importação, inclusive se uma falha deixar registros parcialmente importados.

## 12. Inserção e manutenção da ordem

A função `Inserir` usa busca binária para encontrar a posição correta. Se o ID já existir, retorna `false` e não duplica nem atualiza o registro.

Para inserir o ID 20 entre 10 e 30:

```text
Posição:       0    1    2    3
Antes:        [10] [30] [40]

Move 40:      [10] [30] [40] [40]
Move 30:      [10] [30] [30] [40]
Grava 20:     [10] [20] [30] [40]
```

As cópias acontecem **do final para o começo**, para não sobrescrever um registro que ainda precisa ser movido. O buffer contém apenas os bytes de um registro.

O programa não desordena e depois ordena o arquivo inteiro: ele mantém a parte anterior e desloca somente a parte que precisa abrir espaço. No fim do arquivo, não é necessário deslocar registros. No começo, todos os existentes precisam ser movidos.

`Acrescentar` é uma operação mais restrita: grava no final e exige que o novo ID seja maior que o último. Ela é utilizada para os IDs sequenciais de acessos durante a importação. `Inserir` aceita também posições intermediárias.

## 13. Remoção e manutenção da ordem

A função `Remover` encontra o registro por busca binária. Se o ID não existir, retorna `false`.

Ao remover o ID 20:

```text
Antes:        [10] [20] [30] [40]
Move 30:      [10] [30] [30] [40]
Move 40:      [10] [30] [40] [40]
Corta final:  [10] [30] [40]
```

As cópias vão **da esquerda para a direita**, preenchendo a posição removida. Depois, `SetLength` reduz o arquivo em um registro:

```csharp
arquivo.SetLength((quantidade - 1) * TamanhoRegistro);
```

Os IDs restantes não são renumerados. Se o acesso 50 for removido, o 51 continua sendo o 51, mesmo que sua posição física mude.

Remover um produto **não remove os acessos relacionados**. Preservamos o histórico; portanto, um acesso pode apontar para um ID de produto que já não está cadastrado. A inserção manual também não exige que o produto referido pelo acesso exista.

## 14. Por que reconstruir os índices?

Inserir ou remover desloca registros e pode mudar tanto a posição de uma chave quanto a chave que inicia cada bloco.

```text
Antes da inserção:
posição 10 -> produto 500
índice: 500 -> posição 10

Depois de inserir o produto 450 nessa posição:
posição 10 -> produto 450
posição 11 -> produto 500

Índice antigo: 500 -> posição 10   (desatualizado)
Índice novo:   450 -> posição 10   (correto)
```

Por simplicidade, as operações individuais do menu reconstroem todo o índice correspondente após uma alteração bem-sucedida. A reconstrução lê as posições 0, 10, 20 e assim por diante e regrava as entradas.

Há sobrecargas das funções com e sem o caminho do índice:

```csharp
// Só altera o arquivo de dados: usada na carga inicial em lote.
ArquivoProdutos.Inserir(caminho, produto);

// Altera os dados e, se inseriu, reconstrói o índice: usada no menu.
ArquivoProdutos.Inserir(caminho, produto, caminhoIndice);
```

Não há reconstrução por alteração quando um ID duplicado é recusado ou uma remoção não encontra o ID.

### Abertura e encerramento

Ao iniciar, o programa cria os arquivos de dados que ainda não existem, sem apagar os existentes. Para cada índice:

- Se não existir, cria.
- Se existir, `Carregar` lê e confere o tamanho, as posições e as chaves de início dos blocos.
- Se a conferência indicar índice inválido ou desatualizado, reconstrói.

Neste projeto, carregar significa ler e conferir o índice salvo. As entradas não ficam mantidas em uma lista na RAM; cada consulta volta a pesquisar no arquivo `.idx`.

Ao sair pela opção 0, os dois índices são novamente gravados a partir dos dados atuais.

## 15. Perguntas e consultas

As duas perguntas escolhidas são:

1. **Quantos acessos determinado produto recebeu?**
2. **Quantos eventos de compra determinado produto recebeu?**

As duas consultas percorrem `acessos.bin`, um registro por vez. A primeira compara `ProdutoId`; a segunda exige também `TipoEvento == "purchase"`.

```csharp
if (acesso.ProdutoId == produtoId && acesso.TipoEvento == "purchase")
    total++;
```

O índice de acessos não resolve essas contagens diretamente porque está organizado pelo **ID do acesso**, e eventos de um mesmo produto podem aparecer em qualquer parte do arquivo.

Um usuário pode gerar vários eventos. Portanto, a contagem de acessos não representa usuários únicos. A contagem de compras representa eventos `purchase`, não uma quantidade de unidades informada em outro campo.

### Exemplo real para demonstrar no menu

Na amostra original entregue, o produto `1004856` apresenta:

```text
Opção: 7
ID do produto: 1004856
Acessos: 234
Compras: 10
```

Para comparar as buscas do produto, escolha a opção 3 e depois a opção 4, selecionando produtos e informando o mesmo ID. Os campos retornados devem ser iguais.

Esses resultados podem mudar se a base for modificada pelo menu.

## 16. Limitações e decisões de simplicidade

- A base entregue contém amostras de outubro e novembro; os CSVs completos não foram importados.
- A importação inicial recebe um ou dois CSVs e um limite por arquivo. O menu junta os arquivos em uma base inicialmente vazia, mas não continua uma importação em uma base já preenchida.
- Inserções e remoções podem deslocar muitos registros. A importação de milhões de eventos por essa estratégia será lenta, embora use pouca memória.
- Reconstruir todo o índice após cada alteração individual é simples, mas acrescenta trabalho. Durante a importação, a reconstrução é feita ao terminar.
- Não há banco de dados, árvore B, área de overflow, exclusão lógica ou mecanismo de transação. Uma interrupção durante um deslocamento pode deixar a operação incompleta.
- Os limites dos campos de texto são em bytes. Um valor maior é recusado, em vez de aumentar o registro.
- As buscas dependem de os arquivos serem mantidos ordenados pelas funções do programa. A conferência inicial do índice não é uma validação completa de cada byte dos dados.

## 17. Verificação realizada

O projeto foi compilado sem avisos ou erros e testado em cópias separadas dos arquivos de entrega. Foram conferidos:

- Na primeira versão da amostra, todos os campos dos 4.148 produtos e dos 10.000 acessos de outubro contra o CSV, incluindo preços e manutenção da primeira ocorrência do produto.
- Tamanhos fixos, terminadores, chaves únicas, ordenação e entradas dos dois índices.
- 28.296 buscas de IDs existentes, somando busca direta e por índice, além de IDs ausentes.
- Inserções e remoções no começo, meio e fim; IDs duplicados; textos acentuados.
- Contagens de acessos e compras, exibição parcial e persistência ao reabrir.
- Arquivos vazios, remoção do único registro e reconstrução de índices ausentes ou desatualizados.
- Nova importação dos 10.000 eventos de outubro, produzindo arquivos idênticos aos daquela amostra.

A ampliação da amostra combina outubro e novembro: são 7.510 produtos únicos e 20.000 acessos. A conferência dessa base verifica os campos contra os dois CSVs, a continuidade dos IDs entre os meses, os produtos repetidos e os índices reconstruídos.

Os scripts usados na conferência ficaram fora do código entregue, para manter o projeto acadêmico simples.
