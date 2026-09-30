using FluentValidation;
using SensSera.Application.DTOs;

namespace SensSera.Api.Validators;

public sealed class IngestBatchRequestValidator : AbstractValidator<IngestBatchRequest>
{
    // Enough for a device flushing a buffer after a short outage; bounds per-request DB work.
    public const int MaxReadings = 100;

    public IngestBatchRequestValidator()
    {
        RuleFor(x => x.Readings)
            .NotEmpty()
            .Must(r => r.Count <= MaxReadings)
            .WithMessage($"A batch can contain at most {MaxReadings} readings");

        // Every reading gets the same checks as the single-reading endpoint, and the whole batch
        // is rejected up front — no half-written batch when item N is invalid.
        RuleForEach(x => x.Readings).SetValidator(new IngestRequestValidator());
    }
}
