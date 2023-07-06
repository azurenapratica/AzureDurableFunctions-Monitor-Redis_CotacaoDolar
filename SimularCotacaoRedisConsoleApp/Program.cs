using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using SimularCotacaoRedisConsoleApp.Models;
using StackExchange.Redis;
using System.Text.Json;

const decimal VALOR_BASE = 4.93m;
const string KEY_REDIS = "COTACAO-USD";
const int TIME_TO_LIVE = 30; // Tempo em segundos

var logger = new LoggerConfiguration()
    .WriteTo.Console(theme: AnsiConsoleTheme.Literate)
    .CreateLogger();
logger.Information(
    "Testando a simulacao de cotacoes do dolar com gravacao no Redis");

if (args.Length != 1)
{
    logger.Error("Informe como unico parametro a string de conexao com o Redis!");
    return;
}

var cotacao = new DadosCotacao()
{
    Sigla = "USD",
    Horario = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
    Valor = Math.Round(VALOR_BASE + new Random().Next(0, 21) / 1000m, 3)
};
var jsonCotacao = JsonSerializer.Serialize(cotacao);
logger.Information($"Dados que serao enviados para o Redis: {jsonCotacao}");

try
{
    using (var connection = ConnectionMultiplexer.Connect(args[0]))
    {
        var database = connection.GetDatabase();
        database.StringSet(KEY_REDIS, jsonCotacao, TimeSpan.FromSeconds(TIME_TO_LIVE));
        logger.Information("Dados enviados para o Redis!");
    }
}
catch (Exception ex)
{
    logger.Error(ex, "Erro durante a comunicacao com o Redis!");
}