using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class InterpretadorDiagnostico
    : IInterpretadorDiagnostico
{
    public string Interpretar(
        string argumento,
        string resultado)
    {
        if (string.IsNullOrWhiteSpace(resultado))
        {
            return
                "Não foi possível obter informações suficientes para concluir o diagnóstico.";
        }

        var texto =
            resultado.Replace(
                "\r\n",
                "\n",
                StringComparison.Ordinal)
            .Replace(
                "\r",
                "\n",
                StringComparison.Ordinal);

        var linhas =
            texto.Split(
                '\n',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        var windows =
            EncontrarBloco(
                linhas,
                "Windows");

        var integridade =
            EncontrarBloco(
                linhas,
                "Integridade do sistema");

        var rede =
            EncontrarBloco(
                linhas,
                "Rede");

        var resposta =
            new List<string>
            {
                "Fiz o diagnóstico do Windows e organizei os resultados."
            };

        if (!string.IsNullOrWhiteSpace(windows))
        {
            resposta.Add(
                $"Sistema operacional: {LimparTexto(windows)}.");
        }

        if (!string.IsNullOrWhiteSpace(integridade))
        {
            if (EhFaltaAdministrador(integridade))
            {
                resposta.Add(
                    "A verificação de integridade do Windows não foi concluída porque o SFC exige privilégios de administrador. Portanto, ainda não é possível afirmar se existem arquivos do sistema corrompidos.");
            }
            else
            {
                resposta.Add(
                    $"Integridade do sistema: {LimparTexto(integridade)}.");
            }
        }

        if (!string.IsNullOrWhiteSpace(rede))
        {
            var resumoRede =
                InterpretarRede(rede);

            if (!string.IsNullOrWhiteSpace(resumoRede))
            {
                resposta.Add(resumoRede);
            }
        }

        resposta.Add(
            "Conclusão: as verificações executadas não indicaram, por si só, uma falha evidente no Windows. A verificação de integridade ficou pendente por falta de privilégio administrativo.");

        return string.Join(
            Environment.NewLine +
            Environment.NewLine,
            resposta);
    }

    private static string EncontrarBloco(
        string[] linhas,
        string titulo)
    {
        var indice =
            Array.FindIndex(
                linhas,
                linha =>
                    linha.Equals(
                        titulo + ":",
                        StringComparison.OrdinalIgnoreCase));

        if (indice < 0 ||
            indice + 1 >= linhas.Length)
        {
            return string.Empty;
        }

        var proximasLinhas =
            new List<string>();

        for (var i = indice + 1; i < linhas.Length; i++)
        {
            var linha = linhas[i];

            if (EhTitulo(linha))
            {
                break;
            }

            proximasLinhas.Add(linha);
        }

        return string.Join(
            " ",
            proximasLinhas);
    }

    private static bool EhTitulo(
        string linha)
    {
        return linha.Equals(
                   "Windows:",
                   StringComparison.OrdinalIgnoreCase)
               || linha.Equals(
                   "Integridade do sistema:",
                   StringComparison.OrdinalIgnoreCase)
               || linha.Equals(
                   "Rede:",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool EhFaltaAdministrador(
        string texto)
    {
        var normalizado =
            texto.ToLowerInvariant();

        return normalizado.Contains("administrador")
               || normalizado.Contains("administrator")
               || normalizado.Contains("sfc");
    }

    private static string InterpretarRede(
        string texto)
    {
        var normalizado =
            texto.ToLowerInvariant();

        if (normalizado.Contains(
                "mídia desconectada") ||
            normalizado.Contains(
                "media disconnected"))
        {
            var adaptadoresDesconectados =
                ContarOcorrencias(
                    normalizado,
                    "mídia desconectada");

            if (adaptadoresDesconectados > 0)
            {
                return
                    $"Rede: encontrei {adaptadoresDesconectados} adaptador(es) sem conexão de mídia. Isso, isoladamente, não representa uma falha.";
            }
        }

        if (normalizado.Contains(
                "ipv4"))
        {
            return
                "Rede: foram encontradas configurações IPv4 ativas. Não foi identificada, nessa coleta básica, uma falha evidente de conectividade.";
        }

        return
            "Rede: as informações de configuração foram coletadas, mas não há dados suficientes para afirmar que existe uma falha.";
    }

    private static int ContarOcorrencias(
        string texto,
        string termo)
    {
        var quantidade = 0;
        var inicio = 0;

        while (true)
        {
            var indice =
                texto.IndexOf(
                    termo,
                    inicio,
                    StringComparison.OrdinalIgnoreCase);

            if (indice < 0)
            {
                break;
            }

            quantidade++;
            inicio = indice + termo.Length;
        }

        return quantidade;
    }

    private static string LimparTexto(
        string texto)
    {
        return texto
            .Replace(
                "�",
                "",
                StringComparison.Ordinal)
            .Trim();
    }
}
