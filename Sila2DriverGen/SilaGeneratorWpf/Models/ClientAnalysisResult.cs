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
        /// 是否为维护方法（勾选后生成 MethodMaintenance 特性，否则生成 MethodOperations 特性）
        /// </summary>
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private bool _isMaintenance = false;

        /// <summary>
        /// 原始方法信息引用（用于同步）
        /// </summary>
        public MethodGenerationInfo? MethodInfo { get; set; }
    }
}


