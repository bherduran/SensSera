using FluentValidation;
using SensSera.Application.DTOs;
using SensSera.Application.Ingestion;

namespace SensSera.Api.Validators;

// The single-reading endpoint rejects with a 400 before the service runs. The rules themselves live
// in ReadingRules because a batch applies them per reading and skips failures instead.
public sealed class IngestRequestValidator : AbstractValidator<IngestRequest>
{
    public IngestRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x).Custom((reading, context) =>
        {
            if (ReadingRules.Check(reading, timeProvider.GetUtcNow().UtcDateTime) is { } reason)
                context.AddFailure(char.ToUpperInvariant(reason[0]) + reason[1..]);
        });
    }
}
