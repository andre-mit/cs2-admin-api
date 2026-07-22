using FluentAssertions;
using Moq;
using Cs2Admin.API.Application.Matches.Commands;
using Cs2Admin.API.Infrastructure.Repositories;
using Match = Cs2Admin.API.Models.Match;

namespace Cs2Admin.API.Tests.Handlers;

public class CreateMatchHandlerTests
{
    [Fact]
    public async Task Handle_ShouldCreateMatchAndReturnId()
    {
        // Arrange
        var mockRepo = new Mock<IMatchRepository>();
        
        var matchToCreate = new Match
        {
            Team1Id = 1,
            Team2Id = 2,
            ServerId = 1,
            MaxMaps = 1
        };
        var command = new CreateMatchCommand(matchToCreate);

        Match? createdMatch = null;
        
        mockRepo
            .Setup(r => r.AddAsync(It.IsAny<Match>()))
            .Callback<Match>(m =>
            {
                m.Id = 123;
                createdMatch = m;
            })
            .Returns(Task.CompletedTask);

        var handler = new CreateMatchCommandHandler(mockRepo.Object);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Id.Should().Be(123);
        createdMatch.Should().NotBeNull();
        createdMatch!.Team1Id.Should().Be(1);
        createdMatch.Team2Id.Should().Be(2);
        createdMatch.ServerId.Should().Be(1);
        createdMatch.MaxMaps.Should().Be(1);
        
        mockRepo.Verify(r => r.AddAsync(It.IsAny<Match>()), Times.Once);
        mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }
}
