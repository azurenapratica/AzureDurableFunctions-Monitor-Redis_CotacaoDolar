using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace FunctionAppMonitorCotacao
{
    public class MonitorCotacoesUSD
    {
        private ConnectionMultiplexer _redisConnection;

        public MonitorCotacoesUSD(ConnectionMultiplexer redisConnection)
        {
            _redisConnection = redisConnection;
        }

        [Function(nameof(MonitorCotacoesUSD))]
        public async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            var logger = context.CreateReplaySafeLogger(nameof(MonitorCotacoesUSD));
            logger.LogInformation("Iniciando monitoramento...");
            var outputs = new List<string>();

            int pollingInterval = GetPollingInterval();
            var expiryTime = GetExpiryTime(context);

            while (context.CurrentUtcDateTime < expiryTime)
            {
                var lastUpdate = await context.CallActivityAsync<string>("HasUpdate");
                if (lastUpdate is not null)
                {
                    logger.LogInformation("A orquestracao detectou um update...");
                    outputs.Add(await context.CallActivityAsync<string>(nameof(NotifyUpdate)));
                    break;
                }
                else
                    logger.LogInformation("A orquestracao nao detectou nenhum update...");

                var nextCheck = context.CurrentUtcDateTime.AddSeconds(pollingInterval);
                await context.CreateTimer(nextCheck, CancellationToken.None);
            }
            return outputs;
        }

        [Function(nameof(HasUpdate))]
        public string? HasUpdate([ActivityTrigger] FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger(nameof(HasUpdate));
            var database = _redisConnection.GetDatabase();
            var keyRedis = Environment.GetEnvironmentVariable("KeyCotacaoRedis");

            string data = database.StringGet(keyRedis)!;
            if (data is not null)
                logger.LogInformation($"Ha um update: {data}");
            else
                logger.LogInformation("Sem updates no momento...");
            return data!;
        }

        [Function(nameof(NotifyUpdate))]
        public string NotifyUpdate([ActivityTrigger] FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger(nameof(NotifyUpdate));
            var database = _redisConnection.GetDatabase();
            var keyRedis = Environment.GetEnvironmentVariable("KeyCotacaoRedis");

            var updateMessage = $"Notificacao de Update: {database.StringGet(keyRedis)}";
            database.KeyDelete(keyRedis);

            logger.LogInformation(updateMessage);
            return updateMessage;
        }

        [Function(nameof(MonitorCotacoesUSD_HttpStart))]
        public async Task<HttpResponseData> MonitorCotacoesUSD_HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger(nameof(MonitorCotacoesUSD_HttpStart));

            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(MonitorCotacoesUSD));
            logger.LogInformation("Iniciada orquestracao com ID = '{instanceId}'.", instanceId);

            return client.CreateCheckStatusResponse(req, instanceId);
        }

        private DateTime GetExpiryTime(TaskOrchestrationContext context)
        {
            return context.CurrentUtcDateTime.AddSeconds(Convert.ToInt32(
                Environment.GetEnvironmentVariable("ExpiryTime"))); // Tempo em segundos
        }

        private int GetPollingInterval()
        {
            return Convert.ToInt32(
                Environment.GetEnvironmentVariable("PollingInterval")); // Tempo em segundos
        }
    }
}
