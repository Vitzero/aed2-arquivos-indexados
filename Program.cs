// Mantém a mesma codificação nos textos digitados e mostrados no console.
Console.InputEncoding = System.Text.Encoding.UTF8;
Console.OutputEncoding = System.Text.Encoding.UTF8;

// Durante o desenvolvimento, usa dados/ na raiz do projeto.
// Na versão publicada, usa dados/ ao lado do executável.
DirectoryInfo? pastaProjeto = new DirectoryInfo(AppContext.BaseDirectory);
while (pastaProjeto != null && !File.Exists(Path.Combine(pastaProjeto.FullName, "faculCsharp.csproj")))
    pastaProjeto = pastaProjeto.Parent;

string pastaDados = Path.Combine(pastaProjeto?.FullName ?? AppContext.BaseDirectory, "dados");
Directory.CreateDirectory(pastaDados);
string produtos = Path.Combine(pastaDados, "produtos.bin");
string acessos = Path.Combine(pastaDados, "acessos.bin");
string indiceProdutos = Path.Combine(pastaDados, "produtos.idx");
string indiceAcessos = Path.Combine(pastaDados, "acessos.idx");

// OpenOrCreate cria somente o que ainda não existe, sem apagar os dados.
using (FileStream arquivo = new FileStream(produtos, FileMode.OpenOrCreate)) { }
using (FileStream arquivo = new FileStream(acessos, FileMode.OpenOrCreate)) { }
PrepararIndices();

bool continuar = true;
while (continuar)
{
    Console.WriteLine();
    Console.WriteLine("1 - Importar dados iniciais do CSV");
    Console.WriteLine("2 - Mostrar registros");
    Console.WriteLine("3 - Buscar por ID diretamente no arquivo");
    Console.WriteLine("4 - Buscar por ID usando o índice");
    Console.WriteLine("5 - Inserir registro");
    Console.WriteLine("6 - Remover registro");
    Console.WriteLine("7 - Consultas por produto");
    Console.WriteLine("8 - Esvaziar a base (todos os produtos e acessos)");
    Console.WriteLine("0 - Sair");
    Console.Write("Opção: ");
    string opcao = Console.ReadLine() ?? "0";

    try
    {
        switch (opcao)
        {
            case "1":
                Importar();
                break;
            case "2":
                Mostrar();
                break;
            case "3":
                Buscar(false);
                break;
            case "4":
                Buscar(true);
                break;
            case "5":
                Inserir();
                break;
            case "6":
                Remover();
                break;
            case "7":
                Consultar();
                break;
            case "8":
                EsvaziarBase();
                break;
            case "0":
                IndiceProdutos.Criar(produtos, indiceProdutos);
                IndiceAcessos.Criar(acessos, indiceAcessos);
                Console.WriteLine("Índices salvos. Programa encerrado.");
                continuar = false;
                break;
            default:
                Console.WriteLine("Opção inválida.");
                break;
        }
    }
    catch (Exception erro) when (erro is IOException || erro is ArgumentException || erro is FormatException)
    {
        Console.WriteLine($"Não foi possível concluir: {erro.Message}");
    }
}

void PrepararIndices()
{
    if (!File.Exists(indiceProdutos))
        IndiceProdutos.Criar(produtos, indiceProdutos);
    else
    {
        try
        {
            IndiceProdutos.Carregar(produtos, indiceProdutos);
        }
        catch (InvalidDataException)
        {
            Console.WriteLine("Reconstruindo o índice de produtos desatualizado.");
            IndiceProdutos.Criar(produtos, indiceProdutos);
        }
    }

    if (!File.Exists(indiceAcessos))
        IndiceAcessos.Criar(acessos, indiceAcessos);
    else
    {
        try
        {
            IndiceAcessos.Carregar(acessos, indiceAcessos);
        }
        catch (InvalidDataException)
        {
            Console.WriteLine("Reconstruindo o índice de acessos desatualizado.");
            IndiceAcessos.Criar(acessos, indiceAcessos);
        }
    }

    Console.WriteLine("Índices preparados. Os arquivos de dados foram preservados.");
}

