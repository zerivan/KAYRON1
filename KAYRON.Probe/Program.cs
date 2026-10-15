using KAYRON.Core;
using KAYRON.Engine;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

var options = Options.Create(new PostgresMemoriaOptions
{
    Ativo = true,
    Host = "localhost",
    Port = 5432,
    Database = "kayron",
    Username = "kayron_agent",
    PasswordEnvironmentVariable = "KAYRON_POSTGRES_PASSWORD",
    Tabela = "kayron_memorias",
    Dimensoes = 384
});

var embedding = new TestEmbedding();
var memoriaPostgres = new MemoriaPostgres(options, embedding, NullLogger<MemoriaPostgres>.Instance);
Console.WriteLine($"DISPONIVEL={memoriaPostgres.Disponivel}");
memoriaPostgres.Inicializar();
Console.WriteLine("INICIALIZADO");
memoriaPostgres.Sincronizar(new KAYRON.Core.MemoriaAprendida
{
    Chave = "probe_memoria",
    Valor = "teste postgres pgvector",
    Tipo = "fato",
    Importancia = 3,
    Explicita = false,
    Origem = "probe",
    Tags = Array.Empty<string>(),
    AprendidoEm = DateTime.UtcNow,
    AtualizadoEm = DateTime.UtcNow
});
Console.WriteLine("SINCRONIZADO");

sealed class TestEmbedding : IEmbeddingMemoria
{
    public string Modelo => "probe";
    public Task<float[]> GerarAsync(string texto, CancellationToken cancellationToken = default)
        => Task.FromResult(Enumerable.Repeat(0.01f, 384).ToArray());
}
