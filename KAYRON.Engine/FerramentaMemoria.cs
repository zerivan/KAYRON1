using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class FerramentaMemoria : IFerramenta
{
    private readonly IMemoria _memoria;

    public FerramentaMemoria(IMemoria memoria)
    {
        ArgumentNullException.ThrowIfNull(memoria);

        _memoria = memoria;
    }

    public string Nome => "memoria";

    public string Descricao =>
        "Guarda, recupera e verifica informações da memória operacional.";

    public CapacidadeFerramenta Capacidade =>
        new()
        {
            Nome = Nome,
            Descricao = Descricao,
            Operacoes =
            [
                "guardar",
                "recuperar",
                "existe"
            ],
            Exemplos =
            [
                "guarde meu projeto = KAYRON",
                "recupere meu projeto",
                "verifique se existe meu projeto na memória"
            ]
        };
    public Task<ResultadoFerramenta> ExecutarAsync(
        string argumentos,
        IContexto contexto,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(argumentos))
        {
            return Task.FromResult(
                ResultadoFerramenta.Falha(
                    "Use: guardar chave = valor, recuperar chave ou existe chave"));
        }

        var partes = argumentos.Trim().Split(
            ' ',
            2,
            StringSplitOptions.RemoveEmptyEntries);

        var operacao = partes[0].ToLowerInvariant();

        var restante =
            partes.Length > 1
                ? partes[1].Trim()
                : string.Empty;

        switch (operacao)
        {
            case "guardar":
            {
                var dados = restante.Split('=', 2);

                if (dados.Length != 2)
                {
                    return Task.FromResult(
                        ResultadoFerramenta.Falha(
                            "Use: guardar chave = valor"));
                }

                var chave = dados[0].Trim();
                var valor = dados[1].Trim();

                if (string.IsNullOrWhiteSpace(chave) ||
                    string.IsNullOrWhiteSpace(valor))
                {
                    return Task.FromResult(
                        ResultadoFerramenta.Falha(
                            "A chave e o valor não podem estar vazios."));
                }

                _memoria.Guardar(chave, valor);

                contexto.Adicionar(chave, valor);

                return Task.FromResult(
                    ResultadoFerramenta.Ok(
                        $"Memória guardada: {chave}"));
            }

            case "recuperar":
            {
                if (string.IsNullOrWhiteSpace(restante))
                {
                    return Task.FromResult(
                        ResultadoFerramenta.Falha(
                            "Informe a chave."));
                }

                var valor = _memoria.Recuperar(restante);

                return Task.FromResult(
                    ResultadoFerramenta.Ok(
                        valor ?? "Nenhum valor encontrado."));
            }

            case "existe":
            {
                if (string.IsNullOrWhiteSpace(restante))
                {
                    return Task.FromResult(
                        ResultadoFerramenta.Falha(
                            "Informe a chave."));
                }

                return Task.FromResult(
                    ResultadoFerramenta.Ok(
                        _memoria.Existe(restante)
                            ? "Existe."
                            : "Não existe."));
            }

            default:
                return Task.FromResult(
                    ResultadoFerramenta.Falha(
                        $"Operação desconhecida: {operacao}"));
        }
    }
}

