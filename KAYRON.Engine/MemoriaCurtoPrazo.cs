using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class MemoriaCurtoPrazo
{
    private const int LimiteMensagens = 20;
    private readonly List<MensagemConversa> _mensagens = [];
    private readonly object _lock = new();

    public void Adicionar(string papel, string conteudo)
    {
        if (string.IsNullOrWhiteSpace(papel) || string.IsNullOrWhiteSpace(conteudo))
            return;

        lock (_lock)
        {
            _mensagens.Add(new MensagemConversa { Papel = papel.Trim(), Conteudo = conteudo.Trim() });
            if (_mensagens.Count > LimiteMensagens)
                _mensagens.RemoveRange(0, _mensagens.Count - LimiteMensagens);
        }
    }

    public IReadOnlyCollection<MensagemConversa> Listar()
    {
        lock (_lock)
            return _mensagens.ToArray();
    }
}
