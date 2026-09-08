using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class ContextoProjeto
    : IContextoProjeto
{
    private readonly IDetectorProjeto _detector;

    public InformacoesProjeto? Atual { get; private set; }

    public ContextoProjeto(
        IDetectorProjeto detector)
    {
        ArgumentNullException.ThrowIfNull(detector);

        _detector = detector;
    }

    public InformacoesProjeto? Detectar(
        string diretorio)
    {
        if (string.IsNullOrWhiteSpace(diretorio))
        {
            return null;
        }

        var raiz =
            _detector.DetectarRaiz(
                diretorio);

        if (string.IsNullOrWhiteSpace(raiz))
        {
            Atual = null;
            return null;
        }

        var arquivo =
            _detector.ObterArquivoProjeto(
                raiz);

        var tipo =
            DetectarTipo(
                arquivo);

        var solucao =
            Directory
                .EnumerateFiles(
                    raiz,
                    "*.sln*",
                    SearchOption.TopDirectoryOnly)
                .FirstOrDefault();

        var projeto =
            new InformacoesProjeto
            {
                Identificado = true,
                Diretorio = raiz,
                ArquivoProjeto = arquivo,
                Tipo = tipo,
                Solucao = solucao
            };

        Atual = projeto;

        return projeto;
    }

    public void Definir(
        InformacoesProjeto projeto)
    {
        ArgumentNullException.ThrowIfNull(projeto);

        Atual = projeto;
    }

    private static string? DetectarTipo(
        string? arquivo)
    {
        if (string.IsNullOrWhiteSpace(arquivo))
        {
            return null;
        }

        var nome =
            Path.GetFileName(
                arquivo);

        if (nome.Equals(
                "package.json",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Node.js";
        }

        if (nome.Equals(
                "Cargo.toml",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Rust";
        }

        if (nome.Equals(
                "go.mod",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Go";
        }

        if (nome.Equals(
                "pyproject.toml",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Python";
        }

        if (nome.Equals(
                "pom.xml",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Java";
        }

        if (nome.EndsWith(
                ".csproj",
                StringComparison.OrdinalIgnoreCase) ||
            nome.EndsWith(
                ".sln",
                StringComparison.OrdinalIgnoreCase) ||
            nome.EndsWith(
                ".slnx",
                StringComparison.OrdinalIgnoreCase))
        {
            return ".NET";
        }

        return null;
    }
}
