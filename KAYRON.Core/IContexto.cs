namespace KAYRON.Core;

public interface IContexto
{
    void Adicionar(string chave, string valor);
    string? Obter(string chave);
}
