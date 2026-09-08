using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class ContextoConversacional
    : IContextoConversacional
{
    private readonly object _lock = new();

    private string? _ultimaMensagemUsuario;
    private string? _ultimaRespostaKayron;

    private string _resumo = string.Empty;

    public string? UltimaMensagemUsuario
    {
        get
        {
            lock (_lock)
            {
                return _ultimaMensagemUsuario;
            }
        }
    }

    public string? UltimaRespostaKayron
    {
        get
        {
            lock (_lock)
            {
                return _ultimaRespostaKayron;
            }
        }
    }

    public void Atualizar(
        IReadOnlyCollection<MensagemConversa> mensagens)
    {
        ArgumentNullException.ThrowIfNull(mensagens);

        lock (_lock)
        {
            _ultimaMensagemUsuario =
                mensagens
                    .LastOrDefault(
                        mensagem =>
                            mensagem.Papel.Equals(
                                "usuario",
                                StringComparison.OrdinalIgnoreCase))
                    ?.Conteudo;

            _ultimaRespostaKayron =
                mensagens
                    .LastOrDefault(
                        mensagem =>
                            mensagem.Papel.Equals(
                                "kayron",
                                StringComparison.OrdinalIgnoreCase))
                    ?.Conteudo;

            var recentes =
                mensagens
                    .TakeLast(10)
                    .Select(
                        mensagem =>
                            $"{mensagem.Papel}: {mensagem.Conteudo}");

            _resumo =
                string.Join(
                    Environment.NewLine,
                    recentes);
        }
    }

    public string ObterResumo()
    {
        lock (_lock)
        {
            return _resumo;
        }
    }
}
