using KAYRON.Core;
using KAYRON.Engine;

namespace KAYRON.Agent;

public class Aplicacao
{
    private readonly GerenciadorConversa _gerenciadorConversa;

    public Aplicacao(GerenciadorConversa gerenciadorConversa)
    {
        ArgumentNullException.ThrowIfNull(gerenciadorConversa);

        _gerenciadorConversa = gerenciadorConversa;
    }

    public async Task ExecutarAsync(
        CancellationToken cancellationToken = default)
    {
        var contexto = new KAYRON.Engine.Contexto();

        Console.WriteLine("KAYRON iniciado.");

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write("> ");

            var entrada = Console.ReadLine();

            if (entrada is null)
            {
                break;
            }

            if (entrada.Trim().Equals(
                    "/sair",
                    StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(entrada))
            {
                continue;
            }

            var instrucao = new Instrucao
            {
                Conteudo = entrada
            };

            try
            {
                var resposta =
                    await _gerenciadorConversa.ProcessarAsync(
                        instrucao.Conteudo,
                        contexto,
                        cancellationToken);

                if (!string.IsNullOrWhiteSpace(resposta.Conteudo))
                {
                    Console.WriteLine(resposta.Conteudo);
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Erro ao processar a instrução: {ex.Message}");
            }
        }

        Console.WriteLine("KAYRON encerrado.");
    }
}
