using Microsoft.AspNetCore.SignalR;
using SystemMonitor.Shared.Models;

namespace SystemMonitor.Api.Hubs;

public class MonitoringHub : Hub
{
    private readonly ILogger<MonitoringHub> _logger;

    public MonitoringHub(ILogger<MonitoringHub> logger)
        => _logger = logger;

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Dashboard client connected: {ConnectionId}", Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, "all");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Dashboard client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SubscribeToAgent(string agentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"agent:{agentId}");
        _logger.LogDebug("Client {ConnectionId} subscribed to agent {AgentId}",
            Context.ConnectionId, agentId);
    }

    public async Task UnsubscribeFromAgent(string agentId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"agent:{agentId}");
}

public interface IMonitoringClient
{
    Task MetricBatchReceived(object payload);
    Task LogBatchReceived(object payload);
    Task AnomalyDetected(AnomalyResult anomaly);
    Task HealthScoreUpdated(string agentId, double score);
    Task InfrastructureStatusUpdated(object status);
}
