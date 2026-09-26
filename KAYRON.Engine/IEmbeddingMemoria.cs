namespace KAYRON.Engine;

public interface IEmbeddingMemoria
{
    string Modelo { get; }
    Task<float[]> GerarAsync(string texto, CancellationToken cancellationToken = default);
}
