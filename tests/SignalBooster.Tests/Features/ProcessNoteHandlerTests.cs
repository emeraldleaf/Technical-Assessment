using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SignalBooster.Core.Features;
using SignalBooster.Core.Features.ProcessNote;
using SignalBooster.Core.Models;
using SignalBooster.Core.Services;

namespace SignalBooster.Tests.Features;

public class ProcessNoteHandlerTests
{
    private readonly IFileService _fileService;
    private readonly INoteParser _noteParser;
    private readonly IApiService _apiService;
    private readonly IValidator<ProcessNoteRequest> _requestValidator;
    private readonly ILogger<ProcessNoteHandler> _logger;
    private readonly ProcessNoteHandler _handler;

    public ProcessNoteHandlerTests()
    {
        _fileService = Substitute.For<IFileService>();
        _noteParser = Substitute.For<INoteParser>();
        _apiService = Substitute.For<IApiService>();
        _requestValidator = Substitute.For<IValidator<ProcessNoteRequest>>();
        _logger = Substitute.For<ILogger<ProcessNoteHandler>>();

        _handler = new ProcessNoteHandler(
            _fileService,
            _noteParser,
            _apiService,
            _requestValidator,
            _logger);

        // Setup default successful validation
        _requestValidator.ValidateAsync(Arg.Any<ProcessNoteRequest>())
            .Returns(new ValidationResult());
    }

    [Fact]
    public async Task Handle_WithValidRequest_ShouldReturnSuccessResponse()
    {
        // Arrange
        var request = new ProcessNoteRequest("test.txt", false);
        var noteText = "Patient needs CPAP therapy. Ordered by Dr. Smith.";
        var physicianNote = CreateValidPhysicianNote();
        var deviceOrder = CreateValidDeviceOrder();
        var apiResponse = CreateValidApiResponse();

        _fileService.ReadNoteFromFileAsync("test.txt")
            .Returns(Core.Common.Result.CreateSuccess(noteText));
        
        _noteParser.ParseNoteFromText(noteText)
            .Returns(Core.Common.Result.CreateSuccess(physicianNote));
        
        _noteParser.ExtractDeviceOrder(physicianNote)
            .Returns(Core.Common.Result.CreateSuccess(deviceOrder));
        
        _apiService.SendDeviceOrderAsync(deviceOrder)
            .Returns(Core.Common.Result.CreateSuccess(apiResponse));

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.DeviceType.Should().Be("CPAP");
        result.Value.OrderId.Should().Be("12345");
        result.Value.Status.Should().Be("Accepted");
        result.Value.ProcessedFilePath.Should().Be("test.txt");
    }

    [Fact]
    public async Task Handle_WithSaveOutputEnabled_ShouldSaveOutputAndIncludeFilePath()
    {
        // Arrange
        var request = new ProcessNoteRequest("test.txt", SaveOutput: true);
        var noteText = "Patient needs CPAP therapy. Ordered by Dr. Smith.";
        var physicianNote = CreateValidPhysicianNote();
        var deviceOrder = CreateValidDeviceOrder();
        var apiResponse = CreateValidApiResponse();
        var outputPath = "/output/device_order_12345.json";

        SetupSuccessfulProcessing(request.FilePath, noteText, physicianNote, deviceOrder, apiResponse);
        
        _fileService.WriteOutputAsync(Arg.Any<string>(), null)
            .Returns(Core.Common.Result.CreateSuccess(outputPath));

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.OutputFilePath.Should().Be(outputPath);
        
        // Verify output was saved
        await _fileService.Received(1).WriteOutputAsync(Arg.Any<string>(), null);
    }

    [Fact]
    public async Task Handle_WithCustomOutputFileName_ShouldUseCustomFileName()
    {
        // Arrange
        var request = new ProcessNoteRequest("test.txt", SaveOutput: true, OutputFileName: "custom_output.json");
        var noteText = "Patient needs CPAP therapy. Ordered by Dr. Smith.";
        var physicianNote = CreateValidPhysicianNote();
        var deviceOrder = CreateValidDeviceOrder();
        var apiResponse = CreateValidApiResponse();

        SetupSuccessfulProcessing(request.FilePath, noteText, physicianNote, deviceOrder, apiResponse);
        
        _fileService.WriteOutputAsync(Arg.Any<string>(), "custom_output.json")
            .Returns(Core.Common.Result.CreateSuccess("/output/custom_output.json"));

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        // Verify custom filename was used
        await _fileService.Received(1).WriteOutputAsync(Arg.Any<string>(), "custom_output.json");
    }

