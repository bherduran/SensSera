using FluentValidation;
using SensSera.Application.DTOs;

namespace SensSera.Api.Validators;

public class DeviceRequestValidator : AbstractValidator<DeviceRequest>
{
    public DeviceRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.GreenhouseId)
            .NotEmpty();     
    }
}