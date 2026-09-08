using System.Text.Json;
using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class ConfirmadorOperacao : IConfirmadorOperacao
{
    private const string ChaveConfirmacao =
        "confirmacao_pendente";

    private readonly object _lock = new();

    public SolicitacaoConfirmacao Criar(
        string ferramenta,
        string operacao,
        string argumentos,
        string motivo,
        IContexto contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var solicitacao = new SolicitacaoConfirmacao
        {
            Ferramenta = ferramenta,
            Operacao = operacao,
            Argumentos = argumentos,
            Motivo = motivo
        };

        var dados =
            JsonSerializer.Serialize(solicitacao);

        lock (_lock)
        {
            contexto.Adicionar(
                ChaveConfirmacao,
                dados);

            contexto.Adicionar(
                "confirmacao_status",
                "pendente");

            contexto.Adicionar(
                "confirmacao_id",
                solicitacao.Id);
        }

        return solicitacao;
    }

    public bool Confirmar(
        string id,
        IContexto contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var pendente =
            ObterPendente(contexto);

        if (pendente is null ||
            !pendente.Id.Equals(
                id,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        contexto.Adicionar(
            ChaveConfirmacao,
            string.Empty);

        contexto.Adicionar(
            "confirmacao_status",
            "confirmada");

        contexto.Adicionar(
            "confirmacao_id",
            id);

        return true;
    }

    public bool Cancelar(
        string id,
        IContexto contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var pendente =
            ObterPendente(contexto);

        if (pendente is null ||
            !pendente.Id.Equals(
                id,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        contexto.Adicionar(
            ChaveConfirmacao,
            string.Empty);

        contexto.Adicionar(
            "confirmacao_status",
            "cancelada");

        contexto.Adicionar(
            "confirmacao_id",
            id);

        return true;
    }

    public SolicitacaoConfirmacao? ObterPendente(
        IContexto contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var dados =
            contexto.Obter(
                ChaveConfirmacao);

        if (string.IsNullOrWhiteSpace(dados))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SolicitacaoConfirmacao>(
                dados);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
