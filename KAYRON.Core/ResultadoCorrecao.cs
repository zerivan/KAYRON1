namespace KAYRON.Core;

public sealed class ResultadoCorrecao
{
    public bool Sucesso { get; init; }
    public string Arquivo { get; init; } = string.Empty;
    public string Backup { get; init; } = string.Empty;
    public string Mensagem { get; init; } = string.Empty;
    public string? Erro { get; init; }

    public static ResultadoCorrecao Ok(
        string arquivo,
        string backup,
        string mensagem) =>
        new()
        {
            Sucesso = true,
            Arquivo = arquivo,
            Backup = backup,
            Mensagem = mensagem
        };

    public static ResultadoCorrecao Falha(
        string arquivo,
        string erro) =>
        new()
        {
            Sucesso = false,
            Arquivo = arquivo,
            Erro = erro,
            Mensagem = "A correção não foi aplicada."
        };
}
