using Microsoft.Extensions.DependencyInjection;
using SilaGeneratorWpf.ViewModels;

namespace SilaGeneratorWpf.Services
{
    /// <summary>
    /// 服务集合扩展方法
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 注册所有应用服务
        /// </summary>
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // 注册服务（单例）
            services.AddSingleton<IServiceLocator, ServiceLocator>();
            services.AddSingleton<ServerDiscoveryService>();
            services.AddSingleton<LocalFeaturePersistenceService>();
            services.AddSingleton<ConfigurationService>();
            services.AddSingleton<UserPreferencesService>();
            
            // 注册生成器服务（瞬时）
            services.AddTransient<ClientCodeGenerator>();
            services.AddTransient<ClientCodeAnalyzer>();
            services.AddTransient<D3DriverGeneratorService>();
            services.AddTransient<D3DriverOrchestrationService>();
            services.AddTransient<GeneratedCodeDeduplicator>();
            
            return services;
        }

        /// <summary>
        /// 注册所有ViewModel
        /// </summary>
        public static IServiceCollection AddViewModels(this IServiceCollection services)
        {
            // 注册ViewModel（单例，因为WPF通常使用单例ViewModel）
            services.AddSingleton<D3DriverViewModel>();
            
            return services;
        }
    }
}

