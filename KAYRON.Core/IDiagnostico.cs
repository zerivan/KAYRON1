namespace KAYRON.Core;

public interface IDiagnostico
{
    ResultadoDiagnostico Analisar(
        string saida,
        string erro,
        string origem);
}
