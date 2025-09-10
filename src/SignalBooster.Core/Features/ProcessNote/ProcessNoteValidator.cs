using FluentValidation;
using Microsoft.Extensions.Options;
using SignalBooster.Core.Configuration;

namespace SignalBooster.Core.Features.ProcessNote;

public class ProcessNoteRequestValidator : AbstractValidator<ProcessNoteRequest>
{
    public ProcessNoteRequestValidator(IOptions<SignalBoosterOptions> options)
    {
        var fileOptions = options.Value.Files;

        RuleFor(x => x.FilePath)
            .NotEmpty()
            .WithMessage("File path is required")
            .Must(BeValidPath)
            .WithMessage("File path contains invalid characters");

        When(x => !string.IsNullOrEmpty(x.FilePath), () =>
        {
            RuleFor(x => x.FilePath)
                .Must(path => HasValidExtension(path, fileOptions.SupportedExtensions))
                .WithMessage($"File must have one of these extensions: {string.Join(", ", fileOptions.SupportedExtensions)}");
        });

        When(x => !string.IsNullOrEmpty(x.OutputFileName), () =>
        {
            RuleFor(x => x.OutputFileName)
                .Must(BeValidFileName)
                .WithMessage("Output filename contains invalid characters");
        });
    }

    private static bool BeValidPath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        try
        {
            var invalidChars = Path.GetInvalidPathChars();
            return !filePath.Any(c => invalidChars.Contains(c));
        }
        catch
        {
            return false;
        }
    }

    private static bool HasValidExtension(string filePath, string[] supportedExtensions)
    {
        try
        {
            var extension = Path.GetExtension(filePath);
            return supportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool BeValidFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return true; // Optional field

        try
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return !fileName.Any(c => invalidChars.Contains(c));
        }
        catch
        {
            return false;
        }
    }
}