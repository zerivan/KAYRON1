using KAYRON.Engine;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace KAYRON.MCP;

[McpServerToolType]
public static class KayronMcpTools
{
    [McpServerTool, Description("Envia uma solicitação ao agente KAYRON para análise, planejamento ou execução de uma tarefa no computador/projeto.")]
    public static async Task<string> Kayron(
        [Description("Solicitação em linguagem natural para o agente KAYRON.")] string solicitacao,
        GerenciadorConversa gerenciador,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(solicitacao))
            throw new ArgumentException("A solicitação não pode estar vazia.", nameof(solicitacao));

        var contexto = new Contexto();
        var resposta = await gerenciador.ProcessarAsync(solicitacao.Trim(), contexto);
        cancellationToken.ThrowIfCancellationRequested();
        return resposta.Conteudo;
    }

    [McpServerTool, Description("Informa a identidade e as capacidades principais do servidor MCP do KAYRON.")]
    public static string KayronInfo() =>
        "KAYRON MCP: agente de engenharia local com memória, contexto, planejamento, orquestração e ferramentas para projetos.";
}
