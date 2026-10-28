using System.Globalization;
using Microsoft.VisualBasic.FileIO;

public class LeitorCsv
{
    public static void Importar(string caminhoCsv, string caminhoProdutos, string caminhoAcessos, int limite)
    {
        Importar(new string[] { caminhoCsv }, caminhoProdutos, caminhoAcessos, limite);
    }

    // O limite é por CSV. O ID de acesso continua de um arquivo para o próximo.
    public static void Importar(string[] caminhosCsv, string caminhoProdutos, string caminhoAcessos, int limite)
    {
        if (limite <= 0)
            throw new ArgumentException("O limite deve ser maior que zero.");

        if (caminhosCsv.Length == 0)
            throw new ArgumentException("Informe pelo menos um CSV.");

        // Confere todas as origens antes de começar a gravar os dados.
        HashSet<string> arquivosInformados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string caminho in caminhosCsv)
        {
            if (!File.Exists(caminho))
                throw new FileNotFoundException("CSV não encontrado.", caminho);
            if (!arquivosInformados.Add(Path.GetFullPath(caminho)))
                throw new ArgumentException("O mesmo CSV foi informado mais de uma vez.");
        }

        // Permite iniciar em arquivos vazios, mas nunca apaga dados existentes.
        if ((File.Exists(caminhoProdutos) && new FileInfo(caminhoProdutos).Length > 0) ||
            (File.Exists(caminhoAcessos) && new FileInfo(caminhoAcessos).Length > 0))
            throw new IOException("A importação inicial exige os dois arquivos de dados vazios.");

        using (FileStream produtos = new FileStream(caminhoProdutos, FileMode.OpenOrCreate)) { }
        using (FileStream acessos = new FileStream(caminhoAcessos, FileMode.OpenOrCreate)) { }

        long idAcesso = 1;
        foreach (string caminhoCsv in caminhosCsv)
        {
            using TextFieldParser leitor = new TextFieldParser(caminhoCsv);
            leitor.TextFieldType = FieldType.Delimited;
            leitor.SetDelimiters(",");
            leitor.HasFieldsEnclosedInQuotes = true;
            leitor.ReadFields();

            int eventosLidos = 0;
            while (eventosLidos < limite && !leitor.EndOfData)
            {
                string[] campos = leitor.ReadFields()
                    ?? throw new InvalidDataException("Não foi possível ler a linha do CSV.");
                if (campos.Length != 9)
                    throw new InvalidDataException("A linha do CSV deve ter 9 colunas.");

                Produto produto = CriarProduto(campos);
                Acesso acesso = CriarAcesso(campos, idAcesso);

                // Produtos repetidos entre meses também mantêm a primeira ocorrência.
                ArquivoProdutos.Inserir(caminhoProdutos, produto);
                ArquivoAcessos.Acrescentar(caminhoAcessos, acesso);
                idAcesso++;
                eventosLidos++;
            }

            Console.WriteLine($"{Path.GetFileName(caminhoCsv)}: {eventosLidos} eventos importados.");
        }
    }

    public static void MostrarAmostra(string caminho, int limite)
    {
        // TextFieldParser já faz parte do .NET e lê CSV sem carregar tudo na memória.
        using TextFieldParser leitor = new TextFieldParser(caminho);
        leitor.TextFieldType = FieldType.Delimited;
        leitor.SetDelimiters(",");
        leitor.HasFieldsEnclosedInQuotes = true;

        // A primeira linha contém os nomes das colunas, não um evento.
        leitor.ReadFields();

        long idAcesso = 1;
        while (idAcesso <= limite && !leitor.EndOfData)
        {
            string[] campos = leitor.ReadFields()
                ?? throw new InvalidDataException("Não foi possível ler a linha do CSV.");

            if (campos.Length != 9)
                throw new InvalidDataException("A linha do CSV deve ter 9 colunas.");

            Produto produto = CriarProduto(campos);
            Acesso acesso = CriarAcesso(campos, idAcesso);

            Console.WriteLine();
            Console.WriteLine($"LINHA DE DADOS {idAcesso} DO CSV");
            Console.WriteLine($"Produto: {produto.Id}, categoria: {produto.Categoria}");
            Console.WriteLine($"Marca: {produto.Marca}, preço: {produto.Preco}");
            Console.WriteLine($"Acesso: {acesso.Id}, usuário: {acesso.UsuarioId}");
            Console.WriteLine($"Evento: {acesso.TipoEvento}, data: {acesso.DataHora}");

            idAcesso++;
        }
    }

    // As conversões são compartilhadas pela exibição e pela importação.
    private static Produto CriarProduto(string[] campos)
    {
        Produto produto = new Produto();
        produto.Id = long.Parse(campos[2]);
        produto.Categoria = campos[4];
        produto.Marca = campos[5];
        produto.Preco = decimal.Parse(campos[6], CultureInfo.InvariantCulture);
        return produto;
    }

    private static Acesso CriarAcesso(string[] campos, long id)
    {
        Acesso acesso = new Acesso();
        acesso.Id = id;
        acesso.ProdutoId = long.Parse(campos[2]);
        acesso.UsuarioId = long.Parse(campos[7]);
        acesso.TipoEvento = campos[1];
        acesso.DataHora = campos[0];
        return acesso;
    }
}
