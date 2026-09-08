namespace KAYRON.Core;

public interface IMemoriaAprendidaPersistente
{
    void Salvar(MemoriaAprendida memoria);
    MemoriaAprendida? Recuperar(string chave);
    IReadOnlyCollection<MemoriaAprendida> Listar();
}
