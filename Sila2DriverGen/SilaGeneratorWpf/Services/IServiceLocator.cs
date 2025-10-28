using System;

namespace SilaGeneratorWpf.Services
{
    /// <summary>
    /// 服务定位器接口
    /// </summary>
    public interface IServiceLocator
    {
        /// <summary>
        /// 获取指定类型的服务
        /// </summary>
        T GetService<T>() where T : class;

        /// <summary>
        /// 获取指定类型的服务
        /// </summary>
        object GetService(Type serviceType);
    }
}

