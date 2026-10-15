using System.Text;

namespace KAYRON.Engine;

public sealed class ExplicadorMatematico
{
    public ExplicacaoMatematica Explicar(ResultadoMatematico resultado)
    {
        if (!resultado.Valido)
        {
            return new ExplicacaoMatematica(
                resultado.ExpressaoOriginal,
                resultado.Erro ?? "Não foi possível calcular a expressão.",
                resultado.ValorFinal,
                false);
        }

        var texto = new StringBuilder();

        texto.AppendLine("=== Expressão original ===");
        texto.AppendLine(resultado.ExpressaoOriginal);

        AdicionarEstrutura(texto, resultado.ExpressaoOriginal);

        if (resultado.Etapas.Count > 0)
        {
            texto.AppendLine();
            texto.AppendLine("=== Passo a passo: ===");

            for (var i = 0; i < resultado.Etapas.Count; i++)
            {
                var etapa = resultado.Etapas[i];

                texto.AppendLine();
                texto.AppendLine($"Etapa {i + 1}:");
                texto.AppendLine($"  {etapa}");

                var explicacao = ExplicarEtapa(etapa);

                if (!string.IsNullOrWhiteSpace(explicacao))
                    texto.AppendLine($"  → {explicacao}");
            }
        }

        texto.AppendLine();
        texto.AppendLine("=== Resultado final ===");
        texto.Append($"Resultado final: {resultado.ValorFinal}");

        return new ExplicacaoMatematica(
            resultado.ExpressaoOriginal,
            texto.ToString().Trim(),
            resultado.ValorFinal,
            true);
    }

    private static void AdicionarEstrutura(
        StringBuilder texto,
        string expressao)
    {
        var temParenteses =
            expressao.Contains('(') ||
            expressao.Contains(')');

        var temPotencia = expressao.Contains('^');
        var temMultiplicacaoOuDivisao =
            expressao.Contains('*') ||
            expressao.Contains('×') ||
            expressao.Contains('x') ||
            expressao.Contains('/') ||
            expressao.Contains('÷');
        var temModulo =
            expressao.Contains('%');
        var temAdicaoOuSubtracao =
            expressao.Contains('+') ||
            expressao.Contains('-');

        texto.AppendLine();
        texto.AppendLine("Ordem de cálculo:");

        if (temParenteses)
            texto.AppendLine("1. Parênteses — calculamos primeiro o que está dentro deles.");

        var numero = temParenteses ? 2 : 1;

        if (temPotencia)
            texto.AppendLine($"{numero++}. Potências — calculamos a base elevada ao expoente.");

        if (temMultiplicacaoOuDivisao)
            texto.AppendLine($"{numero++}. Multiplicação e divisão — têm prioridade sobre adição e subtração.");

        if (temModulo)
            texto.AppendLine($"{numero++}. Resto da divisão — calculamos o resto entre dois valores.");

        if (temAdicaoOuSubtracao)
            texto.AppendLine($"{numero}. Adição e subtração — são realizadas depois das operações de maior prioridade.");
    }

    private static string? ExplicarEtapa(string etapa)
    {
        if (etapa.Contains(" ^ ", StringComparison.Ordinal))
            return "Elevamos a base ao expoente.";

        if (etapa.Contains(" × ", StringComparison.Ordinal) ||
            etapa.Contains(" x ", StringComparison.Ordinal) ||
            etapa.Contains(" * ", StringComparison.Ordinal))
            return "Multiplicamos os fatores.";

        if (etapa.Contains(" % ", StringComparison.Ordinal))
            return "Calculamos o resto da divisão entre os valores.";

        if (etapa.Contains(" ÷ ", StringComparison.Ordinal) ||
            etapa.Contains(" / ", StringComparison.Ordinal))
            return "Dividimos o primeiro valor pelo segundo.";

        if (etapa.Contains(" + ", StringComparison.Ordinal))
            return "Somamos os valores.";

        if (etapa.Contains(" - ", StringComparison.Ordinal))
            return "Subtraímos o segundo valor do primeiro.";

        if (etapa.Contains(" → ", StringComparison.Ordinal))
            return "Aplicamos o sinal negativo ao valor.";

        return null;
    }
}
