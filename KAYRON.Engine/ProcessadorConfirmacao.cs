using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class ProcessadorConfirmacao
    : IProcessadorConfirmacao
{
    private readonly IConfirmadorOperacao _confirmador;

    private static readonly string[] Confirmacoes =
    {
        "sim",
        "s",
        "confirmo",
        "confirmar",
        "confirma",
        "pode executar",
        "pode fazer",
        "pode continuar",
        "continue",
        "continuar",
        "sim continue",
        "sim, continue",
        "vamos",
        "vamos continuar",
        "ok",
        "ok continue",
        "ok, continue",
        "por favor",
        "pode prosseguir",
        "execute",
        "executa",
        "pode"
    };

    private static readonly string[] Cancelamentos =
    {
        "não",
        "nao",
        "n",
        "cancelo",
        "cancelar",
        "cancele",
        "não pode",
        "nao pode",
        "não execute",
        "nao execute",
        "não faça",
        "nao faca",
        "não continue",
        "nao continue",
        "espere",
        "espera",
        "pare",
        "parar"
    };

    public ProcessadorConfirmacao(
        IConfirmadorOperacao confirmador)
    {
        ArgumentNullException.ThrowIfNull(confirmador);

        _confirmador = confirmador;
    }

    public bool EhConfirmacao(
        string entrada)
    {
        return Corresponde(
            entrada,
            Confirmacoes);
    }

    public bool EhCancelamento(
        string entrada)
    {
        return Corresponde(
            entrada,
            Cancelamentos);
    }

    public SolicitacaoConfirmacao? ObterPendente(
        IContexto contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        return _confirmador.ObterPendente(
            contexto);
    }

    public bool Confirmar(
        IContexto contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var pendente =
            _confirmador.ObterPendente(
                contexto);

        if (pendente is null)
        {
            return false;
        }

        return _confirmador.Confirmar(
            pendente.Id,
            contexto);
    }

    public bool Cancelar(
        IContexto contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var pendente =
            _confirmador.ObterPendente(
                contexto);

        if (pendente is null)
        {
            return false;
        }

        return _confirmador.Cancelar(
            pendente.Id,
            contexto);
    }

    private static bool Corresponde(
        string entrada,
        IEnumerable<string> termos)
    {
        if (string.IsNullOrWhiteSpace(entrada))
        {
            return false;
        }

        var texto =
            entrada
                .Trim()
                .ToLowerInvariant();

        foreach (var termo in termos)
        {
            if (texto.Equals(
                    termo,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