void Importar()
{
    if (ArquivoProdutos.Contar(produtos) > 0 || ArquivoAcessos.Contar(acessos) > 0)
    {
        Console.WriteLine("A importação inicial exige os dois arquivos vazios. Os dados atuais foram preservados.");
        return;
    }

    long quantidadeCsv = LerNumero("Quantos CSVs importar (1 ou 2)? ");
    if (quantidadeCsv > 2)
        throw new ArgumentException("Informe 1 ou 2 CSVs.");

    string[] caminhosCsv = new string[(int)quantidadeCsv];
    for (int i = 0; i < caminhosCsv.Length; i++)
    {
        Console.Write($"Caminho do CSV {i + 1}: ");
        caminhosCsv[i] = Console.ReadLine() ?? "";
    }

    long limite = LerNumero("Quantidade máxima de eventos por CSV (exemplo: 10000): ");
    if (limite > int.MaxValue)
        throw new ArgumentException("Quantidade acima do limite permitido.");

    try
    {
        LeitorCsv.Importar(caminhosCsv, produtos, acessos, (int)limite);
    }
    finally
    {
        // Mesmo uma importação interrompida pode ter gravado alguns registros.
        IndiceProdutos.Criar(produtos, indiceProdutos);
        IndiceAcessos.Criar(acessos, indiceAcessos);
    }

    Console.WriteLine($"Produtos: {ArquivoProdutos.Contar(produtos)}");
    Console.WriteLine($"Acessos: {ArquivoAcessos.Contar(acessos)}");
}

void Mostrar()
{
    string tipo = EscolherArquivo();
    long limite = LerNumero("Quantos registros mostrar? ");
    long quantidade = tipo == "1" ? ArquivoProdutos.Contar(produtos) : ArquivoAcessos.Contar(acessos);
    Console.WriteLine($"Total de registros: {quantidade}");

    for (long posicao = 0; posicao < Math.Min(limite, quantidade); posicao++)
    {
        if (tipo == "1")
            MostrarProduto(ArquivoProdutos.Ler(produtos, posicao));
        else
            MostrarAcesso(ArquivoAcessos.Ler(acessos, posicao));
    }
}

void Buscar(bool usarIndice)
{
    string tipo = EscolherArquivo();
    long id = LerNumero("ID procurado: ");

    if (tipo == "1")
    {
        Produto? produto = usarIndice
            ? IndiceProdutos.BuscarPorId(produtos, indiceProdutos, id)
            : ArquivoProdutos.BuscarPorId(produtos, id);
        if (produto == null)
            Console.WriteLine("Produto não encontrado.");
        else
            MostrarProduto(produto);
    }
    else
    {
        Acesso? acesso = usarIndice
            ? IndiceAcessos.BuscarPorId(acessos, indiceAcessos, id)
            : ArquivoAcessos.BuscarPorId(acessos, id);
        if (acesso == null)
            Console.WriteLine("Acesso não encontrado.");
        else
            MostrarAcesso(acesso);
    }
}

