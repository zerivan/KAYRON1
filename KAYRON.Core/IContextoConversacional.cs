namespace KAYRON.Core;

public interface IContextoConversacional
{
    void Atualizar(
        IReadOnlyCollection<MensagemConversa> mensagens);

    string ObterResumo();

    string ObterMensagensJson();

    string? UltimaMensagemUsuario { get; }

    string? UltimaRespostaKayron { get; }
}
