namespace KAYRON.Core;

public interface ICatalogoFerramentas
{
    IReadOnlyCollection<IFerramenta> Listar();

    IFerramenta? Obter(string nome);
}
