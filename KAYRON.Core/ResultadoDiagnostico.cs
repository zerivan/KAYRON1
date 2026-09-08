namespace KAYRON.Core;

public sealed class ResultadoDiagnostico
{
    public bool EncontrouErros { get; init; }
    public int TotalErros { get; init; }
    public int TotalAvisos { get; init; }
    public IReadOnlyCollection<DiagnosticoErro> Problemas { get; init; }
        = Array.Empty<DiagnosticoErro>();
}
