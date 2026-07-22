using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Mediator;
using Moq;
using Cs2Admin.API.Infrastructure.PipelineBehaviors;

namespace Cs2Admin.API.Tests.Behaviors;

public class TestCommand : ICommand<string>
{
    public string Name { get; set; } = string.Empty;
}

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_WithValidRequest_ShouldCallNext()
    {
        // Arrange
        var validators = new List<IValidator<TestCommand>>();
        var behavior = new ValidationBehavior<TestCommand, string>(validators);
        var request = new TestCommand { Name = "Valid" };
        
        var nextMock = new Mock<MessageHandlerDelegate<TestCommand, string>>();
        nextMock.Setup(n => n(request, It.IsAny<CancellationToken>())).ReturnsAsync("Success");

        // Act
        var result = await behavior.Handle(request, nextMock.Object, CancellationToken.None);

        // Assert
        result.Should().Be("Success");
        nextMock.Verify(n => n(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInvalidRequest_ShouldThrowValidationException()
    {
        // Arrange
        var request = new TestCommand { Name = "Invalid" };
        
        var validatorMock = new Mock<IValidator<TestCommand>>();
        var validationFailures = new List<ValidationFailure>
        {
            new ValidationFailure("Name", "Name is required")
        };
        
        validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestCommand>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(validationFailures));

        var validators = new List<IValidator<TestCommand>> { validatorMock.Object };
        var behavior = new ValidationBehavior<TestCommand, string>(validators);
        
        var nextMock = new Mock<MessageHandlerDelegate<TestCommand, string>>();

        // Act
        var action = async () => await behavior.Handle(request, nextMock.Object, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Name is required*");
            
        nextMock.Verify(n => n(It.IsAny<TestCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
