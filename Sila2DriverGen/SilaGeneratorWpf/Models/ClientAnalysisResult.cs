using System;
using System.Collections.Generic;
using System.Linq;

namespace SilaGeneratorWpf.Models
{
    /// <summary>
    /// 客户端代码分析结果
    /// </summary>
    public class ClientAnalysisResult
    {
        /// <summary>
        /// 检测到的特性列表
        /// </summary>
        public List<ClientFeatureInfo> Features { get; set; } = new();

        /// <summary>
        /// 获取方法预览数据（用于 DataGrid 显示）
        /// </summary>
        public List<MethodPreviewData> GetMethodPreviewData()
        {
            var previewList = new List<MethodPreviewData>();

            foreach (var feature in Features)
            {
                foreach (var method in feature.Methods)
                {
                    // 过滤掉包含 Stream 或 Stream? 的方法
                    if (HasStreamType(method))
                    {
                        continue;
                    }

                    previewList.Add(new MethodPreviewData
                    {
                        FeatureName = feature.FeatureName,
                        MethodName = method.Name,
                        MethodType = GetMethodTypeDisplay(method),
                        ReturnType = GetReturnTypeDisplay(method.ReturnType),
                        Description = method.Description
                    });
                }
            }

            return previewList;
        }

        /// <summary>
        /// 检查方法是否包含 Stream 类型（参数或返回值）
        /// </summary>
        private bool HasStreamType(MethodGenerationInfo method)
        {
            // 检查返回值是否为 Stream 或 Stream?
            if (IsStreamType(method.ReturnType))
            {
                return true;
            }

            // 检查参数是否包含 Stream 或 Stream?
            foreach (var param in method.Parameters)
            {
                if (IsStreamType(param.Type))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 检查类型是否为 Stream 或 Stream?
        /// </summary>
        private bool IsStreamType(System.Type type)
        {
            if (type == null)
                return false;

            // 检查是否为 Stream 类型
            if (type.Name == "Stream" || type.FullName == "System.IO.Stream")
                return true;

            // 检查是否为 Nullable<Stream> (Stream?)
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var underlyingType = Nullable.GetUnderlyingType(type);
                if (underlyingType != null && (underlyingType.Name == "Stream" || underlyingType.FullName == "System.IO.Stream"))
                    return true;
            }

            return false;
        }

        private string GetMethodTypeDisplay(MethodGenerationInfo method)
        {
            if (method.IsProperty)
                return "属性";
            if (method.IsObservableCommand)
                return "可观察命令";
            return "方法";
        }

        private string GetReturnTypeDisplay(System.Type type)
        {
            if (type == typeof(void))
                return "void";
            
            if (type.IsGenericType)
            {
                var genericType = type.GetGenericTypeDefinition();
                var genericArgs = type.GetGenericArguments();
                var genericArgNames = string.Join(", ", genericArgs.Select(t => t.Name));
                return $"{genericType.Name.Split('`')[0]}<{genericArgNames}>";
            }

            return type.Name;
        }
    }

    /// <summary>
    /// 方法预览数据（用于 DataGrid）
    /// </summary>
    public partial class MethodPreviewData : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private string _featureName = string.Empty;

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private string _methodName = string.Empty;

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private string _methodType = string.Empty;

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private string _returnType = string.Empty;

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private string _description = string.Empty;

        /// <summary>
        /// 是否包含在D3Driver中（只有勾选此项的方法才会被生成）
        /// </summary>
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private bool _isIncluded = true;

        /// <summary>
        /// 是否为调度方法（生成 [MethodOperations] 特性）
        /// </summary>
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private bool _isOperations = false;

        /// <summary>
        /// 是否为维护方法（生成 [MethodMaintenance] 特性）
        /// </summary>
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private bool _isMaintenance = false;

        /// <summary>
        /// 原始方法信息引用（用于同步）
        /// </summary>
        public MethodGenerationInfo? MethodInfo { get; set; }
    }
}


