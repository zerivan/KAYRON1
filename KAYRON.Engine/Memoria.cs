using KAYRON.Core;

namespace KAYRON.Engine;

public class Memoria : IMemoria
{
    private readonly Dictionary<string, string> _dados =
        new(StringComparer.OrdinalIgnoreCase);

    public void Guardar(string chave, string valor)
    {
        if (string.IsNullOrWhiteSpace(chave))
        {
            throw new ArgumentException(
                "A chave da memória não pode estar vazia.",
                nameof(chave));
        }

        _dados[chave] = valor;
    }

    public string? Recuperar(string chave)
    {
        if (string.IsNullOrWhiteSpace(chave))
        {
            return null;
        }

        return _dados.TryGetValue(chave, out var valor)
            ? valor
            : null;
    }

    public bool Existe(string chave)
    {
        return !string.IsNullOrWhiteSpace(chave) &&
               _dados.ContainsKey(chave);
    }
}
