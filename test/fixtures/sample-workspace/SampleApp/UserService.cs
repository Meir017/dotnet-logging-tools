using Microsoft.Extensions.Logging;

namespace SampleApp;

public class UserService
{
    private readonly ILogger<UserService> _logger;

    public UserService(ILogger<UserService> logger)
    {
        _logger = logger;
    }

    public void LoginUser(string username, int userId)
    {
        // Good logging - parameter names match
        _logger.LogInformation("User {username} logged in with ID {userId}", username, userId);
    }

    public void LogoutUser(string username)
    {
        // Missing EventId
        _logger.LogInformation("User {username} logged out", username);
    }

    public void DeleteUser(int userId, string username)
    {
        // Parameter name mismatch - placeholder {userId} but parameter is 'username'
        _logger.LogWarning("Deleting user {userId}", username);
    }

    public void ProcessOrder(int orderId, decimal amount)
    {
        using (_logger.BeginScope("Processing order {orderId}", orderId))
        {
            _logger.LogInformation("Order amount: {amount}", amount);

            // Multiple inconsistencies
            _logger.LogError("Failed to process order {id} for amount {total}", orderId, amount);
        }
    }
}
