using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class Planejador : IPlanejador
{
    public PlanoExecucao CriarPlano(
        string objetivo,
        IContexto contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (string.IsNullOrWhiteSpace(objetivo))
        {
            throw new ArgumentException(
                "O objetivo não pode estar vazio.",
                nameof(objetivo));
        }

        var texto =
            objetivo.Trim();

        var etapas =
            CriarEtapas(texto);

        return new PlanoExecucao
        {
            Objetivo = texto,
            Etapas = etapas
        };
    }

    private static IReadOnlyCollection<EtapaPlano> CriarEtapas(
        string objetivo)
    {
        var etapas = new List<EtapaPlano>();

        if (ContemQualquer(
                objetivo,
                "liste os arquivos",
                "listar arquivos",
                "arquivos de"))
        {
            etapas.Add(
                new EtapaPlano
                {
                    Ordem = etapas.Count + 1,
                    Descricao = "Listar arquivos",
                    Ferramenta = "arquivos",
                    Argumentos = ExtrairCaminho(objetivo)
                });
        }

        if (ContemQualquer(
                objetivo,
                "informações do sistema",
                "informacoes do sistema",
                "informações do computador",
                "informacoes do computador"))
        {
            etapas.Add(
                new EtapaPlano
                {
                    Ordem = etapas.Count + 1,
                    Descricao = "Consultar sistema",
                    Ferramenta = "sistema",
                    Argumentos = string.Empty
                });
        }

        if (ContemQualquer(
                objetivo,
                "processos",
                "processos em execução",
                "processos em execucao"))
        {
            etapas.Add(
                new EtapaPlano
                {
                    Ordem = etapas.Count + 1,
                    Descricao = "Consultar processos",
                    Ferramenta = "processos",
                    Argumentos = string.Empty
                });
        }

        if (ContemQualquer(
                objetivo,
                "recupere",
                "recuperar memória",
                "recuperar memoria"))
        {
            etapas.Add(
                new EtapaPlano
                {
                    Ordem = etapas.Count + 1,
                    Descricao = "Recuperar memória",
                    Ferramenta = "memoria",
                    Argumentos =
                        "recuperar " +
                        ExtrairArgumentoMemoria(objetivo)
                });
        }

        if (etapas.Count == 0)
        {
            etapas.Add(
                new EtapaPlano
                {
                    Ordem = 1,
                    Descricao = "Processar objetivo",
                    Ferramenta = string.Empty,
                    Argumentos = objetivo
                });
        }

        return etapas;
    }

    private static string ExtrairCaminho(
        string texto)
    {
        var indice =
            texto.IndexOf(
                "de ",
                StringComparison.OrdinalIgnoreCase);

        if (indice >= 0)
        {
            return texto[(indice + 3)..].Trim(
                ' ',
                ':',
                ',',
                '.');
        }

        return string.Empty;
    }

    private static string ExtrairArgumentoMemoria(
        string texto)
    {
        var palavras =
            texto.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

        var indice =
            Array.FindIndex(
                palavras,
                palavra =>
                    palavra.Equals(
                        "memória",
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    palavra.Equals(
                        "memoria",
                        StringComparison.OrdinalIgnoreCase));

        if (indice >= 0 && indice + 1 < palavras.Length)
        {
            return palavras[indice + 1];
        }

        return string.Empty;
    }

    private static bool ContemQualquer(
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
}
