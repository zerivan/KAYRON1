namespace KAYRON.Core;

public sealed class ContextoExecucao
{
    public Guid Id { get; } = Guid.NewGuid();

    public DateTimeOffset Inicio { get; } =
        DateTimeOffset.Now;

    public string? Instrucao { get; set; }

    public string? Ferramenta { get; set; }

    public string? Operacao { get; set; }

    public string? Argumentos { get; set; }

    public bool? Sucesso { get; set; }

    public string? Resultado { get; set; }

    public string? Erro { get; set; }

    public DateTimeOffset? Fim { get; set; }

    public TimeSpan? Duracao =>
        Fim.HasValue
            ? Fim.Value - Inicio
            : null;

    public void Concluir(
        bool sucesso,
        string? resultado = null,
        string? erro = null)
    {
        Sucesso = sucesso;
        Resultado = resultado;
        Erro = erro;
        Fim = DateTimeOffset.Now;
    }
}
