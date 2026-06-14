using FluentValidation;
using CadastroClientes.Application.DTOs;

namespace CadastroClientes.Application.Validators;

public class CriarClienteDtoValidator : AbstractValidator<CriarClienteDto>
{
    public CriarClienteDtoValidator()
    {
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Nome é obrigatório")
            .MinimumLength(3).WithMessage("Nome deve ter no mínimo 3 caracteres")
            .MaximumLength(150).WithMessage("Nome não pode exceder 150 caracteres");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email é obrigatório")
            .EmailAddress().WithMessage("Email inválido")
            .MaximumLength(255).WithMessage("Email não pode exceder 255 caracteres");

        RuleFor(x => x.Celular)
            .NotEmpty().WithMessage("Celular é obrigatório")
            .Matches(@"^\d{10,11}$")
            .WithMessage("Celular inválido")
            .MaximumLength(11).WithMessage("Celular não pode exceder 11 caracteres");
    }
}
