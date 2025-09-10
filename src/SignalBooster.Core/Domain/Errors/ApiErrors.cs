using SignalBooster.Core.Common;

namespace SignalBooster.Core.Domain.Errors;

public static class ApiErrors
{
    public static Error ServiceUnavailable(string endpoint) =>
        Error.Failure("Api.ServiceUnavailable", $"The API service at '{endpoint}' is currently unavailable.");

    public static Error Timeout(string endpoint, int timeoutSeconds) =>
        Error.Failure("Api.Timeout", $"Request to '{endpoint}' timed out after {timeoutSeconds} seconds.");

    public static Error Unauthorized(string endpoint) =>
        Error.Failure("Api.Unauthorized", $"Unauthorized access to '{endpoint}'. Check your API credentials.");

    public static Error BadRequest(string endpoint, string responseContent) =>
        Error.Validation("Api.BadRequest", $"Bad request to '{endpoint}': {responseContent}");

    public static Error UnexpectedResponse(string endpoint, int statusCode, string responseContent) =>
        Error.Failure("Api.UnexpectedResponse", 
            $"Unexpected response from '{endpoint}' (Status: {statusCode}): {responseContent}");

    public static Error NetworkError(string endpoint, string errorMessage) =>
        Error.Failure("Api.NetworkError", $"Network error when calling '{endpoint}': {errorMessage}");

    public static Error SerializationError(string errorMessage) =>
        Error.Failure("Api.SerializationError", $"Failed to serialize request data: {errorMessage}");

    public static Error DeserializationError(string endpoint, string errorMessage) =>
        Error.Failure("Api.DeserializationError", $"Failed to deserialize response from '{endpoint}': {errorMessage}");
}