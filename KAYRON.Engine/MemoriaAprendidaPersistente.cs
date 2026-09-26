using System.Text.Json;
using KAYRON.Core;
using Microsoft.Extensions.Logging;

namespace KAYRON.Engine;

public class MemoriaAprendidaPersistente : IMemoriaAprendidaPersistente
{
    private readonly string _caminho;
    private readonly Dictionary<string, KAYRON.Core.MemoriaAprendida> _dados;
    private readonly ILogger<MemoriaAprendidaPersistente> _logger;

    public MemoriaAprendidaPersistente(
        string caminho,
        ILogger<MemoriaAprendidaPersistente> logger)
    {
        if (string.IsNullOrWhiteSpace(caminho))
        {
            throw new ArgumentException(
                "O caminho da memória persistente não pode estar vazio.",
                nameof(caminho));
        }

        ArgumentNullException.ThrowIfNull(logger);

        _caminho = caminho;
        _logger = logger;
        _dados = Carregar();
    }

    public void Salvar(KAYRON.Core.MemoriaAprendida memoria)
    {
        ArgumentNullException.ThrowIfNull(memoria);

        if (string.IsNullOrWhiteSpace(memoria.Chave))
        {
            throw new ArgumentException(
                "A memória precisa possuir uma chave.",
                nameof(memoria));
        }

        _dados[memoria.Chave] = memoria;

        Persistir();

        _logger.LogDebug(
            "Memória persistida. Chave: {Chave}",
            memoria.Chave);
    }

    public bool Remover(string chave)
    {
        if (string.IsNullOrWhiteSpace(chave) || !_dados.Remove(chave.Trim()))
        {
            return false;
        }

        Persistir();
        _logger.LogDebug("Memória removida. Chave: {Chave}", chave);
        return true;
    }

    public KAYRON.Core.MemoriaAprendida? Recuperar(string chave)
    {
        if (string.IsNullOrWhiteSpace(chave))
        {
            return null;
        }

        return _dados.TryGetValue(chave.Trim(), out var memoria)
            ? memoria
            : null;
    }

    public IReadOnlyCollection<KAYRON.Core.MemoriaAprendida> Listar()
    {
        return _dados.Values.ToList().AsReadOnly();
    }

    private Dictionary<string, KAYRON.Core.MemoriaAprendida> Carregar()
    {
        if (!File.Exists(_caminho))
        {
            _logger.LogInformation(
                "Arquivo de memória ainda não existe. Um novo será criado em: {Caminho}",
                _caminho);

            return CriarDicionario();
        }

        try
        {
            var json = File.ReadAllText(_caminho);

            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning(
                    "O arquivo de memória está vazio: {Caminho}",
                    _caminho);

                return CriarDicionario();
            }

            var dados =
                JsonSerializer.Deserialize<List<KAYRON.Core.MemoriaAprendida>>(json);

            var resultado = dados?
                .Where(m => !string.IsNullOrWhiteSpace(m.Chave))
                .GroupBy(m => m.Chave, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    grupo => grupo.Key,
                    grupo => grupo.Last(),
                    StringComparer.OrdinalIgnoreCase)
                ?? CriarDicionario();

            _logger.LogInformation(
                "Memória carregada. Registros: {Quantidade}",
                resultado.Count);

            return resultado;
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "O arquivo de memória contém JSON inválido: {Caminho}",
                _caminho);

            throw new InvalidOperationException(
                "O arquivo de memória persistente está corrompido.",
                ex);
        }
        catch (IOException ex)
        {
            _logger.LogError(
                ex,
                "Erro de I/O ao carregar a memória: {Caminho}",
                _caminho);

            throw;
        }
    }

    private void Persistir()
    {
        var diretorio = Path.GetDirectoryName(_caminho);

        if (!string.IsNullOrWhiteSpace(diretorio))
        {
            Directory.CreateDirectory(diretorio);
        }

        var json = JsonSerializer.Serialize(
            _dados.Values,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        File.WriteAllText(_caminho, json);
    }

    private static Dictionary<string, KAYRON.Core.MemoriaAprendida> CriarDicionario()
    {
        return new Dictionary<string, KAYRON.Core.MemoriaAprendida>(
            StringComparer.OrdinalIgnoreCase);
    }
}
