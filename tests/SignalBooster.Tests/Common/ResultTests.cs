using FluentAssertions;
using SignalBooster.Core.Common;

namespace SignalBooster.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Result_CreateSuccessWithValue_ShouldBeSuccess()
    {
        // Arrange
        var value = "test value";

        // Act
        var result = new Result<string>(value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsError.Should().BeFalse();
        result.Value.Should().Be(value);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Result_CreateWithError_ShouldBeError()
    {
        // Arrange
        var error = Error.Failure("Test.Error", "Test error description");

        // Act
        var result = new Result<string>(error);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(error);
        result.Errors.Should().ContainSingle().Which.Should().Be(error);
    }

    [Fact]
    public void Result_CreateWithMultipleErrors_ShouldContainAllErrors()
    {
        // Arrange
        var errors = new List<Error>
        {
            Error.Validation("Test.Validation", "Validation error"),
            Error.NotFound("Test.NotFound", "Not found error"),
            Error.Failure("Test.Failure", "Failure error")
        };

        // Act
        var result = new Result<string>(errors);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsError.Should().BeTrue();
        result.Errors.Should().HaveCount(3);
        result.Errors.Should().BeEquivalentTo(errors);
        result.FirstError.Should().Be(errors[0]);
    }

    [Fact]
    public void Result_AccessValueWhenError_ShouldThrow()
    {
        // Arrange
        var error = Error.Failure("Test.Error", "Test error");
        var result = new Result<string>(error);

        // Act & Assert
        result.Invoking(r => r.Value)
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot be accessed when Errors property is not empty*");
    }

    [Fact]
    public void Result_AccessFirstErrorWhenSuccess_ShouldThrow()
    {
        // Arrange
        var result = new Result<string>("success value");

        // Act & Assert
        result.Invoking(r => r.FirstError)
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot be accessed when Errors property is empty*");
    }

    [Fact]
    public void Result_ImplicitConversionFromValue_ShouldWork()
    {
        // Arrange & Act
        Result<int> result = 42;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Result_ImplicitConversionFromError_ShouldWork()
    {
        // Arrange
        var error = Error.Validation("Test.Error", "Test error");

        // Act
        Result<int> result = error;

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(error);
    }

    [Fact]
    public void Result_ImplicitConversionFromErrorList_ShouldWork()
    {
        // Arrange
        var errors = new List<Error>
        {
            Error.Validation("Test.Error1", "First error"),
            Error.Validation("Test.Error2", "Second error")
        };

        // Act
        Result<int> result = errors;

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().BeEquivalentTo(errors);
    }

    [Fact]
    public void Result_CreateWithNullValue_ShouldThrow()
    {
        // Act & Assert
        Action act = () => new Result<string>((string)null!);
        
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("value");
    }

    [Fact]
    public void Result_CreateWithNullErrorList_ShouldThrow()
    {
        // Act & Assert
        Action act = () => new Result<string>((List<Error>)null!);
        
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Result_CreateWithEmptyErrorList_ShouldThrow()
    {
        // Arrange
        var emptyErrors = new List<Error>();

        // Act & Assert
        Action act = () => new Result<string>(emptyErrors);
        
        act.Should().Throw<ArgumentException>()
            .WithMessage("*empty collection of errors*");
    }

    [Theory]
    [InlineData(ErrorType.Validation)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Conflict)]
    [InlineData(ErrorType.Failure)]
    [InlineData(ErrorType.Unexpected)]
    public void Error_CreateWithDifferentTypes_ShouldSetCorrectType(ErrorType errorType)
    {
        // Arrange & Act
        var error = new Error("Test.Code", "Test description", errorType);

        // Assert
        error.Type.Should().Be(errorType);
        error.Code.Should().Be("Test.Code");
        error.Description.Should().Be("Test description");
    }

    [Fact]
    public void Error_CreateWithStaticMethods_ShouldSetCorrectTypes()
    {
        // Act
        var validation = Error.Validation("Val.Code", "Validation error");
        var notFound = Error.NotFound("NF.Code", "Not found error");
        var conflict = Error.Conflict("Con.Code", "Conflict error");
        var failure = Error.Failure("Fail.Code", "Failure error");
        var unexpected = Error.Unexpected("Unex.Code", "Unexpected error");

        // Assert
        validation.Type.Should().Be(ErrorType.Validation);
        notFound.Type.Should().Be(ErrorType.NotFound);
        conflict.Type.Should().Be(ErrorType.Conflict);
        failure.Type.Should().Be(ErrorType.Failure);
        unexpected.Type.Should().Be(ErrorType.Unexpected);
    }

    [Fact]
    public void Error_None_ShouldHaveEmptyCodeAndDescription()
    {
        // Act
        var noneError = Error.None;

        // Assert
        noneError.Code.Should().BeEmpty();
        noneError.Description.Should().BeEmpty();
        noneError.Type.Should().Be(ErrorType.Failure);
    }

    [Fact]
    public void Success_StaticProperty_ShouldReturnDefaultSuccess()
    {
        // Act
        var success = Result.Success;

        // Assert
        success.Should().Be(default(Success));
    }

    [Fact]
    public void Result_CreateSuccessWithStaticMethod_ShouldWork()
    {
        // Arrange
        var value = "test value";

        // Act
        var result = Result.CreateSuccess(value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(value);
    }

    [Fact]
    public void Result_CreateFailureWithStaticMethod_ShouldWork()
    {
        // Arrange
        var error = Error.Failure("Test.Error", "Test error");

        // Act
        var result = Result.CreateFailure<string>(error);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(error);
    }

    [Fact]
    public void Result_CreateFailureWithErrorListUsingStaticMethod_ShouldWork()
    {
        // Arrange
        var errors = new List<Error>
        {
            Error.Validation("Val.Error", "Validation error"),
            Error.NotFound("NF.Error", "Not found error")
        };

        // Act
        var result = Result.CreateFailure<string>(errors);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().BeEquivalentTo(errors);
    }

    [Fact]
    public void Result_CompareResultsWithSameValue_ShouldBeEqual()
    {
        // Arrange
        var result1 = new Result<string>("test");
        var result2 = new Result<string>("test");

        // Act & Assert
        result1.IsSuccess.Should().Be(result2.IsSuccess);
        result1.Value.Should().Be(result2.Value);
        result1.Errors.Should().BeEquivalentTo(result2.Errors);
    }

    [Fact]
    public void Result_CompareResultsWithSameError_ShouldBeEqual()
    {
        // Arrange
        var error = Error.Failure("Test.Error", "Test error");
        var result1 = new Result<string>(error);
        var result2 = new Result<string>(error);

        // Act & Assert
        result1.IsSuccess.Should().Be(result2.IsSuccess);
        result1.IsError.Should().Be(result2.IsError);
        result1.FirstError.Should().Be(error);
        result2.FirstError.Should().Be(error);
        result1.Errors.Should().BeEquivalentTo(result2.Errors);
    }

    [Fact]
    public void Result_CompareResultsWithDifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        var result1 = new Result<string>("test1");
        var result2 = new Result<string>("test2");

        // Act & Assert
        result1.Value.Should().NotBe(result2.Value);
        result1.Value.Should().Be("test1");
        result2.Value.Should().Be("test2");
    }

    [Fact]
    public void Result_WithComplexObject_ShouldWork()
    {
        // Arrange
        var complexObject = new
        {
            Id = 123,
            Name = "Test Object",
            Items = new[] { "Item1", "Item2", "Item3" }
        };

        // Act
        var result = new Result<object>(complexObject);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(complexObject);
    }
}