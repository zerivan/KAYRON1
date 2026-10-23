using System.Globalization;

namespace KAYRON.Engine;

public sealed class MotorMatematico
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    public ResultadoMatematico Calcular(NoAst raiz, string expressaoOriginal)
    {
        if (raiz == null)
            throw new ArgumentNullException(nameof(raiz));

        if (expressaoOriginal == null)
            throw new ArgumentNullException(nameof(expressaoOriginal));

        var etapas = new List<string>();

        try
        {
            var valor = Avaliar(raiz, etapas);

            return new ResultadoMatematico(
                valor,
                etapas,
                expressaoOriginal,
                true);
        }
        catch (DivideByZeroException)
        {
            return new ResultadoMatematico(
                0m,
                etapas,
                expressaoOriginal,
                false,
                "Não é possível dividir por zero.");
        }
        catch (OverflowException)
        {
            return new ResultadoMatematico(
                0m,
                etapas,
                expressaoOriginal,
                false,
                "O resultado excede o limite numérico suportado.");
        }
        catch (ArithmeticException ex)
        {
            return new ResultadoMatematico(
                0m,
                etapas,
                expressaoOriginal,
                false,
                ex.Message);
        }
    }

    private decimal Avaliar(NoAst no, List<string> etapas)
    {
        switch (no)
        {
            case Numero numero:
                return numero.Valor;

            case Agrupamento agrupamento:
                return Avaliar(agrupamento.Expressao, etapas);

            case OperacaoUnaria unaria:
            {
                var valor = Avaliar(unaria.Operando, etapas);
                var resultado = unaria.Operador switch
                {
                    "+" => valor,
                    "-" => -valor,
                    _ => throw new ArithmeticException(
                        $"Operador unário desconhecido: {unaria.Operador}")
                };

                if (unaria.Operador == "-")
                    etapas.Add(
                        $"{Formatar(valor)} → {Formatar(resultado)}");

                return resultado;
            }

            case Potencia potencia:
            {
                var baseValor = Avaliar(potencia.Base, etapas);
                var expoente = Avaliar(potencia.Expoente, etapas);
                var resultado = CalcularPotencia(baseValor, expoente);

                etapas.Add(
                    $"{Formatar(baseValor)} ^ {Formatar(expoente)} = {Formatar(resultado)}");

                return resultado;
            }

            case OperacaoBinaria binaria:
            {
                var esquerda = Avaliar(binaria.Esquerda, etapas);
                var direita = Avaliar(binaria.Direita, etapas);

                var resultado = binaria.Operador switch
                {
                    "+" => esquerda + direita,
                    "-" => esquerda - direita,
                    "*" or "×" or "x" => esquerda * direita,
                    "/" or "÷" => Dividir(esquerda, direita),
                    "%" => Modulo(esquerda, direita),
                    _ => throw new ArithmeticException(
                        $"Operador desconhecido: {binaria.Operador}")
                };

                etapas.Add(
                    $"{Formatar(esquerda)} {binaria.Operador} {Formatar(direita)} = {Formatar(resultado)}");

                return resultado;
            }

            default:
                throw new ArithmeticException(
                    $"Tipo de nó matemático não suportado: {no.GetType().Name}");
        }
    }

    private static decimal Dividir(decimal esquerda, decimal direita)
    {
        if (direita == 0m)
            throw new DivideByZeroException();

        return esquerda / direita;
    }

    private static decimal Modulo(decimal esquerda, decimal direita)
    {
        if (direita == 0m)
            throw new DivideByZeroException();

        return esquerda % direita;
    }

    private static decimal CalcularPotencia(decimal baseValor, decimal expoente)
    {
        if (expoente != decimal.Truncate(expoente))
            throw new ArithmeticException(
                "Nesta fase, o expoente deve ser um número inteiro.");

        if (expoente > int.MaxValue || expoente < int.MinValue)
            throw new OverflowException();

        var expoenteInteiro = (int)expoente;

        if (expoenteInteiro == 0)
            return 1m;

        var negativo = expoenteInteiro < 0;
        var quantidade = Math.Abs((long)expoenteInteiro);
        decimal resultado = 1m;

        for (long i = 0; i < quantidade; i++)
            resultado *= baseValor;

        return negativo
            ? Dividir(1m, resultado)
            : resultado;
    }

    private static string Formatar(decimal valor)
    {
        return valor.ToString("0.############################", Cultura);
    }
}
