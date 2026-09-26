using System.Text.Json;
using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class ContextoCiclo : IContexto
{
    private const string ChaveHistorico = "ciclo:historico_dados";

    private readonly IContexto _fonte;
    private readonly Dictionary<string, string> _dados;
    private readonly List<RegistroDecisao> _historico;

    public IReadOnlyDictionary<string, string> Dados => _dados;
    public IReadOnlyList<RegistroDecisao> HistoricoDecisoes => _historico;
    public int PassoAtual => _historico.Count;

    public ContextoCiclo(IContexto contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        _fonte = contexto;

        _dados =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        _historico =
            new List<RegistroDecisao>();

        CopiarContextoExistente(contexto);

        Adicionar(
            "ciclo:passo_atual",
            PassoAtual.ToString());

        Adicionar(
            "ciclo:historico_total",
            HistoricoDecisoes.Count.ToString());
    }

    public void Adicionar(
        string chave,
        string valor)
    {
        if (string.IsNullOrWhiteSpace(chave))
        {
            return;
        }

        var valorSeguro =
            valor ?? string.Empty;

        _dados[chave] = valorSeguro;

        _fonte.Adicionar(
            chave,
            valorSeguro);
    }

    public string? Obter(
        string chave)
    {
        if (string.IsNullOrWhiteSpace(chave))
        {
            return null;
        }

        if (_dados.TryGetValue(
                chave,
                out var valor))
        {
            return valor;
        }

        return _fonte.Obter(chave);
    }

    public void RegistrarDecisao(
        string acao,
        string? ferramenta,
        string? operacao,
        string? resultado,
        bool concluido)
    {
        var registro =
            new RegistroDecisao
            {
                Passo = _historico.Count + 1,
                Acao = acao,
                Ferramenta = ferramenta ?? string.Empty,
                Operacao = operacao ?? string.Empty,
                Resultado = resultado ?? string.Empty,
                Concluido = concluido
            };

        _historico.Add(registro);

        PersistirHistorico();

        Adicionar(
            "ciclo:passo_atual",
            PassoAtual.ToString());

        Adicionar(
            "ciclo:historico_total",
            HistoricoDecisoes.Count.ToString());

        Adicionar(
            "ciclo:ultima_acao",
            registro.Acao);

        Adicionar(
            "ciclo:ultima_ferramenta",
            registro.Ferramenta);

        Adicionar(
            "ciclo:ultima_operacao",
            registro.Operacao);

        Adicionar(
            "ciclo:ultimo_resultado",
            registro.Resultado);
    }

    public bool AcaoJaExecutada(
        string ferramenta,
        string operacao)
    {
        return _historico.Any(
            x =>
                x.Concluido &&
                x.Ferramenta.Equals(
                    ferramenta,
                    StringComparison.OrdinalIgnoreCase) &&
                x.Operacao.Equals(
                    operacao,
                    StringComparison.OrdinalIgnoreCase));
    }

    public void ConsolidarEm(
        IContexto destino)
    {
        ArgumentNullException.ThrowIfNull(destino);

        foreach (var item in _dados)
        {
            destino.Adicionar(
                item.Key,
                item.Value);
        }

        destino.Adicionar(
            ChaveHistorico,
            JsonSerializer.Serialize(
                _historico));
    }

    private void CopiarContextoExistente(
        IContexto contexto)
    {
        var chaves =
            new[]
            {
                "mensagem_atual",
                "instrucao_resolvida",
                "instrucao_interpretada",
                "contexto_conversacional",
                "idioma_codigo",
                "idioma_nome",
                "idioma_confianca",
                "intencao_detectada",
                "intencao_confianca",
                "intencao_evidencia",
                "ultima_resposta",
                "ultima_origem_ferramenta",
                "ultima_origem_operacao",
                "ultima_origem_resultado",
                "diretorio_projeto",
                "arquivo_projeto",
                "solucao",
                "tipo_projeto",
                "decisao:memoria:encontrada",
                "decisao:memoria:chave",
                "decisao:memoria:valor",
                "memoria:instrucao",
                "memoria:ultima_instrucao",
                "memoria:ultima_intencao",
                "memoria:aprendidas_total"
            };

        foreach (var chave in chaves)
        {
            var valor =
                contexto.Obter(chave);

            if (!string.IsNullOrWhiteSpace(valor))
            {
                _dados[chave] = valor;
            }
        }
    }

    private List<RegistroDecisao> CarregarHistorico(
        IContexto contexto)
    {
        var serializado =
            contexto.Obter(ChaveHistorico);

        if (string.IsNullOrWhiteSpace(serializado))
        {
            return new List<RegistroDecisao>();
        }

        try
        {
            return JsonSerializer.Deserialize<
                       List<RegistroDecisao>>(
                       serializado)
                   ?? new List<RegistroDecisao>();
        }
        catch
        {
            return new List<RegistroDecisao>();
        }
    }

    private void PersistirHistorico()
    {
        _fonte.Adicionar(
            ChaveHistorico,
            JsonSerializer.Serialize(
                _historico));
    }
}

public sealed class RegistroDecisao
{
    public int Passo { get; init; }

    public string Acao { get; init; } =
        string.Empty;

    public string Ferramenta { get; init; } =
        string.Empty;

    public string Operacao { get; init; } =
        string.Empty;

    public string Resultado { get; init; } =
        string.Empty;

    public bool Concluido { get; init; }
}

