using KAYRON.Core;

namespace KAYRON.Engine;

public class MemoriaAprendida : IMemoriaAprendida
{
    private readonly Dictionary<string, KAYRON.Core.MemoriaAprendida> _dados =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly IMemoriaAprendidaPersistente _memoriaPersistente;

    public MemoriaAprendida(IMemoriaAprendidaPersistente memoriaPersistente)
    {
        ArgumentNullException.ThrowIfNull(memoriaPersistente);

        _memoriaPersistente = memoriaPersistente;

        foreach (var memoria in _memoriaPersistente.Listar())
        {
            _dados[memoria.Chave] = memoria;
        }
    }

    public void Aprender(string chave, string valor)
    {
        if (string.IsNullOrWhiteSpace(chave))
        {
            throw new ArgumentException(
                "A chave da memória aprendida não pode estar vazia.",
                nameof(chave));
        }

        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ArgumentException(
                "O valor da memória aprendida não pode estar vazio.",
                nameof(valor));
        }

        var memoria = new KAYRON.Core.MemoriaAprendida
        {
            Chave = chave.Trim(),
            Valor = valor.Trim(),
            AprendidoEm = DateTime.UtcNow
        };

        _dados[memoria.Chave] = memoria;
        _memoriaPersistente.Salvar(memoria);
    }

    public KAYRON.Core.MemoriaAprendida? Recuperar(string chave)
    {
        if (string.IsNullOrWhiteSpace(chave))
        {
            return null;
        }

        return _dados.TryGetValue(chave.Trim(), out var memoria)
            ? memoria
            : null;
    }

    public IReadOnlyCollection<KAYRON.Core.MemoriaAprendida> Listar()
    {
        return _dados.Values.ToList().AsReadOnly();
    }
}
