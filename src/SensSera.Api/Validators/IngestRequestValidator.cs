using FluentValidation;
using SensSera.Application.DTOs;

namespace SensSera.Api.Validators;

public sealed class IngestRequestValidator : AbstractValidator<IngestRequest>
{
    private static readonly Dictionary<string, (double Min, double Max)> Ranges = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Temperature"] = (-40, 80),
        ["Humidity"] = (0, 100),
        ["Co2"] = (0, 5000),
        ["Light"] = (0, 100_000),
        ["SoilMoisture"] = (0, 100),
        ["Pressure"] = (800, 1200),
    };

    public IngestRequestValidator()
    {
        RuleFor(x => x.Metric)
            .NotEmpty()
            .Must(m => Ranges.ContainsKey(m))
            .WithMessage("Unknown metric. Valid: Temperature, Humidity, Co2, Light, SoilMoisture, Pressure");

        RuleFor(x => x.Value)
            .Must((req, val) =>
            {
                if (!Ranges.TryGetValue(req.Metric ?? "", out var range)) return true;
                return val >= range.Min && val <= range.Max;
            })
            .WithMessage("Value is out of valid range for the given metric");

        RuleFor(x => x.RecordedAt)
            .NotEmpty();               
    }
}