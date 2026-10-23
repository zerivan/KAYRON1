namespace KAYRON.Core;

public interface IModeloInteligencia
{
    Task<Resposta> GerarAsync(
        Instrucao instrucao,
        IContexto contexto,
        CancellationToken cancellationToken = default);
}

public sealed record AnexoMultimodal(string Nome, string MimeType, byte[] Conteudo);

public interface IModeloInteligenciaMultimodal
{
    Task<Resposta> GerarComAnexosAsync(
        Instrucao instrucao,
        IContexto contexto,
        IReadOnlyList<AnexoMultimodal> anexos,
        CancellationToken cancellationToken = default);
}