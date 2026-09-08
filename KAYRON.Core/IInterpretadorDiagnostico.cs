using KAYRON.Core;

namespace KAYRON.Engine;

public interface IInterpretadorDiagnostico
{
    string Interpretar(
        string argumento,
        string resultado);
}
