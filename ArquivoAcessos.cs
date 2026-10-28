using System.Text;

public class ArquivoAcessos
{
    public const int TamanhoTipoEvento = 8;
    public const int TamanhoDataHora = 23;
    public const int TamanhoRegistro = 8 + 8 + 8 + TamanhoTipoEvento + TamanhoDataHora + 1;

    public static bool Inserir(string caminho, Acesso acesso, string caminhoIndice)
    {
        bool inserido = Inserir(caminho, acesso);
        if (inserido)
            IndiceAcessos.Criar(caminho, caminhoIndice);

        return inserido;
    }

    // Acrescenta o acesso e só depois reconstrói o índice.
    public static void Acrescentar(string caminho, Acesso acesso, string caminhoIndice)
    {
        Acrescentar(caminho, acesso);
        IndiceAcessos.Criar(caminho, caminhoIndice);
    }

    public static bool Remover(string caminho, long id, string caminhoIndice)
    {
        bool removido = Remover(caminho, id);
        if (removido)
            IndiceAcessos.Criar(caminho, caminhoIndice);

        return removido;
    }

    // Neste primeiro exemplo, recria o arquivo com apenas um acesso.
    public static void Gravar(string caminho, Acesso acesso)
    {
        GravarRegistro(caminho, acesso, FileMode.Create);
    }

    // Os IDs dos acessos devem permanecer únicos e em ordem crescente.
    public static void Acrescentar(string caminho, Acesso acesso)
    {
        if (File.Exists(caminho))
        {
            long quantidade = Contar(caminho);
            if (quantidade > 0)
            {
                Acesso ultimo = Ler(caminho, quantidade - 1);
                if (acesso.Id <= ultimo.Id)
                    throw new ArgumentException("O novo ID deve ser maior que o último ID do arquivo.");
            }
        }

        GravarRegistro(caminho, acesso, FileMode.Append);
    }

    public static long Contar(string caminho)
    {
        long tamanho = new FileInfo(caminho).Length;
        if (tamanho % TamanhoRegistro != 0)
            throw new InvalidDataException("O arquivo contém um registro incompleto.");

        return tamanho / TamanhoRegistro;
    }

    private static void GravarRegistro(string caminho, Acesso acesso, FileMode modo)
    {
        byte[] registro = PrepararRegistro(acesso);
        using FileStream arquivo = new FileStream(caminho, modo, FileAccess.Write);
        arquivo.Write(registro);
    }

    // Prepara apenas os 56 bytes de um acesso antes de alterar o arquivo.
    private static byte[] PrepararRegistro(Acesso acesso)
    {
        byte[] tipoEvento = PrepararTexto(acesso.TipoEvento, TamanhoTipoEvento);
        byte[] dataHora = PrepararTexto(acesso.DataHora, TamanhoDataHora);

        using MemoryStream arquivo = new MemoryStream();
        using BinaryWriter escritor = new BinaryWriter(arquivo);

        escritor.Write(acesso.Id);
        escritor.Write(acesso.ProdutoId);
        escritor.Write(acesso.UsuarioId);
        escritor.Write(tipoEvento);
        escritor.Write(dataHora);
        escritor.Write((byte)'\n');
        escritor.Flush();
        return arquivo.ToArray();
    }

    // Insere em qualquer posição, mantendo a ordem crescente dos IDs.
    public static bool Inserir(string caminho, Acesso acesso)
    {
        byte[] novoRegistro = PrepararRegistro(acesso);
        using FileStream arquivo = new FileStream(caminho, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        using BinaryReader leitor = new BinaryReader(arquivo);

        if (arquivo.Length % TamanhoRegistro != 0)
            throw new InvalidDataException("O arquivo contém um registro incompleto.");

        long quantidade = arquivo.Length / TamanhoRegistro;
        long inicio = 0;
        long fim = quantidade - 1;

        while (inicio <= fim)
        {
            long meio = inicio + (fim - inicio) / 2;
            arquivo.Seek(meio * TamanhoRegistro, SeekOrigin.Begin);
            long idExistente = leitor.ReadInt64();

            if (acesso.Id == idExistente)
                return false;

            if (acesso.Id < idExistente)
                fim = meio - 1;
            else
                inicio = meio + 1;
        }

        // Move de trás para frente para abrir espaço sem perder registros.
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

    // A posição começa em zero, assim como no arquivo de produtos.
    public static Acesso Ler(string caminho, long posicao)
    {
        using FileStream arquivo = new FileStream(caminho, FileMode.Open, FileAccess.Read);
        using BinaryReader leitor = new BinaryReader(arquivo);

        if (arquivo.Length % TamanhoRegistro != 0)
            throw new InvalidDataException("O arquivo contém um registro incompleto.");

        long quantidade = arquivo.Length / TamanhoRegistro;
        if (posicao < 0 || posicao >= quantidade)
            throw new ArgumentOutOfRangeException(nameof(posicao), "Posição de acesso inexistente.");

        arquivo.Seek(posicao * TamanhoRegistro, SeekOrigin.Begin);

        Acesso acesso = new Acesso();
        acesso.Id = leitor.ReadInt64();
        acesso.ProdutoId = leitor.ReadInt64();
        acesso.UsuarioId = leitor.ReadInt64();
        acesso.TipoEvento = Encoding.UTF8.GetString(leitor.ReadBytes(TamanhoTipoEvento)).TrimEnd(' ');
        acesso.DataHora = Encoding.UTF8.GetString(leitor.ReadBytes(TamanhoDataHora)).TrimEnd(' ');

        if (leitor.ReadByte() != (byte)'\n')
            throw new InvalidDataException("Final de registro inválido.");

        return acesso;
    }

    // Pesquisa pelo ID do acesso, não pelo ID do produto ou do usuário.
    public static Acesso? BuscarPorId(string caminho, long id)
    {
        long inicio = 0;
        long fim = Contar(caminho) - 1;

        while (inicio <= fim)
        {
            long meio = inicio + (fim - inicio) / 2;
            Acesso acesso = Ler(caminho, meio);

            if (acesso.Id == id)
                return acesso;

            if (id < acesso.Id)
                fim = meio - 1;
            else
                inicio = meio + 1;
        }

        return null;
    }

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
