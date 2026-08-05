using FluentValidation;
using SensSera.Application.DTOs;

namespace SensSera.Api.Validators;

public sealed class AskRequestValidator : AbstractValidator<AskRequest>
{
    public AskRequestValidator()
    {
        // Bound the question length — it becomes untrusted user content in the prompt.
        RuleFor(x => x.Question)
            .NotEmpty()
            .MaximumLength(500);
    }
}
