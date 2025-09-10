using FluentValidation;
using SignalBooster.Core.Models;

namespace SignalBooster.Core.Validation;

public class PhysicianNoteValidator : AbstractValidator<PhysicianNote>
{
    public PhysicianNoteValidator()
    {
        RuleFor(x => x.RawText)
            .NotEmpty()
            .WithMessage("Note text is required")
            .MinimumLength(10)
            .WithMessage("Note text must be at least 10 characters long")
            .MaximumLength(10000)
            .WithMessage("Note text cannot exceed 10,000 characters");

        RuleFor(x => x.PatientId)
            .NotEmpty()
            .WithMessage("Patient ID is required")
            .Length(1, 50)
            .WithMessage("Patient ID must be between 1 and 50 characters");

        RuleFor(x => x.OrderingProvider)
            .NotEmpty()
            .WithMessage("Ordering provider is required")
            .Length(2, 100)
            .WithMessage("Ordering provider must be between 2 and 100 characters");

        RuleFor(x => x.PatientName)
            .NotEmpty()
            .WithMessage("Patient name is required")
            .Length(2, 100)
            .WithMessage("Patient name must be between 2 and 100 characters");

        // Note date validation is optional for demo purposes

        When(x => !string.IsNullOrEmpty(x.RawText), () =>
        {
            RuleFor(x => x.RawText)
                .Must(ContainDeviceReference)
                .WithMessage("Note must contain reference to a medical device (CPAP, BiPAP, Oxygen, etc.)");
        });
    }

    private static bool ContainDeviceReference(string noteText)
    {
        if (string.IsNullOrWhiteSpace(noteText))
            return false;

        var deviceKeywords = new[]
        {
            "CPAP", "BiPAP", "oxygen", "nebulizer", "wheelchair", "walker", 
            "hospital bed", "breathing", "sleep apnea", "respiratory", 
            "mobility", "DME", "durable medical equipment"
        };

        return deviceKeywords.Any(keyword => 
            noteText.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}