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
    private const int MAX_TENTATIVAS = 3;
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

            // Lê tentativa atual do header (padrão 1)
            var tentativa = 1;
            if (ea.BasicProperties.Headers != null &&
                ea.BasicProperties.Headers.TryGetValue("x-tentativa", out var t))
                tentativa = Convert.ToInt32(t);

            try
            {
                var evento = JsonSerializer.Deserialize<ClienteCriadoEvento>(json, _jsonOptions);

                if (evento is null)
                {
                    _logger.LogWarning("Mensagem recebida não pôde ser deserializada: {Json}", json);
                    _channel.BasicAck(ea.DeliveryTag, multiple: false);
                    return;
                }

                _logger.LogInformation(
                    "Processando mensagem - Cliente {ClienteId} | Tentativa {Tentativa}/{Max}.",
                    evento.ClienteId, tentativa, MAX_TENTATIVAS);

                using var scope = _scopeFactory.CreateScope();

                // Email SEMPRE
                var emailUseCase = scope.ServiceProvider.GetRequiredService<ProcessarEnvioEmailUseCase>();
                await emailUseCase.Executar(evento.ClienteId, evento.Nome, evento.Email, evento.Celular, evento.Mensagem);

                // SMS SEMPRE
                var smsUseCase = scope.ServiceProvider.GetRequiredService<ProcessarEnvioSmsUseCase>();
                await smsUseCase.Executar(evento.ClienteId, evento.Nome, evento.Email, evento.Celular, evento.Mensagem);

                _channel.BasicAck(ea.DeliveryTag, multiple: false);

                _logger.LogInformation(
                    "Mensagem processada com sucesso - Cliente {ClienteId} | Tentativa {Tentativa}/{Max}.",
                    evento.ClienteId, tentativa, MAX_TENTATIVAS);
            }
            catch (Exception ex)
            {
                if (tentativa < MAX_TENTATIVAS)
                {
                    _logger.LogWarning(
                        ex,
                        "Tentativa {Tentativa}/{Max} falhou para mensagem. Aguardando 30s para retentar.",
                        tentativa, MAX_TENTATIVAS);

                    await Task.Delay(TimeSpan.FromSeconds(30));

                    // Republica na fila com contador incrementado
                    var props = _channel.CreateBasicProperties();
                    props.Persistent = true;
                    props.Headers = new Dictionary<string, object> { { "x-tentativa", tentativa + 1 } };
                    _channel.BasicPublish("", QUEUE_NAME, props, ea.Body);
                    _channel.BasicAck(ea.DeliveryTag, multiple: false);
                }
                else
                {
                    _logger.LogError(
                        ex,
                        "Todas as {Max} tentativas esgotadas. Mensagem descartada da fila.",
                        MAX_TENTATIVAS);

                    // Remove da fila sem requeue — erro já gravado no banco pelo UseCase
                    _channel.BasicAck(ea.DeliveryTag, multiple: false);
                }
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