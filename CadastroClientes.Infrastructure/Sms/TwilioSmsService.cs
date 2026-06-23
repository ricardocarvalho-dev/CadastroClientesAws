using CadastroClientes.Application.DTOs;
using CadastroClientes.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace CadastroClientes.Infrastructure.Sms;

public class TwilioSmsService : ISmsService
{
    private readonly ILogger<TwilioSmsService> _logger;
    private readonly string _messagingServiceSid;

    public TwilioSmsService(IConfiguration configuration, ILogger<TwilioSmsService> logger)
    {
        _logger = logger;

        var accountSid = configuration["Twilio:AccountSid"]
            ?? throw new InvalidOperationException("Twilio:AccountSid não configurada");
        var authToken = configuration["Twilio:AuthToken"]
            ?? throw new InvalidOperationException("Twilio:AuthToken não configurada");
        _messagingServiceSid = configuration["Twilio:MessagingServiceSid"]
            ?? throw new InvalidOperationException("Twilio:MessagingServiceSid não configurada");

        TwilioClient.Init(accountSid, authToken);
    }

    public async Task<SmsResultadoDto> EnviarAsync(string celular, string nomeCliente, string mensagem)
    {
        try
        {
            var numeroFormatado = FormatarCelular(celular);  // ← formata aqui

            var message = await MessageResource.CreateAsync(
                to: new Twilio.Types.PhoneNumber(numeroFormatado),
                messagingServiceSid: _messagingServiceSid,
                body: $"Olá, {nomeCliente}! Recebemos seu cadastro: {mensagem}");

            return new SmsResultadoDto
            {
                Sucesso = true,
                ProviderMessageId = message.Sid
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("Erro ao enviar SMS via Twilio para {Celular}: {Erro}", celular, ex.Message);
            return new SmsResultadoDto
            {
                Sucesso = false,
                MensagemErro = ex.Message
            };
        }
    }

    private static string FormatarCelular(string celular)
    {
        var soDigitos = new string(celular.Where(char.IsDigit).ToArray());

        if (soDigitos.Length == 13) return "+" + soDigitos;      // ex: 5571991147042
        if (soDigitos.Length == 11) return "+55" + soDigitos;    // ex: 71991147042
        if (soDigitos.Length == 10) return "+550" + soDigitos;   // ex: sem o 9

        return "+" + soDigitos; // fallback
    }

}