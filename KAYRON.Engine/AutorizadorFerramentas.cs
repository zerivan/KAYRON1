using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class AutorizadorFerramentas
    : IAutorizadorFerramentas
{
    public ResultadoAutorizacao Autorizar(
        IFerramenta ferramenta,
        string argumentos,
        string? operacaoSolicitada = null)
    {
        ArgumentNullException.ThrowIfNull(ferramenta);

        var politica =
            ObterPolitica(ferramenta);

        if (!politica.Permitida)
        {
            return ResultadoAutorizacao.Negar(
                $"A ferramenta '{ferramenta.Nome}' está bloqueada pela política de segurança.");
        }

        if (politica.OperacoesPermitidas.Count == 0)
        {
            return ResultadoAutorizacao.Permitir();
        }

        var operacao =
            IdentificarOperacao(
                argumentos,
                politica,
                operacaoSolicitada);

        if (operacao is null)
        {
            return ResultadoAutorizacao.Negar(
                $"A operação solicitada para '{ferramenta.Nome}' não está autorizada.");
        }

        return politica.ExigeConfirmacao
            ? ResultadoAutorizacao.Confirmacao(
                $"A operação '{operacao}' da ferramenta '{ferramenta.Nome}' exige confirmação.")
            : ResultadoAutorizacao.Permitir();
    }

    private static PoliticaFerramenta ObterPolitica(
        IFerramenta ferramenta)
    {
        return ferramenta.Nome.ToLowerInvariant() switch
        {
            "info" =>
                new PoliticaFerramenta
                {
                    Permitida = true,
                    SomenteLeitura = true
                },

            "sistema" =>
                new PoliticaFerramenta
                {
                    Permitida = true,
                    SomenteLeitura = true
                },

            "processos" =>
                new PoliticaFerramenta
                {
                    Permitida = true,
                    SomenteLeitura = true
                },

            "diagnostico" =>
                new PoliticaFerramenta
                {
                    Permitida = true,
                    SomenteLeitura = true
                },

            "arquivos" =>
                new PoliticaFerramenta
                {
                    Permitida = true,
                    SomenteLeitura = true,
                    OperacoesPermitidas =
                    [
                        "listar",
                        "existe",
                        "ler"
                    ]
                },

            "autocorrecao" =>
                new PoliticaFerramenta
                {
                    Permitida = true,
                    SomenteLeitura = false,
                    ExigeConfirmacao = true,
                    OperacoesPermitidas =
                    [
                        "autocorrigir"
                    ]
                },

            "memoria" =>
                new PoliticaFerramenta
                {
                    Permitida = true,
                    SomenteLeitura = false,
                    OperacoesPermitidas =
                    [
                        "guardar",
                        "recuperar",
                        "existe"
                    ]
                },

            _ =>
                new PoliticaFerramenta
                {
                    Permitida = false
                }
        };
    }

    private static string? IdentificarOperacao(
        string argumentos,
        PoliticaFerramenta politica,
        string? operacaoSolicitada)
    {
        if (!string.IsNullOrWhiteSpace(operacaoSolicitada))
        {
            foreach (var operacaoPermitida in politica.OperacoesPermitidas)
            {
                if (operacaoPermitida.Equals(
                        operacaoSolicitada,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return operacaoPermitida;
                }
            }

            return null;
        }

        if (string.IsNullOrWhiteSpace(argumentos))
        {
            return politica.OperacoesPermitidas.Count == 1
                ? politica.OperacoesPermitidas.First()
                : null;
        }

        foreach (var operacao in politica.OperacoesPermitidas)
        {
            if (argumentos.Contains(
                    operacao,
                    StringComparison.OrdinalIgnoreCase))
            {
                return operacao;
            }
        }

        return null;
    }
}
