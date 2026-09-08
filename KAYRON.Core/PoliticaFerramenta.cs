namespace KAYRON.Core;

public sealed class PoliticaFerramenta
{
    public bool Permitida { get; init; } = true;

    public bool SomenteLeitura { get; init; }

    public bool ExigeConfirmacao { get; init; }

    public IReadOnlyCollection<string> OperacoesPermitidas { get; init; } =
        Array.Empty<string>();
}
