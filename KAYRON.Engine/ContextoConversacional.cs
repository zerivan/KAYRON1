using System.Text.Json;
using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class ContextoConversacional
    : IContextoConversacional
{
    private readonly object _lock = new();
    private string? _ultimaMensagemUsuario;
    private string? _ultimaRespostaKayron;
    private string _resumo = string.Empty;
    private string _mensagensJson = "[]";

    public string? UltimaMensagemUsuario
    {
        get { lock (_lock) return _ultimaMensagemUsuario; }
    }

    public string? UltimaRespostaKayron
    {
        get { lock (_lock) return _ultimaRespostaKayron; }
    }

    public void Atualizar(IReadOnlyCollection<MensagemConversa> mensagens)
    {
        ArgumentNullException.ThrowIfNull(mensagens);

        lock (_lock)
        {
            var recentes = mensagens.TakeLast(20).ToArray();
            _ultimaMensagemUsuario = recentes.LastOrDefault(m =>
                m.Papel.Equals("usuario", StringComparison.OrdinalIgnoreCase))?.Conteudo;
            _ultimaRespostaKayron = recentes.LastOrDefault(m =>
                m.Papel.Equals("kayron", StringComparison.OrdinalIgnoreCase))?.Conteudo;
            _resumo = string.Join(
                Environment.NewLine,
                recentes.Select(m => $"{m.Papel}: {m.Conteudo}"));
            _mensagensJson = JsonSerializer.Serialize(recentes);
        }
    }

    public string ObterResumo()
    {
        lock (_lock)
        {
            return _resumo;
        }
    }

    public string ObterMensagensJson()
    {
        lock (_lock)
        {
            return _mensagensJson;
        }
    }
}
