using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class HistoricoConversa
    : IHistoricoConversa
{
    private const int LimiteMensagens = 40;

    private readonly List<MensagemConversa> _mensagens = [];
    private readonly object _lock = new();

    public void AdicionarUsuario(string conteudo)
    {
        Adicionar("usuario", conteudo);
    }

    public void AdicionarAssistente(string conteudo)
    {
        Adicionar("kayron", conteudo);
    }

    public IReadOnlyCollection<MensagemConversa> Listar()
    {
        lock (_lock)
        {
            return _mensagens.ToArray();
        }
    }

    public void Limpar()
    {
        lock (_lock)
        {
            _mensagens.Clear();
        }
    }

    private void Adicionar(string papel, string conteudo)
    {
        if (string.IsNullOrWhiteSpace(conteudo))
        {
            return;
        }

        lock (_lock)
        {
            _mensagens.Add(new MensagemConversa
            {
                Papel = papel,
                Conteudo = conteudo.Trim()
            });

            if (_mensagens.Count > LimiteMensagens)
            {
                _mensagens.RemoveRange(
                    0,
                    _mensagens.Count - LimiteMensagens);
            }
        }
    }
}
