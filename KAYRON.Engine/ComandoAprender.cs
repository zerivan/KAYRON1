using KAYRON.Core;

namespace KAYRON.Engine;

public class ComandoAprender : IComando
{
    private readonly IMemoriaAprendida _memoriaAprendida;

    public string Nome => "aprender";
    public string Descricao => "Ensina o KAYRON uma informação.";

    public ComandoAprender(IMemoriaAprendida memoriaAprendida)
    {
        _memoriaAprendida = memoriaAprendida;
    }

    public Resposta Executar(string argumentos, IContexto contexto)
    {
        if (string.IsNullOrWhiteSpace(argumentos))
        {
            return new Resposta
            {
                Conteudo = "Informe o que o KAYRON deve aprender. Exemplo: /aprender nome = KAYRON"
            };
        }

        var partes = argumentos.Split('=', 2);

        if (partes.Length != 2)
        {
            return new Resposta
            {
                Conteudo = "Formato inválido. Use: /aprender chave = valor"
            };
        }

        var chave = partes[0].Trim();
        var valor = partes[1].Trim();

        if (string.IsNullOrWhiteSpace(chave) || string.IsNullOrWhiteSpace(valor))
        {
            return new Resposta
            {
                Conteudo = "A chave e o valor não podem estar vazios."
            };
        }

        _memoriaAprendida.Aprender(chave, valor);

        contexto.Adicionar($"aprendido:{chave}", valor);

        return new Resposta
        {
            Conteudo = $"KAYRON aprendeu: {chave} = {valor}"
        };
    }
}
