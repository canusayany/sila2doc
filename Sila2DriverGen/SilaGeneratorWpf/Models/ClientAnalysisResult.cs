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
    public class MethodPreviewData
    {
        public string FeatureName { get; set; } = string.Empty;
        public string MethodName { get; set; } = string.Empty;
        public string MethodType { get; set; } = string.Empty;
        public string ReturnType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}


