namespace KAYRON.Core;

public sealed class ResultadoCicloCorrecao
{
    public bool Sucesso { get; init; }
    public bool AlteracaoAplicada { get; init; }
    public bool ValidacaoPassou { get; init; }
    public bool RollbackExecutado { get; init; }
    public int Tentativas { get; init; }
    public string Arquivo { get; init; } = string.Empty;
    public string Backup { get; init; } = string.Empty;
    public string Mensagem { get; init; } = string.Empty;
}
