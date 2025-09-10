using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SignalBooster.Core.Configuration;
using SignalBooster.Core.Models;
using SignalBooster.Core.Services;
using System.Net;
using System.Text;
using System.Text.Json;

namespace SignalBooster.Tests.Services;

public class ApiServiceTests
{
    private readonly ILogger<ApiService> _logger;
    private readonly IOptions<SignalBoosterOptions> _options;
    private readonly HttpClient _httpClient;
    private readonly MockHttpMessageHandler _mockHandler;
    private readonly ApiService _apiService;

    public ApiServiceTests()
    {
        _logger = Substitute.For<ILogger<ApiService>>();
        _mockHandler = new MockHttpMessageHandler();
        _httpClient = new HttpClient(_mockHandler);
        
        var signalBoosterOptions = new SignalBoosterOptions
        {
            Api = new ApiOptions
            {
                BaseUrl = "https://test-api.com",
                ExtractEndpoint = "/DrExtract",
                TimeoutSeconds = 5,
                RetryCount = 2,
                RetryDelaySeconds = 1
            }
        };
        
        _options = Substitute.For<IOptions<SignalBoosterOptions>>();
        _options.Value.Returns(signalBoosterOptions);
        
        _apiService = new ApiService(_httpClient, _logger, _options);
    }

    [Fact]
    public async Task SendDeviceOrderAsync_WithValidOrder_ShouldReturnSuccess()
    {
        // Arrange
        var deviceOrder = CreateValidDeviceOrder();
        var expectedResponse = new DeviceOrderResponse
        {
            OrderId = "12345",
            Status = "Accepted",
            ProcessedAt = DateTime.UtcNow,
            Message = "Order processed successfully"
        };
        
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        _mockHandler.SetResponse(HttpStatusCode.OK, JsonSerializer.Serialize(expectedResponse, jsonOptions));

        // Act
        var result = await _apiService.SendDeviceOrderAsync(deviceOrder);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.OrderId.Should().Be("12345");
        result.Value.Status.Should().Be("Accepted");
        result.Value.Message.Should().Be("Order processed successfully");
    }

    [Fact]
    public async Task SendDeviceOrderAsync_WithCreatedResponse_ShouldReturnSuccess()
    {
        // Arrange
        var deviceOrder = CreateValidDeviceOrder();
        var expectedResponse = new DeviceOrderResponse
        {
            OrderId = "67890",
            Status = "Created",
            ProcessedAt = DateTime.UtcNow,
            Message = "New order created"
        };
        
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        _mockHandler.SetResponse(HttpStatusCode.Created, JsonSerializer.Serialize(expectedResponse, jsonOptions));

        // Act
        var result = await _apiService.SendDeviceOrderAsync(deviceOrder);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.OrderId.Should().Be("67890");
        result.Value.Status.Should().Be("Created");
    }

