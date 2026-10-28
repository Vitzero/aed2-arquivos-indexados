public class IndiceProdutos
{
    // Um único nível de índice: uma entrada para cada bloco de 10 produtos.
    public const int Intervalo = 10;

    // Cada entrada contém dois longs: ID do produto e posição do registro.
    public const int TamanhoEntrada = 8 + 8;

    // Lê e confere o índice salvo, sem manter todas as entradas na memória.
    public static long Carregar(string caminhoDados, string caminhoIndice)
    {
        long quantidade = ArquivoProdutos.Contar(caminhoDados);
        long entradasEsperadas = quantidade / Intervalo;
        if (quantidade % Intervalo != 0)
            entradasEsperadas++;

        using FileStream arquivo = new FileStream(caminhoIndice, FileMode.Open, FileAccess.Read);
        using BinaryReader leitor = new BinaryReader(arquivo);

        if (arquivo.Length != entradasEsperadas * TamanhoEntrada)
            throw new InvalidDataException("O tamanho do índice não corresponde aos dados.");

        for (long entrada = 0; entrada < entradasEsperadas; entrada++)
        {
            long chave = leitor.ReadInt64();
            long posicao = leitor.ReadInt64();
            long posicaoEsperada = entrada * Intervalo;

            if (posicao != posicaoEsperada ||
                chave != ArquivoProdutos.Ler(caminhoDados, posicaoEsperada).Id)
                throw new InvalidDataException("O índice está desatualizado.");
        }

        return entradasEsperadas;
    }

    // O índice precisa corresponder ao estado atual do arquivo de produtos.
    public static Produto? BuscarPorId(string caminhoProdutos, string caminhoIndice, long id)
    {
        using FileStream arquivo = new FileStream(caminhoIndice, FileMode.Open, FileAccess.Read);
        using BinaryReader leitor = new BinaryReader(arquivo);

        if (arquivo.Length % TamanhoEntrada != 0)
            throw new InvalidDataException("O índice contém uma entrada incompleta.");

        long inicio = 0;
        long fim = arquivo.Length / TamanhoEntrada - 1;
        long inicioBloco = -1;

        // Procura a maior chave do índice que não ultrapassa o ID procurado.
        while (inicio <= fim)
        {
            long meio = inicio + (fim - inicio) / 2;
            arquivo.Seek(meio * TamanhoEntrada, SeekOrigin.Begin);
            long chave = leitor.ReadInt64();
            long posicao = leitor.ReadInt64();

            if (chave <= id)
            {
                inicioBloco = posicao;
                inicio = meio + 1;
            }
            else
            {
                fim = meio - 1;
            }
        }

        // Índice vazio ou ID menor que a primeira chave.
        if (inicioBloco == -1)
            return null;

        long quantidade = ArquivoProdutos.Contar(caminhoProdutos);
        long fimBloco = Math.Min(inicioBloco + Intervalo, quantidade);

        // Ler usa Seek para acessar cada registro do bloco no arquivo de dados.
        for (long posicao = inicioBloco; posicao < fimBloco; posicao++)
        {
            Produto produto = ArquivoProdutos.Ler(caminhoProdutos, posicao);
            if (produto.Id == id)
                return produto;

            if (produto.Id > id)
                return null;
        }

        return null;
    }

    public static long Criar(string caminhoProdutos, string caminhoIndice)
    {
        if (string.Equals(Path.GetFullPath(caminhoProdutos),
            Path.GetFullPath(caminhoIndice), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("O índice deve usar um arquivo diferente dos produtos.");

        long quantidade = ArquivoProdutos.Contar(caminhoProdutos);
        long entradas = 0;

        // Recria o índice a partir do estado atual do arquivo de produtos.
        using FileStream arquivo = new FileStream(caminhoIndice, FileMode.Create, FileAccess.Write);
        using BinaryWriter escritor = new BinaryWriter(arquivo);

        for (long posicao = 0; posicao < quantidade; posicao += Intervalo)
        {
            Produto produto = ArquivoProdutos.Ler(caminhoProdutos, posicao);
            escritor.Write(produto.Id);
            escritor.Write(posicao);
            entradas++;
        }

        return entradas;
    }
}
