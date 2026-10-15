using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class ConversorUnidades : IFerramenta
{
    public string Nome => "conversao";

    public string Descricao =>
        "Converte unidades determinísticas de distância, massa, tempo, temperatura e velocidade.";

    public CapacidadeFerramenta Capacidade => new()
    {
        Nome = Nome,
        Descricao = Descricao,
        Operacoes = new[] { "converter" },
        Exemplos = new[]
        {
            "10 km em metros",
            "1 kg para gramas",
            "100 °C em Fahrenheit",
            "72 km/h para m/s"
        }
    };

    public Task<ResultadoFerramenta> ExecutarAsync(
        string argumentos,
        IContexto contexto,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(argumentos))
            return Task.FromResult(ResultadoFerramenta.Falha("Entrada de conversão vazia."));

        var match = Regex.Match(
            argumentos.Trim(),
            @"^\s*(?<valor>[+-]?(?:\d+(?:[\.,]\d+)?|[\.,]\d+))\s*(?<origem>km/h|m/s|km|quilometro|quilometros|m|metro|metros|cm|centimetro|centimetros|centímetro|centímetros|mm|milimetro|milimetros|milímetro|milímetros|kg|quilograma|quilogramas|g|grama|gramas|mg|miligrama|miligramas|h|hora|horas|min|minuto|minutos|s|segundo|segundos|°c|ºc|celsius|c|°f|ºf|fahrenheit|f|k|kelvin)\s*(?:em|para)\s*(?<destino>km/h|m/s|km|quilometro|quilometros|m|metro|metros|cm|centimetro|centimetros|centímetro|centímetros|mm|milimetro|milimetros|milímetro|milímetros|kg|quilograma|quilogramas|g|grama|gramas|mg|miligrama|miligramas|h|hora|horas|min|minuto|minutos|s|segundo|segundos|°c|ºc|celsius|c|°f|ºf|fahrenheit|f|k|kelvin)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (!match.Success)
            return Task.FromResult(ResultadoFerramenta.Falha("Formato de conversão inválido."));

        var valorTexto = match.Groups["valor"].Value.Replace(',', '.');

        if (!double.TryParse(
                valorTexto,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var valor))
        {
            return Task.FromResult(ResultadoFerramenta.Falha("Valor numérico inválido."));
        }

        var origem = NormalizarUnidade(match.Groups["origem"].Value);
        var destino = NormalizarUnidade(match.Groups["destino"].Value);

        if (origem is null)
            return Task.FromResult(ResultadoFerramenta.Falha("Unidade de origem desconhecida."));

        if (destino is null)
            return Task.FromResult(ResultadoFerramenta.Falha("Unidade de destino desconhecida."));

        if (!TentarConverter(valor, origem.Value, destino.Value, out var resultado))
            return Task.FromResult(ResultadoFerramenta.Falha("Unidades incompatíveis."));

        return Task.FromResult(
            ResultadoFerramenta.Ok(
                resultado.ToString("0.###############", CultureInfo.InvariantCulture)));
    }

    private static Unidade? NormalizarUnidade(string unidade)
    {
        return unidade.Trim().ToLowerInvariant() switch
        {
            "km" or "quilometro" or "quilometros" => Unidade.Km,
            "m" or "metro" or "metros" => Unidade.M,
            "cm" or "centimetro" or "centimetros" or "centímetro" or "centímetros" => Unidade.Cm,
            "mm" or "milimetro" or "milimetros" or "milímetro" or "milímetros" => Unidade.Mm,

            "kg" or "quilograma" or "quilogramas" => Unidade.Kg,
            "g" or "grama" or "gramas" => Unidade.G,
            "mg" or "miligrama" or "miligramas" => Unidade.Mg,

            "h" or "hora" or "horas" => Unidade.H,
            "min" or "minuto" or "minutos" => Unidade.Min,
            "s" or "segundo" or "segundos" => Unidade.S,

            "°c" or "ºc" or "celsius" or "c" => Unidade.C,
            "°f" or "ºf" or "fahrenheit" or "f" => Unidade.F,
            "k" or "kelvin" => Unidade.K,

            "km/h" => Unidade.KmH,
            "m/s" => Unidade.MS,

            _ => null
        };
    }

    private static bool TentarConverter(
        double valor,
        Unidade origem,
        Unidade destino,
        out double resultado)
    {
        if (EhDistancia(origem) && EhDistancia(destino))
        {
            var metros = origem switch
            {
                Unidade.Km => valor * 1000d,
                Unidade.M => valor,
                Unidade.Cm => valor / 100d,
                Unidade.Mm => valor / 1000d,
                _ => 0d
            };

            resultado = destino switch
            {
                Unidade.Km => metros / 1000d,
                Unidade.M => metros,
                Unidade.Cm => metros * 100d,
                Unidade.Mm => metros * 1000d,
                _ => 0d
            };

            return true;
        }

        if (EhMassa(origem) && EhMassa(destino))
        {
            var gramas = origem switch
            {
                Unidade.Kg => valor * 1000d,
                Unidade.G => valor,
                Unidade.Mg => valor / 1000d,
                _ => 0d
            };

            resultado = destino switch
            {
                Unidade.Kg => gramas / 1000d,
                Unidade.G => gramas,
                Unidade.Mg => gramas * 1000d,
                _ => 0d
            };

            return true;
        }

        if (EhTempo(origem) && EhTempo(destino))
        {
            var segundos = origem switch
            {
                Unidade.H => valor * 3600d,
                Unidade.Min => valor * 60d,
                Unidade.S => valor,
                _ => 0d
            };

            resultado = destino switch
            {
                Unidade.H => segundos / 3600d,
                Unidade.Min => segundos / 60d,
                Unidade.S => segundos,
                _ => 0d
            };

            return true;
        }

        if (EhTemperatura(origem) && EhTemperatura(destino))
        {
            var celsius = origem switch
            {
                Unidade.C => valor,
                Unidade.F => (valor - 32d) * 5d / 9d,
                Unidade.K => valor - 273.15d,
                _ => 0d
            };

            resultado = destino switch
            {
                Unidade.C => celsius,
                Unidade.F => celsius * 9d / 5d + 32d,
                Unidade.K => celsius + 273.15d,
                _ => 0d
            };

            return true;
        }

        if (EhVelocidade(origem) && EhVelocidade(destino))
        {
            resultado = origem == Unidade.KmH
                ? valor / 3.6d
                : valor * 3.6d;

            return true;
        }

        resultado = 0d;
        return false;
    }

    private static bool EhDistancia(Unidade unidade) =>
        unidade is Unidade.Km or Unidade.M or Unidade.Cm or Unidade.Mm;

    private static bool EhMassa(Unidade unidade) =>
        unidade is Unidade.Kg or Unidade.G or Unidade.Mg;

    private static bool EhTempo(Unidade unidade) =>
        unidade is Unidade.H or Unidade.Min or Unidade.S;

    private static bool EhTemperatura(Unidade unidade) =>
        unidade is Unidade.C or Unidade.F or Unidade.K;

    private static bool EhVelocidade(Unidade unidade) =>
        unidade is Unidade.KmH or Unidade.MS;

    private enum Unidade
    {
        Km,
        M,
        Cm,
        Mm,
        Kg,
        G,
        Mg,
        H,
        Min,
        S,
        C,
        F,
        K,
        KmH,
        MS
    }
}
