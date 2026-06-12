using FluentValidation;
using SensSera.Application.DTOs;

namespace SensSera.Api.Validators;

public class GreenhouseRequestValidator : AbstractValidator<GreenhouseRequest>
{
    public GreenhouseRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Location)
            .MaximumLength(200);    
    }
}