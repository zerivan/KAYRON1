using KAYRON.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector;
using Pgvector.Npgsql;

namespace KAYRON.Engine;

public sealed class MemoriaPostgres
{
    private readonly PostgresMemoriaOptions _options;
    private readonly IEmbeddingMemoria _embedding;
    private readonly ILogger<MemoriaPostgres> _logger;
    private readonly NpgsqlDataSource? _dataSource;
    private bool _inicializado;

    public MemoriaPostgres(
        IOptions<PostgresMemoriaOptions> options,
        IEmbeddingMemoria embedding,
        ILogger<MemoriaPostgres> logger)
    {
        _options = options.Value;
        _embedding = embedding;
        _logger = logger;
        _dataSource = CriarDataSource();
    }

    public bool Disponivel => _dataSource is not null;

    public void Inicializar()
    {
        if (_inicializado || _dataSource is null)
            return;

        try
        {
            using var conn = _dataSource.OpenConnection();
            using var extension = new NpgsqlCommand(
                "CREATE EXTENSION IF NOT EXISTS vector", conn);
            extension.ExecuteNonQuery();
            conn.ReloadTypes();

            var tabela = IdentificadorSeguro(_options.Tabela);
            using var tabelaCommand = new NpgsqlCommand($"""
                CREATE TABLE IF NOT EXISTS {tabela} (
                    chave text PRIMARY KEY,
                    valor text NOT NULL,
                    tipo text NOT NULL,
                    importancia integer NOT NULL,
                    explicita boolean NOT NULL,
                    origem text NOT NULL,
                    tags text[] NOT NULL DEFAULT ARRAY[]::text[],
                    aprendido_em timestamptz NOT NULL,
                    atualizado_em timestamptz NOT NULL,
                    ultima_recuperacao_em timestamptz NULL,
                    embedding vector({_options.Dimensoes}) NOT NULL,
                    embedding_modelo text NOT NULL
                )
                """, conn);
            tabelaCommand.ExecuteNonQuery();

            using var indexCommand = new NpgsqlCommand($"""
                CREATE INDEX IF NOT EXISTS {tabela}_embedding_hnsw
                ON {tabela} USING hnsw (embedding vector_cosine_ops)
                """, conn);
            indexCommand.ExecuteNonQuery();

            _inicializado = true;
            _logger.LogInformation("Memória PostgreSQL inicializada. Tabela: {Tabela}", tabela);
        }
        catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Memória PostgreSQL indisponível. O KAYRON continuará com a memória local.");
        }
    }

    public void Sincronizar(KAYRON.Core.MemoriaAprendida memoria)
    {
        if (_dataSource is null)
            return;

        try
        {
            Inicializar();
            if (!_inicializado)
                return;

            var vetor = memoria.Embedding;
            if (vetor is null || vetor.Length != _options.Dimensoes)
                vetor = _embedding.GerarAsync(Texto(memoria)).GetAwaiter().GetResult();

            using var conn = _dataSource.OpenConnection();
            var tabela = IdentificadorSeguro(_options.Tabela);
            using var command = new NpgsqlCommand($"""
                INSERT INTO {tabela}
                (chave, valor, tipo, importancia, explicita, origem, tags,
                 aprendido_em, atualizado_em, ultima_recuperacao_em,
                 embedding, embedding_modelo)
                VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)
                ON CONFLICT (chave) DO UPDATE SET
                    valor = EXCLUDED.valor,
                    tipo = EXCLUDED.tipo,
                    importancia = EXCLUDED.importancia,
                    explicita = EXCLUDED.explicita,
                    origem = EXCLUDED.origem,
                    tags = EXCLUDED.tags,
                    atualizado_em = EXCLUDED.atualizado_em,
                    ultima_recuperacao_em = EXCLUDED.ultima_recuperacao_em,
                    embedding = EXCLUDED.embedding,
                    embedding_modelo = EXCLUDED.embedding_modelo
                """, conn);

            command.Parameters.AddWithValue(memoria.Chave);
            command.Parameters.AddWithValue(memoria.Valor);
            command.Parameters.AddWithValue(memoria.Tipo);
            command.Parameters.AddWithValue(memoria.Importancia);
            command.Parameters.AddWithValue(memoria.Explicita);
            command.Parameters.AddWithValue(memoria.Origem);
            command.Parameters.AddWithValue(memoria.Tags.ToArray());
            command.Parameters.AddWithValue(memoria.AprendidoEm);
            command.Parameters.AddWithValue(memoria.AtualizadoEm);
            command.Parameters.AddWithValue((object?)memoria.UltimaRecuperacaoEm ?? DBNull.Value);
            command.Parameters.AddWithValue(new Vector(vetor));
            command.Parameters.AddWithValue(memoria.EmbeddingModelo ?? _embedding.Modelo);
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Não foi possível sincronizar a memória {Chave} no PostgreSQL.", memoria.Chave);
        }
    }

    public IReadOnlyCollection<KAYRON.Core.MemoriaAprendida> Pesquisar(
        string consulta,
        int limite = 5)
    {
        if (_dataSource is null || string.IsNullOrWhiteSpace(consulta) || limite <= 0)
            return Array.Empty<KAYRON.Core.MemoriaAprendida>();

        try
        {
            Inicializar();
            if (!_inicializado)
                return Array.Empty<KAYRON.Core.MemoriaAprendida>();

            var vetor = _embedding.GerarAsync(consulta).GetAwaiter().GetResult();
            using var conn = _dataSource.OpenConnection();
            var tabela = IdentificadorSeguro(_options.Tabela);
            using var command = new NpgsqlCommand($"""
                SELECT chave, valor, tipo, importancia, explicita, origem, tags,
                       aprendido_em, atualizado_em, ultima_recuperacao_em,
                       embedding, embedding_modelo,
                       1 - (embedding <=> $1) AS score
                FROM {tabela}
                WHERE NOT (tipo = 'conhecimento' AND valor ILIKE 'Pesquisa realizada para:%')
                  AND (tipo <> 'conhecimento' OR (origem = 'pesquisa_verificada' AND 'verificado' = ANY(tags) AND atualizado_em >= NOW() - INTERVAL '7 days' AND aprendido_em >= NOW() - INTERVAL '7 days'))
                  AND 1 - (embedding <=> $1) >= 0.55
                ORDER BY embedding <=> $1
                LIMIT $2
                """, conn);
            command.Parameters.AddWithValue(new Vector(vetor));
            command.Parameters.AddWithValue(Math.Min(limite, 10));

            var resultados = new List<KAYRON.Core.MemoriaAprendida>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var vector = (Vector)reader.GetValue(10);
                resultados.Add(new KAYRON.Core.MemoriaAprendida
                {
                    Chave = reader.GetString(0),
                    Valor = reader.GetString(1),
                    Tipo = reader.GetString(2),
                    Importancia = reader.GetInt32(3),
                    Explicita = reader.GetBoolean(4),
                    Origem = reader.GetString(5),
                    Tags = ((string[])reader.GetValue(6)).AsReadOnly(),
                    AprendidoEm = reader.GetFieldValue<DateTime>(7),
                    AtualizadoEm = reader.GetFieldValue<DateTime>(8),
                    UltimaRecuperacaoEm = reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTime>(9),
                    Embedding = vector.ToArray(),
                    EmbeddingModelo = reader.GetString(11)
                });
            }

            return resultados;
        }
        catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Não foi possível pesquisar memória semântica no PostgreSQL.");
            return Array.Empty<KAYRON.Core.MemoriaAprendida>();
        }
    }

    private NpgsqlDataSource? CriarDataSource()
    {
        if (!_options.Ativo)
            return null;

        var connectionString = _options.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var senha = Environment.GetEnvironmentVariable(_options.PasswordEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(senha))
            {
                _logger.LogInformation(
                    "PostgreSQL de memória aguardando a variável {Variavel}.",
                    _options.PasswordEnvironmentVariable);
                return null;
            }

            connectionString =
                $"Host={_options.Host};Port={_options.Port};Database={_options.Database};" +
                $"Username={_options.Username};Password={senha};Pooling=true";
        }

        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.UseVector();
        return builder.Build();
    }

    private static string Texto(KAYRON.Core.MemoriaAprendida memoria) =>
        $"{memoria.Chave} {memoria.Valor} {string.Join(' ', memoria.Tags)}";

    private static string IdentificadorSeguro(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor) ||
            valor.Any(c => !char.IsLetterOrDigit(c) && c != '_'))
        {
            throw new ArgumentException("Identificador PostgreSQL inválido.", nameof(valor));
        }

        return valor;
    }
}