    [Fact]
    public async Task SendDeviceOrderAsync_WithEmptySuccessResponse_ShouldReturnFallbackResponse()
    {
        // Arrange
        var deviceOrder = CreateValidDeviceOrder();
        _mockHandler.SetResponse(HttpStatusCode.OK, "");

        // Act
        var result = await _apiService.SendDeviceOrderAsync(deviceOrder);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Status.Should().Be("Accepted");
        result.Value.Message.Should().Be("Order processed successfully");
        result.Value.OrderId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SendDeviceOrderAsync_WithBadRequest_ShouldReturnBadRequestError()
    {
        // Arrange
        var deviceOrder = CreateValidDeviceOrder();
        var errorMessage = "Invalid device type specified";
        _mockHandler.SetResponse(HttpStatusCode.BadRequest, errorMessage);

        // Act
        var result = await _apiService.SendDeviceOrderAsync(deviceOrder);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Api.BadRequest");
        result.FirstError.Description.Should().Contain(errorMessage);
    }

    [Fact]
    public async Task SendDeviceOrderAsync_WithUnauthorized_ShouldReturnUnauthorizedError()
    {
        // Arrange
        var deviceOrder = CreateValidDeviceOrder();
        _mockHandler.SetResponse(HttpStatusCode.Unauthorized, "Authentication failed");

        // Act
        var result = await _apiService.SendDeviceOrderAsync(deviceOrder);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Api.Unauthorized");
    }

    [Fact]
    public async Task SendDeviceOrderAsync_WithServiceUnavailable_ShouldReturnServiceUnavailableError()
    {
        // Arrange
        var deviceOrder = CreateValidDeviceOrder();
        _mockHandler.SetResponse(HttpStatusCode.ServiceUnavailable, "Service temporarily unavailable");

        // Act
        var result = await _apiService.SendDeviceOrderAsync(deviceOrder);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Api.ServiceUnavailable");
    }

    [Fact]
    public async Task SendDeviceOrderAsync_WithInternalServerError_ShouldReturnUnexpectedResponseError()
    {
        // Arrange
        var deviceOrder = CreateValidDeviceOrder();
        _mockHandler.SetResponse(HttpStatusCode.InternalServerError, "Internal server error occurred");

        // Act
        var result = await _apiService.SendDeviceOrderAsync(deviceOrder);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Api.UnexpectedResponse");
        result.FirstError.Description.Should().Contain("500");
    }

    [Fact]
    public async Task SendDeviceOrderAsync_WithInvalidJson_ShouldReturnDeserializationError()
    {
        // Arrange
        var deviceOrder = CreateValidDeviceOrder();
        _mockHandler.SetResponse(HttpStatusCode.OK, "invalid json response");

        // Act
        var result = await _apiService.SendDeviceOrderAsync(deviceOrder);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Api.DeserializationError");
    }

    [Fact]
    public async Task SendDeviceOrderAsync_WithHttpRequestException_ShouldRetryAndThenReturnNetworkError()
    {
        // Arrange
        var deviceOrder = CreateValidDeviceOrder();
        _mockHandler.SetException(new HttpRequestException("Network error"));

        // Act
        var result = await _apiService.SendDeviceOrderAsync(deviceOrder);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Api.NetworkError");
        result.FirstError.Description.Should().Contain("Network error");
        
        // Verify retries happened (initial attempt + 2 retries = 3 total)
        _mockHandler.RequestCount.Should().Be(3);
    }

    [Fact]
    public async Task SendDeviceOrderAsync_WithTimeout_ShouldReturnTimeoutError()
    {
        // Arrange
        var deviceOrder = CreateValidDeviceOrder();
        _mockHandler.SetException(new TaskCanceledException("Request timeout", new TimeoutException()));

        // Act
        var result = await _apiService.SendDeviceOrderAsync(deviceOrder);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Api.Timeout");
    }

    [Fact]
    public async Task SendDeviceOrderAsync_WithNullDeviceOrder_ShouldReturnValidationError()
    {
        // Act
        var result = await _apiService.SendDeviceOrderAsync(null!);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Validation.MissingRequiredField");
    }

    [Fact]
    public async Task SendDeviceOrderAsync_WithRetryableErrorThenSuccess_ShouldRetryAndSucceed()
    {
        // Arrange
        var deviceOrder = CreateValidDeviceOrder();
        var successResponse = new DeviceOrderResponse
        {
            OrderId = "retry-success",
            Status = "Accepted",
            ProcessedAt = DateTime.UtcNow,
            Message = "Order processed after retry"
        };

        // First call fails, second succeeds
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        _mockHandler.SetSequentialResponses(
            new HttpRequestException("Temporary network error"),
            (HttpStatusCode.OK, JsonSerializer.Serialize(successResponse, jsonOptions))
        );

        // Act
        var result = await _apiService.SendDeviceOrderAsync(deviceOrder);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.OrderId.Should().Be("retry-success");
        _mockHandler.RequestCount.Should().Be(2);
    }

    [Fact]
    public async Task SendDeviceOrderAsync_ShouldSendCorrectPayload()
    {
        // Arrange
        var deviceOrder = CreateValidDeviceOrder();
        _mockHandler.SetResponse(HttpStatusCode.OK, JsonSerializer.Serialize(new DeviceOrderResponse()));

        // Act
        await _apiService.SendDeviceOrderAsync(deviceOrder);

        // Assert
        _mockHandler.LastRequest.Should().NotBeNull();
        _mockHandler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        _mockHandler.LastRequest.RequestUri!.AbsolutePath.Should().Be("/DrExtract");
        _mockHandler.LastRequest.Content.Should().NotBeNull();
        
        var requestContent = await _mockHandler.LastRequest.Content!.ReadAsStringAsync();
        requestContent.Should().NotBeNullOrEmpty();
        
        // Verify the JSON contains expected device order data
        requestContent.Should().Contain("CPAP");
        requestContent.Should().Contain("John Doe");
        requestContent.Should().Contain("Dr. Smith");
    }

    [Fact]
    public async Task SendDeviceOrderAsync_ShouldIncludeCorrectHeaders()
    {
        // Arrange
        var deviceOrder = CreateValidDeviceOrder();
        _mockHandler.SetResponse(HttpStatusCode.OK, JsonSerializer.Serialize(new DeviceOrderResponse()));

        // Act
        await _apiService.SendDeviceOrderAsync(deviceOrder);

        // Assert
        _mockHandler.LastRequest.Should().NotBeNull();
        _mockHandler.LastRequest!.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
        _mockHandler.LastRequest.Content.Headers.ContentType.CharSet.Should().Be("utf-8");
    }

    private static DeviceOrder CreateValidDeviceOrder()
    {
        return new DeviceOrder(
            Device: "CPAP",
            MaskType: "full face",
            AddOns: ["humidifier"],
            Qualifier: "AHI > 20",
            OrderingProvider: "Dr. Smith",
            Liters: null,
            Usage: null,
            Diagnosis: "Sleep Apnea",
            PatientName: "John Doe",
            DateOfBirth: "01/01/1980"
        )
        {
            PatientId = "12345",
            Specifications = new Dictionary<string, object>
            {
                ["MaskType"] = "full face",
                ["Pressure"] = "10 cmH2O",
                ["AddOns"] = new[] { "humidifier" }
            }
        };
    }
}

// Helper class to mock HttpMessageHandler
public class MockHttpMessageHandler : HttpMessageHandler
{
    private HttpResponseMessage? _response;
    private Exception? _exception;
    private Queue<object>? _sequentialResponses;

    public HttpRequestMessage? LastRequest { get; private set; }
    public int RequestCount { get; private set; }

    public void SetResponse(HttpStatusCode statusCode, string content = "")
    {
        _response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };
    }

    public void SetException(Exception exception)
    {
        _exception = exception;
    }

    public void SetSequentialResponses(params object[] responses)
    {
        _sequentialResponses = new Queue<object>(responses);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        RequestCount++;

        // Add a small delay to simulate network call
        await Task.Delay(10, cancellationToken);

        // Handle sequential responses
        if (_sequentialResponses?.Count > 0)
        {
            var response = _sequentialResponses.Dequeue();
            
            if (response is Exception ex)
                throw ex;
            
            if (response is (HttpStatusCode statusCode, string content))
            {
                return new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(content, Encoding.UTF8, "application/json")
                };
            }
        }

        if (_exception != null)
            throw _exception;

        return _response ?? new HttpResponseMessage(HttpStatusCode.OK);
    }
}