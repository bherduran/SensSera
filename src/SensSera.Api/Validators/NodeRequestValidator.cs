using FluentValidation;
using SensSera.Application.DTOs;

namespace SensSera.Api.Validators;

public sealed class NodeRequestValidator : AbstractValidator<NodeRequest>
{
    public NodeRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.GreenhouseId)
            .NotEmpty();
    }
}

public sealed class NodeChannelRequestValidator : AbstractValidator<NodeChannelRequest>
{
    public NodeChannelRequestValidator()
    {
        // The firmware sends this string with every reading, so keep it to a plain identifier.
        RuleFor(x => x.Channel)
            .NotEmpty()
            .MaximumLength(32)
            .Matches("^[a-z0-9_]+$")
            .WithMessage("Channel may contain only lowercase letters, digits and underscores.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Metric)
            .IsInEnum();
    }
}
