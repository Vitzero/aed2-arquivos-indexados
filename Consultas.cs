public class Consultas
{
    // Conta somente os eventos de compra registrados para este produto.
    public static long ContarComprasPorProduto(string caminhoAcessos, long produtoId)
    {
        long total = 0;
        long quantidade = ArquivoAcessos.Contar(caminhoAcessos);

        for (long posicao = 0; posicao < quantidade; posicao++)
        {
            Acesso acesso = ArquivoAcessos.Ler(caminhoAcessos, posicao);
            if (acesso.ProdutoId == produtoId && acesso.TipoEvento == "purchase")
                total++;
        }

        return total;
    }

    // Conta eventos do produto, não usuários distintos nem unidades vendidas.
    public static long ContarAcessosPorProduto(string caminhoAcessos, long produtoId)
    {
        long total = 0;
        long quantidade = ArquivoAcessos.Contar(caminhoAcessos);

        for (long posicao = 0; posicao < quantidade; posicao++)
        {
            Acesso acesso = ArquivoAcessos.Ler(caminhoAcessos, posicao);
            if (acesso.ProdutoId == produtoId)
                total++;
        }

        return total;
    }
}
