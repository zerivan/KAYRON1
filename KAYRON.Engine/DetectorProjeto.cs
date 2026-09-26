using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class DetectorProjeto
    : IDetectorProjeto
{
    private static readonly string[] Marcadores =
    {
        "*.sln",
        "*.slnx",
        "*.csproj",
        "*.fsproj",
        "*.vbproj",
        "package.json",
        "Cargo.toml",
        "go.mod",
        "pyproject.toml",
        "requirements.txt",
        "pom.xml",
        "build.gradle",
        "Makefile",
        ".git"
    };

    public string? DetectarRaiz(
        string diretorio)
    {
        if (string.IsNullOrWhiteSpace(diretorio))
        {
            return null;
        }

        var atual =
            Path.GetFullPath(diretorio);

        if (File.Exists(atual))
        {
            atual =
                Path.GetDirectoryName(atual)
                ?? atual;
        }

        while (!string.IsNullOrWhiteSpace(atual))
        {
            if (EhProjeto(atual))
            {
                return atual;
            }

            var pai =
                Directory.GetParent(atual);

            if (pai is null)
            {
                break;
            }

            atual =
                pai.FullName;
        }

        return null;
    }

    public bool EhProjeto(
        string diretorio)
    {
        if (!Directory.Exists(diretorio))
        {
            return false;
        }

        foreach (var marcador in Marcadores)
        {
            if (marcador == ".git")
            {
                if (Directory.Exists(
                        Path.Combine(
                            diretorio,
                            marcador)))
                {
                    return true;
                }

                continue;
            }

            if (Directory.EnumerateFileSystemEntries(
                    diretorio,
                    marcador,
                    SearchOption.TopDirectoryOnly)
                .Any())
            {
                return true;
            }
        }

        return false;
    }

    public string? ObterArquivoProjeto(
        string diretorio)
    {
        if (!Directory.Exists(diretorio))
        {
            return null;
        }

        var extensoes =
            new[]
            {
                "*.sln",
                "*.slnx",
                "*.csproj",
                "*.fsproj",
                "*.vbproj",
                "package.json",
                "Cargo.toml",
                "go.mod",
                "pyproject.toml",
                "pom.xml"
            };

        foreach (var extensao in extensoes)
        {
            var arquivo =
                Directory
                    .EnumerateFiles(
                        diretorio,
                        extensao,
                        SearchOption.TopDirectoryOnly)
                    .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(arquivo))
            {
                return arquivo;
            }
        }

        return null;
    }
}
