namespace KAYRON.Core;

public class Contexto
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public DateTime CriadoEm { get; init; } = DateTime.UtcNow;
    public List<ContextoDados> Dados { get; init; } = new();
}
