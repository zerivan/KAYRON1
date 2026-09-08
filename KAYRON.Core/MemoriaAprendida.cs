namespace KAYRON.Core;

public class MemoriaAprendida
{
    public string Chave { get; init; } = string.Empty;
    public string Valor { get; init; } = string.Empty;
    public DateTime AprendidoEm { get; init; } = DateTime.UtcNow;
}
