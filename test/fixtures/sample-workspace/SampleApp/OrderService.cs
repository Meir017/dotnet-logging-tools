using Microsoft.Extensions.Logging;

namespace SampleApp;

public class OrderService
{
    private readonly ILogger<OrderService> _logger;

    public OrderService(ILogger<OrderService> logger)
    {
        _logger = logger;
    }

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Creating order for customer {customerId}")]
    partial void LogOrderCreation(int customerId);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "Order {orderId} is taking too long")]
    partial void LogSlowOrder(int orderId);

    public void CreateOrder(int customerId)
    {
        LogOrderCreation(customerId);
        _logger.LogDebug("Order creation started");
    }

    public void CheckOrderStatus(int orderId, string status)
    {
        if (status == "slow")
        {
            LogSlowOrder(orderId);
        }
        
        _logger.LogInformation("Order {orderId} status: {status}", orderId, status);
    }
}
