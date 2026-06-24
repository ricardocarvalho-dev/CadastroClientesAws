using CadastroClientes.Application.DTOs;
using CadastroClientes.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CadastroClientes.Api.Controllers;

[ApiController]
[Route("api/mensageria")]
public class MensageriaController : ControllerBase
{
    private readonly IHistoricoEnvioMensagemRepository _historicoRepository;
    private readonly ILogger<MensageriaController> _logger;

    public MensageriaController(
        IHistoricoEnvioMensagemRepository historicoRepository,
        ILogger<MensageriaController> logger)
    {
        _historicoRepository = historicoRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        try
        {
            var historicos = await _historicoRepository.ListarAsync();

            var dtos = historicos.Select(h => new HistoricoEnvioMensagemDto
            {
                Id = h.Id,
                ClienteId = h.ClienteId,
                Canal = h.Canal,
                Destinatario = h.Destinatario,
                Status = h.Status,
                MensagemErro = h.MensagemErro,
                ProviderMessageId = h.ProviderMessageId,
                DataEnvio = h.DataEnvio
            });

            return Ok(new { mensagem = "Históricos listados com sucesso", historicos = dtos });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Erro ao listar históricos de envio: {ex.Message}");
            return StatusCode(500, new { mensagem = "Erro interno ao listar históricos", erroReal = ex.Message });
        }
    }
}
