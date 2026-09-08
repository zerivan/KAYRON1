using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class ResolvedorConversa
{
    public ResultadoResolucaoConversa Resolver(
        string objetivo,
        IContexto contexto,
        IContextoProjeto contextoProjeto,
        IntencaoDetectada intencao)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(contextoProjeto);
        ArgumentNullException.ThrowIfNull(intencao);

        if (string.IsNullOrWhiteSpace(objetivo))
        {
            return new ResultadoResolucaoConversa
            {
                Resposta = "O que você deseja que eu faça?"
            };
        }

        var texto = Normalizar(objetivo);
        texto = CorrigirVariacoesConhecidas(texto);

        if (!intencao.Identificada)
        {
            var ferramentaLocal = ResolverFerramentaLocal(texto);

            if (ferramentaLocal is not null)
            {
                intencao = new IntencaoDetectada
                {
                    Ferramenta = ferramentaLocal.Value.Ferramenta,
                    Operacao = ferramentaLocal.Value.Operacao,
                    Confianca = ferramentaLocal.Value.Confianca,
                    Identificada = true,
                    Evidencia = ferramentaLocal.Value.Evidencia
                };
            }
        }

        if (intencao.Identificada)
        {
            if (intencao.Ferramenta.Equals(
                    "autocorrecao",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (contextoProjeto.Atual?.Identificado == true)
                {
                    return new ResultadoResolucaoConversa
                    {
                        Intencao = intencao
                    };
                }

                if (TemAlvoExplicito(texto))
                {
                    return new ResultadoResolucaoConversa
                    {
                        Intencao = intencao
                    };
                }

                return new ResultadoResolucaoConversa
                {
                    Resposta =
                        "Qual projeto você quer que eu analise e corrija?"
                };
            }

            return new ResultadoResolucaoConversa
            {
                Intencao = intencao
            };
        }

        if (EhPedidoDeProjeto(texto))
        {
            if (RefereSeAoProjetoAtual(texto))
            {
                if (contextoProjeto.Atual?.Identificado == true)
                {
                    return new ResultadoResolucaoConversa
                    {
                        Intencao = intencao
                    };
                }

                return new ResultadoResolucaoConversa
                {
                    Resposta =
                        "Não tenho um projeto atual identificado. Qual projeto você quer que eu analise?"
                };
            }

            if (!TemAlvoExplicito(texto))
            {
                return new ResultadoResolucaoConversa
                {
                    Resposta =
                        "Qual projeto você quer que eu analise?"
                };
            }
        }

        if (EhPedidoDeWindows(texto))
        {
            return new ResultadoResolucaoConversa
            {
                Intencao = new IntencaoDetectada
                {
                    Ferramenta = "diagnostico",
                    Operacao = "diagnosticar",
                    Confianca = 0.95,
                    Identificada = true,
                    Evidencia =
                        "pedido explícito de diagnóstico do Windows"
                }
            };
        }

        if (EhPedidoDeArquivos(texto))
        {
            return new ResultadoResolucaoConversa
            {
                Intencao = new IntencaoDetectada
                {
                    Ferramenta = "arquivos",
                    Operacao = "listar",
                    Confianca = 0.90,
                    Identificada = true,
                    Evidencia =
                        "pedido relacionado a arquivos"
                }
            };
        }

        return new ResultadoResolucaoConversa();
    }
    private static string CorrigirVariacoesConhecidas(string texto)
    {
        var variacoes = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["arqivos"] = "arquivos",
            ["arqivo"] = "arquivo",
            ["progeto"] = "projeto",
            ["progetos"] = "projetos",
            ["codgio"] = "codigo",
            ["cogido"] = "codigo",
            ["codgo"] = "codigo",
            ["diretorios"] = "diretorios",
            ["diretorio"] = "diretorio",
            ["repositorio"] = "repositorio",
            ["solucao"] = "solucao",
            ["janelas"] = "windows"
        };

        var palavras = texto.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < palavras.Length; i++)
        {
            var palavra = palavras[i];
            var inicio = palavra.TrimStart(
                '.', ',', ';', ':', '!', '?');
            var fim = inicio.TrimEnd(
                '.', ',', ';', ':', '!', '?');

            if (variacoes.TryGetValue(fim, out var corrigida))
            {
                var prefixo = palavra.Substring(
                    0,
                    palavra.Length - inicio.Length);

                var sufixo = inicio.Substring(
                    fim.Length);

                palavras[i] = prefixo + corrigida + sufixo;
            }
        }

        return string.Join(' ', palavras);
    }
    private static (string Ferramenta, string Operacao, double Confianca, string Evidencia)? ResolverFerramentaLocal(
        string texto)
    {
        if (EhPedidoDeProjeto(texto))
        {
            if (EhPedidoDeCorrecao(texto))
            {
                return (
                    "autocorrecao",
                    "autocorrigir",
                    0.95,
                    "pedido explícito de correção de projeto"
                );
            }

            return (
                "diagnostico",
                "analisar",
                0.95,
                "pedido de análise de projeto"
            );
        }

        if (EhPedidoDeWindows(texto))
        {
            return (
                "diagnostico",
                "diagnosticar",
                0.95,
                "pedido relacionado ao Windows"
            );
        }

        if (EhPedidoDeCodigo(texto))
        {
            if (EhPedidoDeCorrecao(texto))
            {
                return (
                    "autocorrecao",
                    "autocorrigir",
                    0.90,
                    "pedido explícito de correção de código"
                );
            }

            return (
                "diagnostico",
                "analisar",
                0.90,
                "pedido de análise de código"
            );
        }

        if (EhPedidoDeArquivos(texto))
        {
            return null;
        }

        return null;
    }
    private static bool EhPedidoDeCorrecao(string texto)
    {
        return ContemAlgum(
            texto,
            "corrigir",
            "corrija",
            "corrige",
            "consertar",
            "conserta",
            "conserte",
            "arrumar",
            "arruma",
            "arrume",
            "resolver erro",
            "resolver os erros",
            "corrigir erro",
            "corrigir os erros",
            "autocorrecao",
            "autocorreção");
    }
    private static bool EhPedidoDeProjeto(string texto)
    {
        return ContemAlgum(
            texto,
            "projeto",
            "projetos",
            "solucao",
            "codigo fonte",
            "repositorio",
            "repositorio git");
    }

    private static bool EhPedidoDeWindows(string texto)
    {
        return ContemAlgum(
            texto,
            "windows",
            "sistema operacional",
            "computador",
            "pc",
            "diagnostico do sistema",
            "diagnostico do windows",
            "problema no windows",
            "erro no windows",
            "diagnostico no windows",
            "diagnostico do pc",
            "diagnostico do computador",
            "problema no pc",
            "erro no pc",
            "verificar windows",
            "verifique o windows",
            "analisar windows",
            "analise o windows");
    }

    private static bool EhPedidoDeCodigo(string texto)
    {
        return ContemAlgum(
            texto,
            "codigo",
            "programacao",
            "classe",
            "metodo",
            "funcao",
            "bug",
            "erro no codigo",
            "programar",
            "analisa o codigo",
            "analise o codigo",
            "analisar o codigo",
            "verifica o codigo",
            "verifique o codigo",
            "corrige o codigo",
            "corrigir o codigo",
            "encontra o erro",
            "encontre o erro");
    }

    private static bool EhPedidoDeArquivos(string texto)
    {
        return ContemAlgum(
            texto,
            "arquivo",
            "arquivos",
            "pasta",
            "pastas",
            "documento",
            "documentos",
            "diretorio",
            "diretorios",
            "listar arquivos",
            "liste os arquivos",
            "mostrar arquivos",
            "mostre os arquivos",
            "listar pastas",
            "liste as pastas",
            "mostrar pastas",
            "mostre as pastas",
            "procurar arquivo",
            "procure o arquivo");
    }

    private static bool RefereSeAoProjetoAtual(string texto)
    {
        return ContemAlgum(
            texto,
            "este projeto",
            "esse projeto",
            "projeto atual",
            "neste projeto",
            "nesse projeto",
            "analisa esse projeto",
            "analise esse projeto",
            "analisar esse projeto",
            "olha esse projeto",
            "olhe esse projeto",
            "verifica esse projeto",
            "verifique esse projeto",
            "meu projeto");
    }

    private static bool TemAlvoExplicito(string texto)
    {
        if (RefereSeAoProjetoAtual(texto))
        {
            return true;
        }

        if (texto.Contains(
                "c:\\",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (texto.Contains(
                ".csproj",
                StringComparison.OrdinalIgnoreCase) ||
            texto.Contains(
                ".sln",
                StringComparison.OrdinalIgnoreCase) ||
            texto.Contains(
                ".slnx",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool ContemAlgum(
        string texto,
        params string[] termos)
    {
        foreach (var termo in termos)
        {
            if (texto.Contains(
                    termo,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalizar(string texto)
    {
        return texto
            .Trim()
            .ToLowerInvariant()
            .Replace('á', 'a')
            .Replace('à', 'a')
            .Replace('ã', 'a')
            .Replace('â', 'a')
            .Replace('ä', 'a')
            .Replace('é', 'e')
            .Replace('è', 'e')
            .Replace('ê', 'e')
            .Replace('ë', 'e')
            .Replace('í', 'i')
            .Replace('ì', 'i')
            .Replace('î', 'i')
            .Replace('ï', 'i')
            .Replace('ó', 'o')
            .Replace('ò', 'o')
            .Replace('õ', 'o')
            .Replace('ô', 'o')
            .Replace('ö', 'o')
            .Replace('ú', 'u')
            .Replace('ù', 'u')
            .Replace('û', 'u')
            .Replace('ü', 'u')
            .Replace('ç', 'c');
    }
}








