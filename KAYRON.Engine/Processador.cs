using KAYRON.Core;
using Microsoft.Extensions.Logging;

namespace KAYRON.Engine;

public class Processador : IProcessador
{
    private readonly Interpretador _interpretador;
    private readonly IMemoria _memoria;
    private readonly DispatcherComandos _dispatcher;
    private readonly IDecisor _decisor;
    private readonly ILogger<Processador> _logger;

    public Processador(
        Interpretador interpretador,
        IMemoria memoria,
        DispatcherComandos dispatcher,
        IDecisor decisor,
        ILogger<Processador> logger)
    {
        ArgumentNullException.ThrowIfNull(interpretador);
        ArgumentNullException.ThrowIfNull(memoria);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(decisor);
        ArgumentNullException.ThrowIfNull(logger);

        _interpretador = interpretador;
        _memoria = memoria;
        _dispatcher = dispatcher;
        _decisor = decisor;
        _logger = logger;
    }

    public async Task<Resposta> ExecutarAsync(
        Instrucao instrucao,
        IContexto contexto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrucao);
        ArgumentNullException.ThrowIfNull(contexto);

        var conteudo =
            instrucao.Conteudo.Trim();

        if (string.IsNullOrWhiteSpace(conteudo))
        {
            _logger.LogWarning(
                "KAYRON recebeu uma instrução vazia.");

            return new Resposta
            {
                Conteudo =
                    "KAYRON não recebeu nenhuma instrução."
            };
        }

        var intencao =
            _interpretador.Interpretar(instrucao);

        contexto.Adicionar(
            "ultima_instrucao",
            conteudo);

        contexto.Adicionar(
            "ultima_intencao",
            intencao.ToString());

        _memoria.Guardar(
            "ultima_instrucao",
            conteudo);

        _memoria.Guardar(
            "ultima_intencao",
            intencao.ToString());

        if (intencao == Intencao.Comando)
        {
            return ExecutarComando(
                conteudo,
                contexto);
        }

        var ciclo =
            contexto as ContextoCiclo
            ?? new ContextoCiclo(contexto);

        var decisao =
            await _decisor.DecidirAsync(
                instrucao,
                ciclo,
                cancellationToken);

        return new Resposta
        {
            Conteudo =
                $"Decisão: {decisao.Acao}."
        };
    }

    private Resposta ExecutarComando(
        string conteudo,
        IContexto contexto)
    {
        var partes =
            conteudo[1..].Split(
                ' ',
                2);

        var nomeComando =
            partes[0].Trim();

        var argumentos =
            partes.Length > 1
                ? partes[1].Trim()
                : string.Empty;

        if (string.IsNullOrWhiteSpace(
                nomeComando))
        {
            return new Resposta
            {
                Conteudo =
                    "KAYRON recebeu um comando vazio."
            };
        }

        var comando =
            _dispatcher.Obter(
                nomeComando);

        if (comando is null)
        {
            return new Resposta
            {
                Conteudo =
                    $"KAYRON não reconhece o comando: /{nomeComando}"
            };
        }

        return comando.Executar(
            argumentos,
            contexto);
    }
}
