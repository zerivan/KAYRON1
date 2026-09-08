using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class FerramentaDiagnostico
    : IFerramenta
{
    private readonly IDiagnostico _diagnostico;
    private readonly ITerminal _terminal;
    private readonly IInterpretadorDiagnostico _interpretador;

    public FerramentaDiagnostico(
        IDiagnostico diagnostico,
        ITerminal terminal,
        IInterpretadorDiagnostico interpretador)
    {
        ArgumentNullException.ThrowIfNull(diagnostico);
        ArgumentNullException.ThrowIfNull(terminal);
        ArgumentNullException.ThrowIfNull(interpretador);

        _interpretador = interpretador;

        _diagnostico = diagnostico;
        _terminal = terminal;
    }

    public string Nome =>
        "diagnostico";

    public string Descricao =>
        "Analisa erros e avisos de ferramentas de desenvolvimento e executa verificações controladas do ambiente Windows.";

    public CapacidadeFerramenta Capacidade =>
        new()
        {
            Nome = Nome,
            Descricao = Descricao,
            Operacoes =
            [
                "analisar",
                "diagnosticar",
                "erros",
                "avisos"
            ],
            Exemplos =
            [
                "diagnostique o Windows",
                "diagnostique os erros",
                "analise os erros do build",
                "mostre os erros",
                "analise os avisos"
            ]
        };

    public async Task<ResultadoFerramenta> ExecutarAsync(
        string argumentos,
        IContexto contexto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        cancellationToken.ThrowIfCancellationRequested();

        var projetoSaida =
            contexto.Obter("projeto_saida");

        var gitSaida =
            contexto.Obter("git_saida");

        var projetoStatus =
            contexto.Obter("projeto_status");

        var possuiSaidaDesenvolvimento =
            !string.IsNullOrWhiteSpace(projetoSaida) ||
            !string.IsNullOrWhiteSpace(gitSaida);

        var pedidoWindows =
            EhPedidoWindows(argumentos);

        if (pedidoWindows || !possuiSaidaDesenvolvimento)
        {
            return await DiagnosticarWindowsAsync(
                contexto,
                cancellationToken);
        }

        var saida =
            projetoSaida ??
            gitSaida ??
            string.Empty;

        var erro =
            projetoStatus?.Equals(
                "falha",
                StringComparison.OrdinalIgnoreCase) == true
                ? saida
                : string.Empty;

        var resultado =
            _diagnostico.Analisar(
                saida,
                erro,
                "projeto");

        contexto.Adicionar(
            "diagnostico_total_erros",
            resultado.TotalErros.ToString());

        contexto.Adicionar(
            "diagnostico_total_avisos",
            resultado.TotalAvisos.ToString());

        if (!resultado.EncontrouErros &&
            resultado.TotalAvisos == 0)
        {
            return ResultadoFerramenta.Ok(
                "Nenhum erro ou aviso estruturado foi identificado.");
        }

        var linhas =
            resultado.Problemas
                .Select(
                    problema =>
                    {
                        var local =
                            string.IsNullOrWhiteSpace(
                                problema.Arquivo)
                                ? string.Empty
                                : problema.Arquivo;

                        if (problema.Linha.HasValue)
                        {
                            local +=
                                $":{problema.Linha.Value}";
                        }

                        if (problema.Coluna.HasValue)
                        {
                            local +=
                                $":{problema.Coluna.Value}";
                        }

                        var codigo =
                            string.IsNullOrWhiteSpace(
                                problema.Codigo)
                                ? string.Empty
                                : $" [{problema.Codigo}]";

                        return
                            $"{problema.Severidade.ToUpperInvariant()}" +
                            $"{codigo} {local} — " +
                            $"{problema.Mensagem}";
                    })
                .ToArray();

        return ResultadoFerramenta.Ok(
            string.Join(
                Environment.NewLine,
                linhas));
    }

    private async Task<ResultadoFerramenta> DiagnosticarWindowsAsync(
        IContexto contexto,
        CancellationToken cancellationToken)
    {
        var verificacoes =
            new (string Nome, string Comando)[]
            {
                ("Windows", "ver"),
                ("Integridade do sistema", "sfc /verifyonly"),
                ("Rede", "ipconfig /all")
            };

        var resultados =
            new List<string>();

        foreach (var verificacao in verificacoes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var resultado =
                await _terminal.ExecutarAsync(
                    verificacao.Comando,
                    Environment.CurrentDirectory,
                    cancellationToken);

            if (resultado.Sucesso)
            {
                var saida =
                    string.IsNullOrWhiteSpace(resultado.Saida)
                        ? "Sem saída."
                        : resultado.Saida;

                resultados.Add(
                    $"{verificacao.Nome}:{Environment.NewLine}{saida}");
            }
            else
            {
                var erro =
                    string.IsNullOrWhiteSpace(resultado.Erro)
                        ? resultado.Saida
                        : resultado.Erro;

                resultados.Add(
                    $"{verificacao.Nome}: falha na verificação." +
                    Environment.NewLine +
                    erro);
            }
        }

        contexto.Adicionar(
            "diagnostico_windows",
            "executado");

        contexto.Adicionar(
            "diagnostico_total_erros",
            "0");

        contexto.Adicionar(
            "diagnostico_total_avisos",
            "0");

        return ResultadoFerramenta.Ok(
            string.Join(
                Environment.NewLine +
                Environment.NewLine,
                resultados));
    }

    private static bool EhPedidoWindows(
        string argumentos)
    {
        var texto =
            argumentos.Trim();

        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        var termos =
            new[]
            {
                "windows",
                "pc",
                "computador",
                "sistema operacional",
                "sistema"
            };

        return termos.Any(
            termo =>
                texto.Contains(
                    termo,
                    StringComparison.OrdinalIgnoreCase));
    }
}

