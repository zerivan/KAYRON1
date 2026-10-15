using ElBruno.LocalEmbeddings;

namespace KAYRON.Engine;

public sealed class EmbeddingMemoriaLocal : IEmbeddingMemoria
{
    private static readonly Lazy<Task<LocalEmbeddingGenerator>> Gerador =
        new(() => LocalEmbeddingGenerator.CreateAsync());

    public string Modelo => "ElBruno.LocalEmbeddings-local";

    public float[] Gerar(string texto)
    {
        // Compatibilidade síncrona mínima: delega para a versão assíncrona.
        return GerarAsync(texto).GetAwaiter().GetResult();
    }

    public async Task<float[]> GerarAsync(
        string texto,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var gerador = await Gerador.Value.ConfigureAwait(false);
        var embedding = await gerador.GenerateEmbeddingAsync(texto).ConfigureAwait(false);
        return embedding.Vector.ToArray();
    }
}