    [Fact]
    public async Task Handle_WithRequestValidationFailure_ShouldReturnValidationError()
    {
        // Arrange
        var request = new ProcessNoteRequest("", false);
        var validationFailures = new List<ValidationFailure>
        {
            new("FilePath", "File path is required")
        };
        
        _requestValidator.ValidateAsync(request)
            .Returns(new ValidationResult(validationFailures));

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Validation.InvalidFormat");
        
        // Verify no further processing occurred
        await _fileService.DidNotReceive().ReadNoteFromFileAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_WithFileReadFailure_ShouldReturnFileError()
    {
        // Arrange
        var request = new ProcessNoteRequest("nonexistent.txt", false);
        var fileError = Core.Common.Error.NotFound("File.NotFound", "File not found");

        _fileService.ReadNoteFromFileAsync("nonexistent.txt")
            .Returns(Core.Common.Result.CreateFailure<string>(fileError));

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(fileError);
        
        // Verify no further processing occurred
        _noteParser.DidNotReceive().ParseNoteFromText(Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_WithNoteParsingFailure_ShouldReturnParsingError()
    {
        // Arrange
        var request = new ProcessNoteRequest("test.txt", false);
        var noteText = "Invalid note text";
        var parsingError = Core.Common.Error.Validation("Parsing.Failed", "Failed to parse note");

        _fileService.ReadNoteFromFileAsync("test.txt")
            .Returns(Core.Common.Result.CreateSuccess(noteText));
        
        _noteParser.ParseNoteFromText(noteText)
            .Returns(Core.Common.Result.CreateFailure<PhysicianNote>(parsingError));

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(parsingError);
        
        // Verify no further processing occurred
        _noteParser.DidNotReceive().ExtractDeviceOrder(Arg.Any<PhysicianNote>());
    }

    [Fact]
    public async Task Handle_WithDeviceOrderExtractionFailure_ShouldReturnExtractionError()
    {
        // Arrange
        var request = new ProcessNoteRequest("test.txt", false);
        var noteText = "Patient note text";
        var physicianNote = CreateValidPhysicianNote();
        var extractionError = Core.Common.Error.Validation("Extraction.Failed", "Failed to extract device order");

        _fileService.ReadNoteFromFileAsync("test.txt")
            .Returns(Core.Common.Result.CreateSuccess(noteText));
        
        _noteParser.ParseNoteFromText(noteText)
            .Returns(Core.Common.Result.CreateSuccess(physicianNote));
        
        _noteParser.ExtractDeviceOrder(physicianNote)
            .Returns(Core.Common.Result.CreateFailure<DeviceOrder>(extractionError));

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(extractionError);
        
        // Verify no API call was made
        await _apiService.DidNotReceive().SendDeviceOrderAsync(Arg.Any<DeviceOrder>());
    }

    [Fact]
    public async Task Handle_WithApiFailure_ShouldReturnApiError()
    {
        // Arrange
        var request = new ProcessNoteRequest("test.txt", false);
        var noteText = "Patient needs CPAP therapy. Ordered by Dr. Smith.";
        var physicianNote = CreateValidPhysicianNote();
        var deviceOrder = CreateValidDeviceOrder();
        var apiError = Core.Common.Error.Failure("Api.NetworkError", "Network error occurred");

        _fileService.ReadNoteFromFileAsync("test.txt")
            .Returns(Core.Common.Result.CreateSuccess(noteText));
        
        _noteParser.ParseNoteFromText(noteText)
            .Returns(Core.Common.Result.CreateSuccess(physicianNote));
        
        _noteParser.ExtractDeviceOrder(physicianNote)
            .Returns(Core.Common.Result.CreateSuccess(deviceOrder));
        
        _apiService.SendDeviceOrderAsync(deviceOrder)
            .Returns(Core.Common.Result.CreateFailure<DeviceOrderResponse>(apiError));

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(apiError);
    }

    [Fact]
    public async Task Handle_WithSaveOutputFailure_ShouldContinueAndLogWarning()
    {
        // Arrange
        var request = new ProcessNoteRequest("test.txt", SaveOutput: true);
        var noteText = "Patient needs CPAP therapy. Ordered by Dr. Smith.";
        var physicianNote = CreateValidPhysicianNote();
        var deviceOrder = CreateValidDeviceOrder();
        var apiResponse = CreateValidApiResponse();
        var saveError = Core.Common.Error.Failure("File.WriteError", "Failed to save output");

        SetupSuccessfulProcessing(request.FilePath, noteText, physicianNote, deviceOrder, apiResponse);
        
        _fileService.WriteOutputAsync(Arg.Any<string>(), null)
            .Returns(Core.Common.Result.CreateFailure<string>(saveError));

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsSuccess.Should().BeTrue(); // Should still succeed overall
        result.Value!.OutputFilePath.Should().BeNull(); // But output path should be null
        
        // Verify warning was logged (check that logger was called)
        _logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Failed to save output")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Handle_ShouldCallServicesInCorrectOrder()
    {
        // Arrange
        var request = new ProcessNoteRequest("test.txt", false);
        var noteText = "Patient needs CPAP therapy. Ordered by Dr. Smith.";
        var physicianNote = CreateValidPhysicianNote();
        var deviceOrder = CreateValidDeviceOrder();
        var apiResponse = CreateValidApiResponse();

        SetupSuccessfulProcessing(request.FilePath, noteText, physicianNote, deviceOrder, apiResponse);

        // Act
        await _handler.Handle(request);

        // Assert - Verify calls were made in the correct order
        Received.InOrder(async () =>
        {
            await _requestValidator.ValidateAsync(request);
            await _fileService.ReadNoteFromFileAsync("test.txt");
            _noteParser.ParseNoteFromText(noteText);
            _noteParser.ExtractDeviceOrder(physicianNote);
            await _apiService.SendDeviceOrderAsync(deviceOrder);
        });
    }

    [Fact]
    public async Task Handle_ShouldLogProcessingStartAndCompletion()
    {
        // Arrange
        var request = new ProcessNoteRequest("test.txt", false);
        var noteText = "Patient needs CPAP therapy. Ordered by Dr. Smith.";
        var physicianNote = CreateValidPhysicianNote();
        var deviceOrder = CreateValidDeviceOrder();
        var apiResponse = CreateValidApiResponse();

        SetupSuccessfulProcessing(request.FilePath, noteText, physicianNote, deviceOrder, apiResponse);

        // Act
        await _handler.Handle(request);

        // Assert - Verify logging occurred
        _logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Starting physician note processing")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
        
        _logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Successfully processed physician note")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    private void SetupSuccessfulProcessing(
        string filePath,
        string noteText,
        PhysicianNote physicianNote,
        DeviceOrder deviceOrder,
        DeviceOrderResponse apiResponse)
    {
        _fileService.ReadNoteFromFileAsync(filePath)
            .Returns(Core.Common.Result.CreateSuccess(noteText));
        
        _noteParser.ParseNoteFromText(noteText)
            .Returns(Core.Common.Result.CreateSuccess(physicianNote));
        
        _noteParser.ExtractDeviceOrder(physicianNote)
            .Returns(Core.Common.Result.CreateSuccess(deviceOrder));
        
        _apiService.SendDeviceOrderAsync(deviceOrder)
            .Returns(Core.Common.Result.CreateSuccess(apiResponse));
    }

    private static PhysicianNote CreateValidPhysicianNote()
    {
        return new PhysicianNote(
            PatientName: "John Doe",
            DateOfBirth: "01/01/1980",
            Diagnosis: "Sleep Apnea",
            Prescription: "CPAP therapy prescribed",
            Usage: "Nightly use",
            OrderingProvider: "Dr. Smith",
            RawText: "Patient needs CPAP therapy with full face mask for sleep apnea treatment. Ordered by Dr. Smith."
        )
        {
            PatientId = "12345",
            NoteDate = DateTime.UtcNow.AddDays(-1)
        };
    }

    private static DeviceOrder CreateValidDeviceOrder()
    {
        return new DeviceOrder(
            Device: "CPAP",
            MaskType: "full face",
            AddOns: ["humidifier"],
            Qualifier: "AHI > 20",
            OrderingProvider: "Dr. Smith",
            Diagnosis: "Sleep Apnea",
            PatientName: "John Doe",
            DateOfBirth: "01/01/1980"
        )
        {
            PatientId = "12345",
            Specifications = new Dictionary<string, object>
            {
                ["MaskType"] = "full face",
                ["Pressure"] = "10 cmH2O"
            }
        };
    }

    private static DeviceOrderResponse CreateValidApiResponse()
    {
        return new DeviceOrderResponse
        {
            OrderId = "12345",
            Status = "Accepted",
            ProcessedAt = DateTime.UtcNow,
            Message = "Order processed successfully"
        };
    }
}