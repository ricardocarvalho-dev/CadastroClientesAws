using System.Text;
using System.Text.Json;
using CadastroClientes.Application.DTOs;
using CadastroClientes.Application.UseCases;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CadastroClientes.Worker;

public class RabbitMqConsumerWorker : BackgroundService
{
    private const string QUEUE_NAME = "fila_cadastro_clientes";
    private readonly ILogger<RabbitMqConsumerWorker> _logger;
    private readonly IConnectionFactory _connectionFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private IConnection? _connection;
    private IModel? _channel;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public RabbitMqConsumerWorker(
        ILogger<RabbitMqConsumerWorker> logger,
        IConnectionFactory connectionFactory,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _connectionFactory = connectionFactory;
        _scopeFactory = scopeFactory;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _connection = _connectionFactory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.QueueDeclare(
            queue: QUEUE_NAME,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        _logger.LogInformation("Conectado ao RabbitMQ. Aguardando mensagens na fila {Fila}.", QUEUE_NAME);

        return base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel is null)
            throw new InvalidOperationException("Canal RabbitMQ não inicializado.");

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);

            try
            {
                var evento = JsonSerializer.Deserialize<ClienteCriadoEvento>(json, _jsonOptions);

                if (evento is null)
                {
                    _logger.LogWarning("Mensagem recebida não pôde ser deserializada: {Json}", json);
                    _channel.BasicAck(ea.DeliveryTag, multiple: false);
                    return;
                }

                _logger.LogInformation("Mensagem recebida para cliente {ClienteId}. Canal: {Canal}", evento.ClienteId, evento.Canal);

                using var scope = _scopeFactory.CreateScope();

                // Email SEMPRE
                var emailUseCase = scope.ServiceProvider.GetRequiredService<ProcessarEnvioEmailUseCase>();
                await emailUseCase.Executar(evento.ClienteId, evento.Nome, evento.Email, evento.Celular, evento.Mensagem);

                // SMS SEMPRE
                var smsUseCase = scope.ServiceProvider.GetRequiredService<ProcessarEnvioSmsUseCase>();
                await smsUseCase.Executar(evento.ClienteId, evento.Nome, evento.Email, evento.Celular, evento.Mensagem);

                _channel.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar mensagem da fila. Mensagem será reenviada (nack).");
                _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        _channel.BasicConsume(
            queue: QUEUE_NAME,
            autoAck: false,
            consumer: consumer);

        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _channel?.Close();
        _connection?.Close();
        _logger.LogInformation("Worker de mensageria finalizado.");
        return base.StopAsync(cancellationToken);
    }
}