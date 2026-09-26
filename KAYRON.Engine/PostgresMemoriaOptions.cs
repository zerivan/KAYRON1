namespace KAYRON.Engine;

public sealed class PostgresMemoriaOptions
{
    public const string SectionName = "KAYRON:Memoria:Postgres";

    public bool Ativo { get; set; } = true;
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = "postgres";
    public string Username { get; set; } = "postgres";
    public string? ConnectionString { get; set; }
    public string PasswordEnvironmentVariable { get; set; } = "KAYRON_POSTGRES_PASSWORD";
    public string Tabela { get; set; } = "kayron_memorias";
    public int Dimensoes { get; set; } = 384;
}
