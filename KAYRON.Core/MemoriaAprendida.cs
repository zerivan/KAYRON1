namespace KAYRON.Core;

public class MemoriaAprendida
{
    public string Chave { get; init; } = string.Empty;
    public string Valor { get; init; } = string.Empty;
    public string Tipo { get; init; } = "fato";
    public int Importancia { get; init; } = 3;
    public bool Explicita { get; init; }
    public string Origem { get; init; } = "agente";
    public IReadOnlyCollection<string> Tags { get; init; } = Array.Empty<string>();
    public DateTime AprendidoEm { get; init; } = DateTime.UtcNow;
    public DateTime AtualizadoEm { get; init; } = DateTime.UtcNow;
    public DateTime? UltimaRecuperacaoEm { get; set; }
    public float[]? Embedding { get; init; }
    public string? EmbeddingModelo { get; init; }
}
