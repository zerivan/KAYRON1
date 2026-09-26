namespace KAYRON.Core;

public interface IMemoriaAprendida
{
    void Aprender(string chave, string valor);
    void Aprender(string chave, string valor, string tipo, int importancia, bool explicita, string origem, IEnumerable<string>? tags);
    bool Remover(string chave);
    MemoriaAprendida? Recuperar(string chave);
    IReadOnlyCollection<MemoriaAprendida> Pesquisar(string consulta, int limite = 5);
    IReadOnlyCollection<MemoriaAprendida> Listar();
}
