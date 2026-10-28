using System.Text;

public class ArquivoProdutos
{
    public const int TamanhoCategoria = 100;
    public const int TamanhoMarca = 50;
    public const int TamanhoRegistro = 8 + TamanhoCategoria + TamanhoMarca + 16 + 1;

    // Versões para operações individuais: alteram os dados e atualizam o índice.
    public static bool Inserir(string caminho, Produto produto, string caminhoIndice)
    {
        bool inserido = Inserir(caminho, produto);
        if (inserido)
            IndiceProdutos.Criar(caminho, caminhoIndice);

        return inserido;
    }

    public static bool Remover(string caminho, long id, string caminhoIndice)
    {
        bool removido = Remover(caminho, id);
        if (removido)
            IndiceProdutos.Criar(caminho, caminhoIndice);

        return removido;
    }

    // Por enquanto, cria um arquivo com apenas um produto.
    // FileMode.Create substitui o conteúdo se o arquivo já existir.
    public static void Gravar(string caminho, Produto produto)
    {
        GravarRegistro(caminho, produto, FileMode.Create);
    }

    // Acrescenta ao final somente se o ID mantiver a ordem crescente.
    // Para colocar um ID entre registros existentes, use Inserir.
    public static void Acrescentar(string caminho, Produto produto)
    {
        if (File.Exists(caminho))
        {
            long quantidade = Contar(caminho);
            if (quantidade > 0)
            {
                Produto ultimo = Ler(caminho, quantidade - 1);
                if (produto.Id <= ultimo.Id)
                    throw new ArgumentException("O novo ID deve ser maior que o último ID do arquivo.");
            }
        }

        GravarRegistro(caminho, produto, FileMode.Append);
    }

    public static long Contar(string caminho)
    {
        long tamanho = new FileInfo(caminho).Length;
        if (tamanho % TamanhoRegistro != 0)
            throw new InvalidDataException("O arquivo contém um registro incompleto.");

        return tamanho / TamanhoRegistro;
    }

    // Create e Append usam exatamente o mesmo formato de registro.
    private static void GravarRegistro(string caminho, Produto produto, FileMode modo)
    {
        byte[] registro = PrepararRegistro(produto);
        using FileStream arquivo = new FileStream(caminho, modo, FileAccess.Write);
        arquivo.Write(registro);
    }

    // Monta somente um registro em memória e valida os tamanhos dos textos.
    private static byte[] PrepararRegistro(Produto produto)
    {
        byte[] categoria = PrepararTexto(produto.Categoria, TamanhoCategoria);
        byte[] marca = PrepararTexto(produto.Marca, TamanhoMarca);

        using MemoryStream arquivo = new MemoryStream();
        using BinaryWriter escritor = new BinaryWriter(arquivo);

        escritor.Write(produto.Id);
        escritor.Write(categoria);
        escritor.Write(marca);
        escritor.Write(produto.Preco);
        escritor.Write((byte)'\n');
        escritor.Flush();
        return arquivo.ToArray();
    }

