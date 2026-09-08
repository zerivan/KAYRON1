namespace KAYRON.Core;

public interface IProcessadorConfirmacao
{
    bool EhConfirmacao(string entrada);

    bool EhCancelamento(string entrada);

    SolicitacaoConfirmacao? ObterPendente(
        IContexto contexto);

    bool Confirmar(
        IContexto contexto);

    bool Cancelar(
        IContexto contexto);
}
