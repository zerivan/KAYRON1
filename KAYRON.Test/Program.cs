using KAYRON.Core;
using KAYRON.Engine;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
var options = Options.Create(new ModeloInteligenciaOptions
{
    Modelo = "gemini-3.1-flash-lite",
    Temperatura = 0.2,
    Provedor = "Gemini"
});
var caminho = Path.Combine(Path.GetTempPath(), $"kayron-learning-{Guid.NewGuid():N}.json");

try
{
    var loggerFactory = LoggerFactory.Create(builder => builder
        .AddSimpleConsole(options => { options.SingleLine = true; options.TimestampFormat = "HH:mm:ss "; })
        .SetMinimumLevel(LogLevel.Information));

    var persistente = new MemoriaAprendidaPersistente(caminho, loggerFactory.CreateLogger<MemoriaAprendidaPersistente>());
    var embedding = new EmbeddingMemoriaLocal();
    var postgres = new MemoriaPostgres(Options.Create(new PostgresMemoriaOptions { Ativo = false }), embedding, NullLogger<MemoriaPostgres>.Instance);
    var memoriaAprendida = new KAYRON.Engine.MemoriaAprendida(persistente, postgres);
    var memoriaBase = new Memoria();
    var semantica = new MemoriaSemantica(embedding);
    var convocador = new ConvocadorMemoria(memoriaBase, memoriaAprendida, semantica, postgres);
    var web = new PesquisaWeb(http, loggerFactory.CreateLogger<PesquisaWeb>());
    var testeBusca = await web.PesquisarAsync(".NET C# linguagem principal", 6);
    Console.WriteLine($"PESQUISA_WEB_RESULTADOS={testeBusca.Count}");
    foreach (var resultado in testeBusca.Take(3))
        Console.WriteLine($"WEB_RESULT={resultado.Titulo} | {resultado.Url}");

    var deepSearch = new DeepSearch(
        http,
        web,
        embedding,
        options,
        loggerFactory.CreateLogger<DeepSearch>(),
        loggerFactory.CreateLogger<ModeloInteligenciaHuggingFace>());
    var gestor = new GestorConhecimento(convocador, deepSearch, memoriaAprendida);

    var primeira = new KAYRON.Engine.Contexto();
    var pergunta1 = "Qual é a linguagem de programação principal usada para desenvolver aplicações .NET?";
    primeira.Adicionar("intencao_detectada", "internet:pesquisar");
    var diagnostico = await deepSearch.GerarAsync(new Instrucao { Conteudo = pergunta1 }, primeira);
    Console.WriteLine($"DEEPSEARCH_TAMANHO={diagnostico.Conteudo.Length}");
    Console.WriteLine($"DEEPSEARCH_INICIO={diagnostico.Conteudo[..Math.Min(500, diagnostico.Conteudo.Length)].Replace(Environment.NewLine, " ")}");
    var adquiriu1 = await gestor.AdquirirSeNecessarioAsync(new Instrucao { Conteudo = pergunta1 }, primeira);
    var estado1 = primeira.Obter("conhecimento:estado");
    Console.WriteLine($"PRIMEIRA_ESTADO={estado1 ?? "nenhum"}");

    var adquirido = primeira.Obter("conhecimento:adquirido");
    var persistiu = !string.IsNullOrWhiteSpace(adquirido) &&
                    memoriaAprendida.Listar().Any(m => m.Valor == adquirido);

    var segunda = new KAYRON.Engine.Contexto();
    var pergunta2 = "Que linguagem é normalmente utilizada no desenvolvimento com .NET?";
    var adquiriu2 = await gestor.AdquirirSeNecessarioAsync(
        new Instrucao { Conteudo = pergunta2 },
        segunda);
    var recuperou = !string.IsNullOrWhiteSpace(
        segunda.Obter("conhecimento:resposta"));

    Console.WriteLine($"PRIMEIRA_ADQUIRIU={adquiriu1}");
    Console.WriteLine($"CONHECIMENTO_PERSISTIDO={persistiu}");
    Console.WriteLine($"SEGUNDA_ADQUIRIU_NOVAMENTE={adquiriu2}");
    Console.WriteLine($"SEGUNDA_RECUPEROU_MEMORIA={recuperou}");
    Console.WriteLine($"RESULTADO={(adquiriu1 && persistiu && !adquiriu2 && recuperou ? "PASSOU" : "FALHOU")}");
}
finally
{
    if (File.Exists(caminho))
        File.Delete(caminho);
}