    // Retorna false se o produto já existe; nesse caso, mantém os dados anteriores.
    public static bool Inserir(string caminho, Produto produto)
    {
        byte[] novoRegistro = PrepararRegistro(produto);
        using FileStream arquivo = new FileStream(caminho, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        using BinaryReader leitor = new BinaryReader(arquivo);

        if (arquivo.Length % TamanhoRegistro != 0)
            throw new InvalidDataException("O arquivo contém um registro incompleto.");

        long quantidade = arquivo.Length / TamanhoRegistro;
        long inicio = 0;
        long fim = quantidade - 1;

        // A busca binária também encontra a posição onde o novo ID deve entrar.
        while (inicio <= fim)
        {
            long meio = inicio + (fim - inicio) / 2;
            arquivo.Seek(meio * TamanhoRegistro, SeekOrigin.Begin);
            long idExistente = leitor.ReadInt64();

            if (produto.Id == idExistente)
                return false;

            if (produto.Id < idExistente)
                fim = meio - 1;
            else
                inicio = meio + 1;
        }

        // Copia de trás para frente para não sobrescrever registros ainda não movidos.
        byte[] registro = new byte[TamanhoRegistro];
        for (long posicao = quantidade - 1; posicao >= inicio; posicao--)
        {
            arquivo.Seek(posicao * TamanhoRegistro, SeekOrigin.Begin);
            arquivo.ReadExactly(registro);
            arquivo.Seek((posicao + 1) * TamanhoRegistro, SeekOrigin.Begin);
            arquivo.Write(registro);
        }

        arquivo.Seek(inicio * TamanhoRegistro, SeekOrigin.Begin);
        arquivo.Write(novoRegistro);
        return true;
    }

    // A posição começa em zero: 0 é o primeiro registro, 1 é o segundo.
    public static Produto Ler(string caminho, long posicao)
    {
        using FileStream arquivo = new FileStream(caminho, FileMode.Open, FileAccess.Read);
        using BinaryReader leitor = new BinaryReader(arquivo);

        if (arquivo.Length % TamanhoRegistro != 0)
            throw new InvalidDataException("O arquivo contém um registro incompleto.");

        long quantidade = arquivo.Length / TamanhoRegistro;
        if (posicao < 0 || posicao >= quantidade)
            throw new ArgumentOutOfRangeException(nameof(posicao), "Posição de produto inexistente.");

        arquivo.Seek(posicao * TamanhoRegistro, SeekOrigin.Begin);

        Produto produto = new Produto();
        produto.Id = leitor.ReadInt64();
        produto.Categoria = Encoding.UTF8.GetString(leitor.ReadBytes(TamanhoCategoria)).TrimEnd(' ');
        produto.Marca = Encoding.UTF8.GetString(leitor.ReadBytes(TamanhoMarca)).TrimEnd(' ');
        produto.Preco = leitor.ReadDecimal();

        if (leitor.ReadByte() != (byte)'\n')
            throw new InvalidDataException("Final de registro inválido.");

        return produto;
    }

    // Exige que o arquivo esteja ordenado pelo ID em ordem crescente.
    public static Produto? BuscarPorId(string caminho, long id)
    {
        long inicio = 0;
        long fim = Contar(caminho) - 1;

        while (inicio <= fim)
        {
            long meio = inicio + (fim - inicio) / 2;
            Produto produto = Ler(caminho, meio);

            if (produto.Id == id)
                return produto;

            if (id < produto.Id)
                fim = meio - 1;
            else
                inicio = meio + 1;
        }

        // O intervalo acabou e o ID não foi encontrado.
        return null;
    }

    // Converte o texto em bytes e completa o espaço restante com espaços.
    // Remove fisicamente o registro e preserva a ordem dos IDs restantes.
    public static bool Remover(string caminho, long id)
    {
        using FileStream arquivo = new FileStream(caminho, FileMode.Open, FileAccess.ReadWrite);
        using BinaryReader leitor = new BinaryReader(arquivo);

        if (arquivo.Length % TamanhoRegistro != 0)
            throw new InvalidDataException("O arquivo contém um registro incompleto.");

        long quantidade = arquivo.Length / TamanhoRegistro;
        long inicio = 0;
        long fim = quantidade - 1;
        long posicaoRemover = -1;

        while (inicio <= fim)
        {
            long meio = inicio + (fim - inicio) / 2;
            arquivo.Seek(meio * TamanhoRegistro, SeekOrigin.Begin);
            long idExistente = leitor.ReadInt64();

            if (id == idExistente)
            {
                posicaoRemover = meio;
                break;
            }

            if (id < idExistente)
                fim = meio - 1;
            else
                inicio = meio + 1;
        }

        if (posicaoRemover == -1)
            return false;

        // Copia da esquerda para a direita, preenchendo o espaço do removido.
        byte[] registro = new byte[TamanhoRegistro];
        for (long posicao = posicaoRemover + 1; posicao < quantidade; posicao++)
        {
            arquivo.Seek(posicao * TamanhoRegistro, SeekOrigin.Begin);
            arquivo.ReadExactly(registro);
            arquivo.Seek((posicao - 1) * TamanhoRegistro, SeekOrigin.Begin);
            arquivo.Write(registro);
        }

        // O último registro ficou duplicado após as cópias: corta esse espaço.
        arquivo.SetLength((quantidade - 1) * TamanhoRegistro);
        return true;
    }

    private static byte[] PrepararTexto(string texto, int tamanho)
    {
        byte[] textoEmBytes = Encoding.UTF8.GetBytes(texto);
        if (textoEmBytes.Length > tamanho)
            throw new ArgumentException($"O texto ultrapassa o limite de {tamanho} bytes.");

        byte[] campo = new byte[tamanho];
        Array.Fill(campo, (byte)' ');
        textoEmBytes.CopyTo(campo, 0);
        return campo;
    }
}
