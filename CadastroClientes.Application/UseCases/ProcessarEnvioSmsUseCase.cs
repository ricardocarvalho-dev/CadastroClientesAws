using CadastroClientes.Application.DTOs;
using CadastroClientes.Application.Interfaces;
using CadastroClientes.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace CadastroClientes.Application.UseCases;

public class ProcessarEnvioSmsUseCase
{
    private const string CANAL = "SMS";
    private readonly ISmsService _smsService;
    private readonly IHistoricoEnvioMensagemRepository _historicoRepository;
    private readonly ILogger<ProcessarEnvioSmsUseCase> _logger;

    public ProcessarEnvioSmsUseCase(
        ISmsService smsService,
        IHistoricoEnvioMensagemRepository historicoRepository,
        ILogger<ProcessarEnvioSmsUseCase> logger)
    {
        _smsService = smsService;
        _historicoRepository = historicoRepository;
        _logger = logger;
    }

    public async Task Executar(Guid clienteId, string nome, string email, string celular, string mensagem)
    {
        if (await _historicoRepository.JaEnviadoComSucessoAsync(clienteId, CANAL))
        {
            _logger.LogInformation("SMS já enviado anteriormente com sucesso para cliente {ClienteId}. Reprocessamento ignorado.", clienteId);
            return;
        }

        SmsResultadoDto resultado;

        try
        {
            resultado = await _smsService.EnviarAsync(celular, nome, mensagem);
        }
        catch (Exception ex)
        {
            _logger.LogError("Erro inesperado ao enviar SMS para o cliente {ClienteId}: {Erro}", clienteId, ex.Message);
            resultado = new SmsResultadoDto
            {
                Sucesso = false,
                MensagemErro = ex.Message
            };
        }

        var historico = new HistoricoEnvioMensagem
        {
            ClienteId = clienteId,
            Canal = CANAL,
            Destinatario = celular,
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
            _logger.LogCritical("FALHA AO REGISTRAR HISTÓRICO (SMS já processado) para cliente {ClienteId}. Status: {Status}. Erro: {Erro}",
                clienteId, historico.Status, ex.Message);
        }

        if (resultado.Sucesso)
            _logger.LogInformation("SMS enviado com sucesso para cliente {ClienteId} (Sid: {Sid})", clienteId, resultado.ProviderMessageId);
        else
            _logger.LogWarning("Falha ao enviar SMS para cliente {ClienteId}: {Erro}", clienteId, resultado.MensagemErro);
    }
}