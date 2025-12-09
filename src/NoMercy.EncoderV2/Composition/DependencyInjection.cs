using Microsoft.Extensions.DependencyInjection;
using NoMercy.NmSystem.Dto;

namespace NoMercy.EncoderV2.Composition;

public static class DependencyInjection
{
    public static IServiceCollection AddFfmpegProcessExecutor(this IServiceCollection services)
    {
        services.AddSingleton<ExecutorRegistry>();
        return services;
    }
}