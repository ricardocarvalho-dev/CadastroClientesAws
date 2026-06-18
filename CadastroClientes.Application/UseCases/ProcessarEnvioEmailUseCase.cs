using CadastroClientes.Application.DTOs;
using CadastroClientes.Application.Interfaces;
using CadastroClientes.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace CadastroClientes.Application.UseCases;

public class ProcessarEnvioEmailUseCase
{
    private const string CANAL = "Email";
    private readonly IEmailService _emailService;
    private readonly IHistoricoEnvioMensagemRepository _historicoRepository;
    private readonly ILogger<ProcessarEnvioEmailUseCase> _logger;

    public ProcessarEnvioEmailUseCase(
        IEmailService emailService,
        IHistoricoEnvioMensagemRepository historicoRepository,
        ILogger<ProcessarEnvioEmailUseCase> logger)
    {
        _emailService = emailService;
        _historicoRepository = historicoRepository;
        _logger = logger;
    }

    public async Task Executar(Guid clienteId, string nome, string email, string celular, string mensagem)
    {
        // Idempotência: se a fila redentregar a mensagem (comportamento normal do RabbitMQ)
        // e o e-mail já foi enviado com sucesso antes, não envia de novo.
        if (await _historicoRepository.JaEnviadoComSucessoAsync(clienteId, CANAL))
        {
            _logger.LogInformation($"E-mail já enviado anteriormente com sucesso para cliente {clienteId}. Reprocessamento ignorado.");
            return;
        }

        EmailResultadoDto resultado;

        try
        {
            resultado = await _emailService.EnviarAsync(email, nome, mensagem);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Erro inesperado ao enviar e-mail para o cliente {clienteId}: {ex.Message}");
            resultado = new EmailResultadoDto
            {
                Sucesso = false,
                MensagemErro = ex.Message
            };
        }

        var historico = new HistoricoEnvioMensagem
        {
            ClienteId = clienteId,
            Canal = CANAL,
            Destinatario = email,
            Status = resultado.Sucesso ? "Sucesso" : "Falha",
            MensagemErro = resultado.MensagemErro,
            ProviderMessageId = resultado.ProviderMessageId,
            DataEnvio = DateTime.UtcNow
        };

        try
        {
            await _historicoRepository.RegistrarAsync(historico);
        }
        catch (Exception ex)
        {
            // Não relança: o e-mail já foi processado (enviado ou definitivamente rejeitado).
            // Relançar aqui faria o Worker dar nack/requeue, reenviando o e-mail novamente.
            _logger.LogCritical($"FALHA AO REGISTRAR HISTÓRICO (e-mail já processado) para cliente {clienteId}. Status que deveria ter sido salvo: {historico.Status}. Erro: {ex.Message}");
        }

        if (resultado.Sucesso)
            _logger.LogInformation($"E-mail enviado com sucesso para cliente {clienteId} (ProviderMessageId: {resultado.ProviderMessageId})");
        else
            _logger.LogWarning($"Falha ao enviar e-mail para cliente {clienteId}: {resultado.MensagemErro}");
    }
}