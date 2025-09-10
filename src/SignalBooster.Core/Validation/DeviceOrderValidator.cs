using FluentValidation;
using SignalBooster.Core.Models;

namespace SignalBooster.Core.Validation;

public class DeviceOrderValidator : AbstractValidator<DeviceOrder>
{
    public DeviceOrderValidator()
    {
        RuleFor(x => x.DeviceType)
            .NotEmpty()
            .WithMessage("Device type is required")
            .Must(BeValidDeviceType)
            .WithMessage("Device type must be one of: CPAP, BiPAP, Oxygen, Nebulizer, Wheelchair, Walker, Hospital Bed");

        RuleFor(x => x.Provider)
            .NotEmpty()
            .WithMessage("Provider is required")
            .Length(1, 100)
            .WithMessage("Provider must be between 1 and 100 characters");

        RuleFor(x => x.PatientId)
            .NotEmpty()
            .WithMessage("Patient ID is required")
            .Length(1, 50)
            .WithMessage("Patient ID must be between 1 and 50 characters");

        RuleFor(x => x.Specifications)
            .NotNull()
            .WithMessage("Specifications are required");

        When(x => x.DeviceType?.ToUpperInvariant() == "CPAP", () =>
        {
            RuleFor(x => x.Specifications)
                .Must(HaveValidCpapSpecs)
                .WithMessage("CPAP orders must include mask type and pressure settings");
        });

        When(x => x.DeviceType?.ToUpperInvariant() == "OXYGEN", () =>
        {
            RuleFor(x => x.Specifications)
                .Must(HaveValidOxygenSpecs)
                .WithMessage("Oxygen orders must include flow rate and delivery method");
        });
    }

    private static bool BeValidDeviceType(string? deviceType)
    {
        if (string.IsNullOrWhiteSpace(deviceType))
            return false;

        var validTypes = new[] { "CPAP", "BiPAP", "Oxygen", "Nebulizer", "Wheelchair", "Walker", "Hospital Bed" };
        return validTypes.Contains(deviceType, StringComparer.OrdinalIgnoreCase);
    }

    private static bool HaveValidCpapSpecs(Dictionary<string, object>? specifications)
    {
        if (specifications == null)
            return false;

        return specifications.ContainsKey("MaskType") && 
               (specifications.ContainsKey("PressureMin") || specifications.ContainsKey("Pressure"));
    }

    private static bool HaveValidOxygenSpecs(Dictionary<string, object>? specifications)
    {
        if (specifications == null)
            return false;

        return specifications.ContainsKey("FlowRate") && 
               specifications.ContainsKey("DeliveryMethod");
    }
}