using Microsoft.Extensions.Logging;

namespace SampleApp;

class Program
{
    static void Main(string[] args)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
        });

        var userLogger = loggerFactory.CreateLogger<UserService>();
        var userService = new UserService(userLogger);

        var orderLogger = loggerFactory.CreateLogger<OrderService>();
        var orderService = new OrderService(orderLogger);

        // Test user operations
        userService.LoginUser("john_doe", 123);
        userService.ProcessOrder(456, 99.99m);
        userService.DeleteUser(123, "john_doe");
        userService.LogoutUser("john_doe");

        // Test order operations
        orderService.CreateOrder(789);
        orderService.CheckOrderStatus(456, "slow");
    }
}
