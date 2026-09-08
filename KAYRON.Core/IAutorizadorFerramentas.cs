namespace KAYRON.Core;

public interface IAutorizadorFerramentas
{
    ResultadoAutorizacao Autorizar(
        IFerramenta ferramenta,
        string argumentos,
        string? operacaoSolicitada = null);
}

