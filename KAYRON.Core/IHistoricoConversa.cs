namespace KAYRON.Core;

public interface IHistoricoConversa
{
    void AdicionarUsuario(string conteudo);

    void AdicionarAssistente(string conteudo);

    IReadOnlyCollection<MensagemConversa> Listar();

    void Limpar();
}
