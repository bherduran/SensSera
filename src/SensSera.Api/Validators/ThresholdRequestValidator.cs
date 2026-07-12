using FluentValidation;
using SensSera.Application.DTOs;

namespace SensSera.Api.Validators;

public class ThresholdRequestValidator : AbstractValidator<ThresholdRequest>
{
    public ThresholdRequestValidator()
    {
        RuleFor(x => x.GreenhouseId).NotEmpty();
        RuleFor(x => x.Metric).IsInEnum();
        RuleFor(x => x).Must(x => x.MinValue is not null || x.MaxValue is not null)
            .WithMessage("At least one of MinValue or MaxValue must be set");   
        RuleFor(x => x).Must(x => 
                x.MinValue is null || x.MaxValue is null || x.MinValue < x.MaxValue)
            .WithMessage("MinValue must be less than MaxValue");
    }
}