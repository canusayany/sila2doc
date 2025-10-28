using System;
using Microsoft.Extensions.DependencyInjection;

namespace SilaGeneratorWpf.Services
{
    /// <summary>
    /// 服务定位器实现
    /// </summary>
    public class ServiceLocator : IServiceLocator
    {
        private static IServiceProvider? _serviceProvider;

        /// <summary>
        /// 初始化服务提供者
        /// </summary>
        public static void Initialize(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// 获取当前服务提供者
        /// </summary>
        public static IServiceProvider Current
        {
            get
            {
                if (_serviceProvider == null)
                    throw new InvalidOperationException("ServiceLocator未初始化，请先调用Initialize方法");
                return _serviceProvider;
            }
        }

        /// <summary>
        /// 获取指定类型的服务
        /// </summary>
        public T GetService<T>() where T : class
        {
            return Current.GetRequiredService<T>();
        }

        /// <summary>
        /// 获取指定类型的服务
        /// </summary>
        public object GetService(Type serviceType)
        {
            return Current.GetRequiredService(serviceType);
        }
    }
}

