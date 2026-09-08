namespace KAYRON.Core;

public sealed class PlanoExecucao
{
    public Guid Id { get; } = Guid.NewGuid();

    public string Objetivo { get; init; } = string.Empty;

    public IReadOnlyCollection<EtapaPlano> Etapas { get; init; } =
        Array.Empty<EtapaPlano>();
}
