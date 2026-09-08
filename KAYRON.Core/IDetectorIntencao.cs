namespace KAYRON.Core;

public interface IDetectorIntencao
{
    IntencaoDetectada Detectar(
        string texto,
        IContexto contexto);
}
