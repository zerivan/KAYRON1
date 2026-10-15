using System.ComponentModel;
using System.Text.Json;
using KAYRON.Core;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Server;

namespace KAYRON.MCP;

[McpServerToolType]
public static class MemoriaMcpTools
{
    [McpServerTool, Description("remember: grava uma memória persistente do KAYRON com chave, valor e tags.")]
    public static string Remember(
        [Description("Chave estável da memória.")] string chave,
        [Description("Conteúdo que deve ser lembrado entre sessões.")] string valor,
        [Description("Tags separadas por vírgula para facilitar a recuperação.")] string? tags,
        IMemoriaAprendida memoria,
        IConfiguration configuration)
    {
        Validar(chave, valor);
        var anterior = memoria.Recuperar(chave.Trim());
        var checkpoint = CriarCheckpoint(chave.Trim(), anterior, tags, configuration);
        var tagsArray = (tags ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        memoria.Aprender(chave.Trim(), valor.Trim(), "fato", 3, true, "mcp", tagsArray);
        return $"Memória registrada. chave={chave.Trim()} checkpoint={checkpoint}";
    }

    [McpServerTool, Description("recall: recupera a memória persistente mais diretamente relacionada à consulta.")]
    public static string Recall(
        [Description("Chave ou consulta para recuperar memória.")] string consulta,
        IMemoriaAprendida memoria)
    {
        if (string.IsNullOrWhiteSpace(consulta))
            throw new ArgumentException("A consulta não pode estar vazia.", nameof(consulta));

        var exata = memoria.Recuperar(consulta.Trim());
        if (exata is not null)
            return Formatar(exata);

        var resultado = memoria.Pesquisar(consulta.Trim(), 1).FirstOrDefault();
        return resultado is null ? "Nenhuma memória relevante encontrada." : Formatar(resultado);
    }

    [McpServerTool, Description("search: pesquisa memórias persistentes por palavras-chave em chave e conteúdo.")]
    public static string Search(
        [Description("Texto ou palavras-chave da pesquisa.")] string consulta,
        IMemoriaAprendida memoria)
    {
        if (string.IsNullOrWhiteSpace(consulta))
            throw new ArgumentException("A consulta não pode estar vazia.", nameof(consulta));

        var resultados = memoria.Pesquisar(consulta.Trim(), 5).ToList();
        if (resultados.Count == 0)
            return "Nenhuma memória encontrada.";

        return string.Join(Environment.NewLine, resultados.Select(Formatar));
    }

    [McpServerTool, Description("rollback: restaura a memória para o estado imediatamente anterior a um remember.")]
    public static string Rollback(
        [Description("Identificador do checkpoint retornado por remember.")] string checkpoint,
        IMemoriaAprendida memoria,
        IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(checkpoint))
            throw new ArgumentException("O checkpoint não pode estar vazio.", nameof(checkpoint));

        var caminho = CaminhoCheckpoints(configuration);
        if (!File.Exists(caminho))
            return "Checkpoint não encontrado.";

        var registros = JsonSerializer.Deserialize<List<Checkpoint>>(File.ReadAllText(caminho)) ?? [];
        var item = registros.FirstOrDefault(x => x.Id.Equals(checkpoint.Trim(), StringComparison.OrdinalIgnoreCase));
        if (item is null)
            return "Checkpoint não encontrado.";

        if (item.Anterior is null)
            memoria.Remover(item.Chave);
        else
            memoria.Aprender(item.Chave, item.Anterior.Valor);

        return item.Anterior is null
            ? $"Rollback concluído. A memória '{item.Chave}' foi removida."
            : $"Rollback concluído. A memória '{item.Chave}' foi restaurada.";
    }

    private static string CriarCheckpoint(
        string chave,
        KAYRON.Core.MemoriaAprendida? anterior,
        string? tags,
        IConfiguration configuration)
    {
        var id = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var caminho = CaminhoCheckpoints(configuration);
        var registros = File.Exists(caminho)
            ? JsonSerializer.Deserialize<List<Checkpoint>>(File.ReadAllText(caminho)) ?? []
            : [];

        registros.Add(new Checkpoint(id, chave, anterior, tags ?? string.Empty));
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        File.WriteAllText(caminho, JsonSerializer.Serialize(registros, new JsonSerializerOptions { WriteIndented = true }));
        return id;
    }

    private static string CaminhoCheckpoints(IConfiguration configuration)
    {
        var memoria = configuration["KAYRON:Memoria:Caminho"]
            ?? throw new InvalidOperationException("KAYRON:Memoria:Caminho não foi configurado.");
        return Path.Combine(Path.GetDirectoryName(memoria)!, "memoria-checkpoints.json");
    }

    private static string Formatar(KAYRON.Core.MemoriaAprendida memoria) =>
        $"[{memoria.Chave}] {memoria.Valor} (aprendida em {memoria.AprendidoEm:O})";

    private static string Normalizar(string valor) =>
        valor.Trim().ToLowerInvariant()
            .Replace("á", "a").Replace("à", "a").Replace("ã", "a").Replace("â", "a")
            .Replace("é", "e").Replace("ê", "e").Replace("í", "i")
            .Replace("ó", "o").Replace("ô", "o").Replace("õ", "o")
            .Replace("ú", "u").Replace("ç", "c");

    private static void Validar(string chave, string valor)
    {
        if (string.IsNullOrWhiteSpace(chave)) throw new ArgumentException("A chave não pode estar vazia.", nameof(chave));
        if (string.IsNullOrWhiteSpace(valor)) throw new ArgumentException("O valor não pode estar vazio.", nameof(valor));
    }

    private sealed record Checkpoint(
        string Id,
        string Chave,
        KAYRON.Core.MemoriaAprendida? Anterior,
        string Tags);
}
