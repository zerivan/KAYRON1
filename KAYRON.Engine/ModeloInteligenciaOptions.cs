namespace KAYRON.Engine;

public sealed class ModeloInteligenciaOptions
{
    public const string SectionName = "KAYRON:Inteligencia";

    public string Provedor { get; set; } = "Local";

    public string Modelo { get; set; } = "local";

    public double Temperatura { get; set; } = 0.7;

    public string ApiKey { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string Url { get; set; } = "https://us-south.ml.cloud.ibm.com";




}

