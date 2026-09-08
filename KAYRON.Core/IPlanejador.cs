namespace KAYRON.Core;

public interface IPlanejador
{
    PlanoExecucao CriarPlano(
        string objetivo,
        IContexto contexto);
}
