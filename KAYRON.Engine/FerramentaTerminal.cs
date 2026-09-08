using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class FerramentaTerminal
    : IFerramenta
{
    private readonly ITerminal _terminal;

    public string Nome => "terminal";

    public string Descricao =>
        "Executa comandos controlados no terminal local.";

    public CapacidadeFerramenta Capacidade =>
        new()
        {
            Nome = Nome,
            Descricao = Descricao,
            Operacoes =
                new[]
                {
                    "executar",
                    "comando",
                    "terminal"
                },
            Exemplos =
                new[]
                {
                    "execute dotnet build",
                    "execute dotnet test",
                    "rode git status",
                    "execute npm install",
                    "execute o comando no terminal"
                }
        };

    public FerramentaTerminal(
        ITerminal terminal)
    {
        ArgumentNullException.ThrowIfNull(terminal);

        _terminal = terminal;
    }

    public async Task<ResultadoFerramenta> ExecutarAsync(
        string argumentos,
        IContexto contexto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (string.IsNullOrWhiteSpace(argumentos))
        {
            return ResultadoFerramenta.Falha(
                "Informe o comando que deve ser executado.");
        }

        var comando =
            RemoverOperacao(argumentos);

        if (string.IsNullOrWhiteSpace(comando))
        {
            return ResultadoFerramenta.Falha(
                "Informe o comando que deve ser executado.");
        }

        var resultado =
            await _terminal.ExecutarAsync(
                comando,
                Environment.CurrentDirectory,
                cancellationToken);

        contexto.Adicionar(
            "terminal_comando",
            comando);

        contexto.Adicionar(
            "terminal_codigo_saida",
            resultado.CodigoSaida.ToString());

        contexto.Adicionar(
            "terminal_sucesso",
            resultado.Sucesso
                ? "sim"
                : "não");

        if (!string.IsNullOrWhiteSpace(resultado.Erro))
        {
            contexto.Adicionar(
                "terminal_erro",
                resultado.Erro);
        }

        if (!resultado.Sucesso)
        {
            var mensagem =
                string.IsNullOrWhiteSpace(resultado.Erro)
                    ? resultado.Saida
                    : resultado.Erro;

            return ResultadoFerramenta.Falha(
                $"Comando terminou com código {resultado.CodigoSaida}: {mensagem}");
        }

        return ResultadoFerramenta.Ok(
            string.IsNullOrWhiteSpace(resultado.Saida)
                ? "Comando executado com sucesso."
                : resultado.Saida);
    }

    private static string RemoverOperacao(
        string argumentos)
    {
        var texto =
            argumentos.Trim();

        var prefixos =
            new[]
            {
                "executar ",
                "execute ",
                "comando ",
                "rode ",
                "rodar ",
                "run ",
                "execute o comando "
            };

        foreach (var prefixo in prefixos)
        {
            if (texto.StartsWith(
                    prefixo,
                    StringComparison.OrdinalIgnoreCase))
            {
                return texto[prefixo.Length..].Trim();
            }
        }

        return texto;
    }
}
