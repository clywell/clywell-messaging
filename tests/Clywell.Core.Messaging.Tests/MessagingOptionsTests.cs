namespace Clywell.Core.Messaging.Tests;

public sealed class MessagingOptionsTests
{
    [Fact]
    public void Defaults_AreConfiguredAsExpected()
    {
        var options = new MessagingOptions();

        Assert.Equal("Messaging", MessagingOptions.SectionName);
        Assert.Equal(string.Empty, options.Exchange);
        Assert.Equal(TimeSpan.FromSeconds(5), options.ProcessingInterval);
        Assert.Equal(50, options.BatchSize);
        Assert.Equal(3, options.MaxRetryAttempts);
    }

    [Fact]
    public void Properties_AreSettable()
    {
        var options = new MessagingOptions
        {
            Exchange = "events",
            ProcessingInterval = TimeSpan.FromSeconds(10),
            BatchSize = 100,
            MaxRetryAttempts = 7,
        };

        Assert.Equal("events", options.Exchange);
        Assert.Equal(TimeSpan.FromSeconds(10), options.ProcessingInterval);
        Assert.Equal(100, options.BatchSize);
        Assert.Equal(7, options.MaxRetryAttempts);
    }
}
