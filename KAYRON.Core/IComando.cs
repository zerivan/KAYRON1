namespace KAYRON.Core;

public interface IComando
{
    string Nome { get; }
    string Descricao { get; }
    Resposta Executar(string argumentos, IContexto contexto);
}
