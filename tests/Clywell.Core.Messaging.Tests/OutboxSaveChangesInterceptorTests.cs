using System.Reflection;

namespace Clywell.Core.Messaging.Tests;

public sealed class OutboxSaveChangesInterceptorTests
{
    [Theory]
    [InlineData("UserRegisteredIntegrationEvent", "user.registered")]
    [InlineData("OrderCreatedIntegrationEvent", "order.created")]
    [InlineData("PaymentProcessed", "payment.processed")]
    public void DeriveRoutingKey_ConvertsEventTypeNameToDotSeparatedLowercase(string eventTypeName, string expected)
    {
        var method = typeof(OutboxSaveChangesInterceptor)
            .GetMethod("DeriveRoutingKey", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var actual = (string?)method!.Invoke(null, [eventTypeName]);

        Assert.Equal(expected, actual);
    }
}
