using ElBruno.LocalEmbeddings;

namespace KAYRON.Engine;

public sealed class EmbeddingMemoriaLocal : IEmbeddingMemoria
{
    private static readonly Lazy<Task<LocalEmbeddingGenerator>> Gerador =
        new(() => LocalEmbeddingGenerator.CreateAsync());

    public string Modelo => "ElBruno.LocalEmbeddings-local";

    public float[] Gerar(string texto)
    {
        var gerador = Gerador.Value.GetAwaiter().GetResult();
        var embedding = gerador.GenerateEmbeddingAsync(texto).GetAwaiter().GetResult();
        return embedding.Vector.ToArray();
    }

    public Task<float[]> GerarAsync(
        string texto,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Gerar(texto));
    }
}
