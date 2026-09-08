namespace KAYRON.Core;

public sealed class ResultadoAutorizacao
{
    public bool Permitido { get; init; }

    public bool ExigeConfirmacao { get; init; }

    public string Motivo { get; init; } = string.Empty;

    public static ResultadoAutorizacao Permitir()
    {
        return new ResultadoAutorizacao
        {
            Permitido = true
        };
    }

    public static ResultadoAutorizacao Confirmacao(
        string motivo)
    {
        return new ResultadoAutorizacao
        {
            Permitido = false,
            ExigeConfirmacao = true,
            Motivo = motivo
        };
    }

    public static ResultadoAutorizacao Negar(
        string motivo)
    {
        return new ResultadoAutorizacao
        {
            Permitido = false,
            ExigeConfirmacao = false,
            Motivo = motivo
        };
    }
}