void Inserir()
{
    string tipo = EscolherArquivo();
    bool inserido;

    if (tipo == "1")
    {
        Produto produto = new Produto();
        produto.Id = LerNumero("ID do produto: ");
        Console.Write("Categoria: ");
        produto.Categoria = Console.ReadLine() ?? "";
        Console.Write("Marca: ");
        produto.Marca = Console.ReadLine() ?? "";
        Console.Write("Preço (use o separador decimal do seu sistema, por exemplo 35,79): ");
        if (!decimal.TryParse(Console.ReadLine(), out produto.Preco) || produto.Preco < 0)
            throw new ArgumentException("Preço inválido.");

        inserido = ArquivoProdutos.Inserir(produtos, produto, indiceProdutos);
    }
    else
    {
        Acesso acesso = new Acesso();
        acesso.Id = LerNumero("ID do acesso: ");
        acesso.ProdutoId = LerNumero("ID do produto: ");
        acesso.UsuarioId = LerNumero("ID do usuário: ");
        Console.Write("Evento (view, cart ou purchase): ");
        acesso.TipoEvento = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
        if (acesso.TipoEvento != "view" && acesso.TipoEvento != "cart" && acesso.TipoEvento != "purchase")
            throw new ArgumentException("Tipo de evento inválido.");

        Console.Write("Data e hora UTC (exemplo: 2019-10-01 00:00:00 UTC): ");
        acesso.DataHora = Console.ReadLine() ?? "";
        if (!DateTime.TryParseExact(acesso.DataHora, "yyyy-MM-dd HH:mm:ss 'UTC'",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out _))
            throw new ArgumentException("Data e hora inválidas.");

        inserido = ArquivoAcessos.Inserir(acessos, acesso, indiceAcessos);
    }

    Console.WriteLine(inserido ? "Registro inserido e índice atualizado." : "Esse ID já existe.");
}

void Remover()
{
    string tipo = EscolherArquivo();
    long id = LerNumero("ID para remover: ");
    bool removido = tipo == "1"
        ? ArquivoProdutos.Remover(produtos, id, indiceProdutos)
        : ArquivoAcessos.Remover(acessos, id, indiceAcessos);
    Console.WriteLine(removido ? "Registro removido e índice atualizado." : "ID não encontrado.");
    if (removido && tipo == "1")
        Console.WriteLine("O histórico de acessos foi preservado.");
}

void EsvaziarBase()
{
    Console.WriteLine($"Serão apagados {ArquivoProdutos.Contar(produtos)} produtos e {ArquivoAcessos.Contar(acessos)} acessos.");
    Console.WriteLine("Esta operação apaga todos os registros da base, inclusive o histórico de acessos.");
    Console.Write("Digite APAGAR para confirmar, ou qualquer outra coisa para cancelar: ");
    if (Console.ReadLine() != "APAGAR")
    {
        Console.WriteLine("Operação cancelada. Os dados foram preservados.");
        return;
    }

    // Create mantém os arquivos, mas reduz o conteúdo a zero bytes.
    using (FileStream arquivo = new FileStream(produtos, FileMode.Create)) { }
    using (FileStream arquivo = new FileStream(acessos, FileMode.Create)) { }
    IndiceProdutos.Criar(produtos, indiceProdutos);
    IndiceAcessos.Criar(acessos, indiceAcessos);
    Console.WriteLine("Base esvaziada e índices recriados. Use a opção 1 para importar novamente.");
}

void Consultar()
{
    long id = LerNumero("ID do produto: ");
    Console.WriteLine($"Acessos: {Consultas.ContarAcessosPorProduto(acessos, id)}");
    Console.WriteLine($"Compras: {Consultas.ContarComprasPorProduto(acessos, id)}");
}

string EscolherArquivo()
{
    Console.Write("Arquivo (1 - Produtos, 2 - Acessos): ");
    string tipo = Console.ReadLine() ?? "";
    if (tipo != "1" && tipo != "2")
        throw new ArgumentException("Escolha 1 ou 2.");
    return tipo;
}

long LerNumero(string mensagem)
{
    Console.Write(mensagem);
    if (!long.TryParse(Console.ReadLine(), out long numero) || numero <= 0)
        throw new ArgumentException("Digite um número inteiro maior que zero.");
    return numero;
}

void MostrarProduto(Produto produto)
{
    Console.WriteLine($"ID: {produto.Id} | Categoria: {produto.Categoria} | Marca: {produto.Marca} | Preço: {produto.Preco}");
}

void MostrarAcesso(Acesso acesso)
{
    Console.WriteLine($"ID: {acesso.Id} | Produto: {acesso.ProdutoId} | Usuário: {acesso.UsuarioId} | Evento: {acesso.TipoEvento} | Data: {acesso.DataHora}");
}
