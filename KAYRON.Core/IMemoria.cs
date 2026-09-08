namespace KAYRON.Core;

public interface IMemoria
{
    void Guardar(string chave, string valor);
    string? Recuperar(string chave);
    bool Existe(string chave);
}
