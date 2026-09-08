using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class ExecutorPlano : IExecutorPlano
{
    private readonly IOrquestrador _orquestrador;

    public ExecutorPlano(
        IOrquestrador orquestrador)
    {
        ArgumentNullException.ThrowIfNull(orquestrador);

        _orquestrador = orquestrador;
    }

    public async Task<ResultadoPlano> ExecutarAsync(
        PlanoExecucao plano,
        IContexto contexto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plano);
        ArgumentNullException.ThrowIfNull(contexto);

        var resultados =
            new List<Resposta>();

        foreach (var etapa in plano.Etapas.OrderBy(
                     etapa => etapa.Ordem))
        {
            cancellationToken.ThrowIfCancellationRequested();

            contexto.Adicionar(
                "plano_id",
                plano.Id.ToString());

            contexto.Adicionar(
                "etapa_atual",
                etapa.Ordem.ToString());

            contexto.Adicionar(
                "etapa_descricao",
                etapa.Descricao);

            var instrucao =
                CriarInstrucao(etapa);

            var resposta =
                await _orquestrador.ProcessarAsync(
                    instrucao,
                    contexto,
                    cancellationToken);

            resultados.Add(resposta);

            contexto.Adicionar(
                $"etapa_{etapa.Ordem}_resultado",
                resposta.Conteudo);

            if (resposta.Conteudo.StartsWith(
                    "Erro:",
                    StringComparison.OrdinalIgnoreCase))
            {
                return ResultadoPlano.Falha(
                    plano.Objetivo,
                    resposta.Conteudo,
                    resultados);
            }
        }

        return ResultadoPlano.Ok(
            plano.Objetivo,
            resultados);
    }

    private static Instrucao CriarInstrucao(
        EtapaPlano etapa)
    {
        var conteudo =
            string.IsNullOrWhiteSpace(etapa.Ferramenta)
                ? etapa.Argumentos
                : $"/{etapa.Ferramenta} {etapa.Argumentos}".Trim();

        return new Instrucao
        {
            Conteudo = conteudo
        };
    }
}
