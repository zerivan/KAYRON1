namespace KAYRON.Core;

public interface IInterpretadorLinguagem
{
    string Interpretar(
        string entrada,
        IContexto contexto);
}
