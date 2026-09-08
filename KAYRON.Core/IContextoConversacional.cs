namespace KAYRON.Core;

public interface IContextoConversacional
{
    void Atualizar(
        IReadOnlyCollection<MensagemConversa> mensagens);

    string ObterResumo();

    string? UltimaMensagemUsuario { get; }

    string? UltimaRespostaKayron { get; }
}
