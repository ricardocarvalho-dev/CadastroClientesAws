using Amazon.SQS;
using Amazon.SQS.Model;
using CadastroClientes.Application.DTOs;
using CadastroClientes.Application.UseCases;
using System.Text.Json;

namespace CadastroClientes.Worker;

public class SqsConsumerWorker : BackgroundService
{
    private const int MAX_TENTATIVAS = 3;
    private const int WAIT_TIME_SECONDS = 20; // Long polling
    private const int MAX_MESSAGES = 10;

    private readonly ILogger<SqsConsumerWorker> _logger;
    private readonly IAmazonSQS _sqsClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _queueUrl;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SqsConsumerWorker(
        ILogger<SqsConsumerWorker> logger,
        IAmazonSQS sqsClient,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _sqsClient = sqsClient;
        _scopeFactory = scopeFactory;
        _queueUrl = configuration["AWS:SqsQueueUrl"]
            ?? throw new InvalidOperationException("AWS:SqsQueueUrl não configurado.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SQS Consumer Worker iniciado. Aguardando mensagens...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var request = new ReceiveMessageRequest
                {
                    QueueUrl = _queueUrl,
                    MaxNumberOfMessages = MAX_MESSAGES,
                    WaitTimeSeconds = WAIT_TIME_SECONDS,
                    AttributeNames = new List<string> { "ApproximateReceiveCount" },
                    MessageAttributeNames = new List<string> { "All" }
                };

                var response = await _sqsClient.ReceiveMessageAsync(request, stoppingToken);

                foreach (var message in response.Messages)
                {
                    await ProcessarMensagemAsync(message, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no loop principal do SQS Consumer. Aguardando 5s...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("SQS Consumer Worker finalizado.");
    }

    private async Task ProcessarMensagemAsync(Message message, CancellationToken stoppingToken)
    {
        var tentativa = 1;
        if (message.Attributes.TryGetValue("ApproximateReceiveCount", out var countStr))
            int.TryParse(countStr, out tentativa);

        try
        {
            var evento = JsonSerializer.Deserialize<ClienteCriadoEvento>(message.Body, _jsonOptions);

            if (evento is null)
            {
                _logger.LogWarning("Mensagem não pôde ser deserializada: {Body}", message.Body);
                await DeletarMensagemAsync(message.ReceiptHandle);
                return;
            }

            _logger.LogInformation(
                "Processando mensagem - Cliente {ClienteId} | Tentativa {Tentativa}/{Max}.",
                evento.ClienteId, tentativa, MAX_TENTATIVAS);

            using var scope = _scopeFactory.CreateScope();

            var emailUseCase = scope.ServiceProvider.GetRequiredService<ProcessarEnvioEmailUseCase>();
            await emailUseCase.Executar(
                evento.ClienteId, evento.Nome, evento.Email, evento.Celular, evento.Mensagem);

            var smsUseCase = scope.ServiceProvider.GetRequiredService<ProcessarEnvioSmsUseCase>();
            await smsUseCase.Executar(
                evento.ClienteId, evento.Nome, evento.Email, evento.Celular, evento.Mensagem);

            await DeletarMensagemAsync(message.ReceiptHandle);

            _logger.LogInformation(
                "Mensagem processada com sucesso - Cliente {ClienteId}.", evento.ClienteId);
        }
        catch (Exception ex)
        {
            if (tentativa >= MAX_TENTATIVAS)
            {
                _logger.LogError(ex,
                    "Todas as {Max} tentativas esgotadas para mensagem {MessageId}. Deletando.",
                    MAX_TENTATIVAS, message.MessageId);
                await DeletarMensagemAsync(message.ReceiptHandle);
            }
            else
            {
                _logger.LogWarning(ex,
                    "Tentativa {Tentativa}/{Max} falhou para mensagem {MessageId}. Mensagem voltará à fila.",
                    tentativa, MAX_TENTATIVAS, message.MessageId);
                // No SQS a mensagem volta automaticamente após o visibility timeout
            }
        }
    }

    private async Task DeletarMensagemAsync(string receiptHandle)
    {
        await _sqsClient.DeleteMessageAsync(_queueUrl, receiptHandle);
    }
}