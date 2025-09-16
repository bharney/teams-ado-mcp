using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using McpServer.Models;
using McpServer.Services;
using Moq;
using TeamsBot.Models;
using TeamsBot.Services;
using Xunit;

// Alias to disambiguate between TeamsBot.Services.IAzureDevOpsService and McpServer.Services.IAzureDevOpsService
using ServerAdoService = McpServer.Services.IAzureDevOpsService;

namespace TeamsBot.Tests.Services;

public class WorkItemCreationServiceTests
{
  private readonly Mock<ServerAdoService> _adoMock = new();
  private readonly WorkItemCreationService _sut;

  public WorkItemCreationServiceTests()
  {
    _sut = new WorkItemCreationService(_adoMock.Object);
  }

  [Fact]
  public async Task CreateFromActionItemAsync_NullArgument_Throws()
  {
    await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.CreateFromActionItemAsync(null!));
  }

  [Fact]
  public async Task CreateFromActionItemAsync_EmptyTitle_ReturnsNull()
  {
    var details = new ActionItemDetails { Title = "  " };
    var result = await _sut.CreateFromActionItemAsync(details);
    result.Should().BeNull();
    _adoMock.Verify(a => a.CreateWorkItemAsync(It.IsAny<WorkItemRequest>()), Times.Never);
  }

  [Fact]
  public async Task CreateFromActionItemAsync_Valid_MapsAndCallsServiceOnce()
  {
    var details = new ActionItemDetails
    {
      Title = "Implement caching layer",
      Description = "Add Redis based caching",
      Priority = "High",
      WorkItemType = "Task"
    };

  _adoMock.Setup(a => a.CreateWorkItemAsync(It.IsAny<WorkItemRequest>()))
    .ReturnsAsync(new WorkItemResult { Id = 123, Title = details.Title, WorkItemType = details.WorkItemType!, State = "New" });

    var result = await _sut.CreateFromActionItemAsync(details, CancellationToken.None);

    result.Should().NotBeNull();
    result!.Id.Should().Be(123);
    result.Title.Should().Be(details.Title);

    _adoMock.Verify(a => a.CreateWorkItemAsync(It.Is<WorkItemRequest>(r =>
        r.Title == details.Title &&
        r.Description == details.Description &&
        r.Priority == details.Priority &&
        r.AssignedTo == null &&
        r.WorkItemType == details.WorkItemType
    )), Times.Once);
  }
}
