using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clywell.Core.Messaging;

/// <summary>
/// Extension methods for wiring messaging interceptors onto a <see cref="DbContextOptionsBuilder"/>.
/// </summary>
public static class DbContextOptionsBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="OutboxSaveChangesInterceptor"/> on the DbContext.
    /// Call this from the service's <c>IDbContextOptionsConfiguration&lt;T&gt;</c> implementation.
    /// </summary>
    public static DbContextOptionsBuilder UseOutboxInterceptor(
        this DbContextOptionsBuilder optionsBuilder,
        IServiceProvider serviceProvider)
    {
        var interceptor = serviceProvider.GetRequiredService<OutboxSaveChangesInterceptor>();
        return optionsBuilder.AddInterceptors(interceptor);
    }
}
