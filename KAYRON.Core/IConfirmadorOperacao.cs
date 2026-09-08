namespace KAYRON.Core;

public interface IConfirmadorOperacao
{
    SolicitacaoConfirmacao Criar(
        string ferramenta,
        string operacao,
        string argumentos,
        string motivo,
        IContexto contexto);

    bool Confirmar(
        string id,
        IContexto contexto);

    bool Cancelar(
        string id,
        IContexto contexto);

    SolicitacaoConfirmacao? ObterPendente(
        IContexto contexto);
}
