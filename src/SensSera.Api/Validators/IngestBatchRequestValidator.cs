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

        // Only the shape is checked here. Each reading's content is checked by the service, which
        // stores the good ones and reports the rest (ReadingRules): a 400 makes the board drop the
        // whole batch, so it is kept for requests that could never succeed.
        RuleForEach(x => x.Readings).NotNull();
    }
}
