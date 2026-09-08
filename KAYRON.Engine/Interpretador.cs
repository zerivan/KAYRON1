using KAYRON.Core;

namespace KAYRON.Engine;

public class Interpretador
{
    public Intencao Interpretar(Instrucao instrucao)
    {
        ArgumentNullException.ThrowIfNull(instrucao);

        var conteudo = instrucao.Conteudo.Trim();

        if (string.IsNullOrWhiteSpace(conteudo))
        {
            return Intencao.Desconhecida;
        }

        return conteudo.StartsWith('/')
            ? Intencao.Comando
            : Intencao.Conversar;
    }
}
