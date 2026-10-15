namespace KAYRON.Engine;

public abstract class NoAst
{
}

public sealed class Numero : NoAst
{
    public decimal Valor { get; }

    public Numero(decimal valor)
    {
        Valor = valor;
    }
}

public sealed class OperacaoBinaria : NoAst
{
    public string Operador { get; }
    public NoAst Esquerda { get; }
    public NoAst Direita { get; }

    public OperacaoBinaria(string operador, NoAst esquerda, NoAst direita)
    {
        Operador = operador;
        Esquerda = esquerda;
        Direita = direita;
    }
}

public sealed class OperacaoUnaria : NoAst
{
    public string Operador { get; }
    public NoAst Operando { get; }

    public OperacaoUnaria(string operador, NoAst operando)
    {
        Operador = operador;
        Operando = operando;
    }
}

public sealed class Potencia : NoAst
{
    public NoAst Base { get; }
    public NoAst Expoente { get; }

    public Potencia(NoAst baseNo, NoAst expoente)
    {
        Base = baseNo;
        Expoente = expoente;
    }
}

public sealed class Agrupamento : NoAst
{
    public NoAst Expressao { get; }

    public Agrupamento(NoAst expressao)
    {
        Expressao = expressao;
    }
}
