namespace KAYRON.Core;

public interface IResolvedorContexto
{
    string Resolver(
        string entrada,
        IContexto contexto);
}
