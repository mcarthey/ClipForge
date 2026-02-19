using Microsoft.AspNetCore.SignalR;

namespace ClipForge.Hubs;

public class ProcessingHub : Hub<IProcessingHubClient>
{
    public async Task JoinUserGroup(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
    }

    public async Task LeaveUserGroup(string userId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");
    }
}

public interface IProcessingHubClient
{
    Task JobStatusChanged(int jobId, string status, string? errorMessage = null);
    Task JobCompleted(int jobId, string platform);
    Task BatchCompleted(List<int> jobIds);
}
