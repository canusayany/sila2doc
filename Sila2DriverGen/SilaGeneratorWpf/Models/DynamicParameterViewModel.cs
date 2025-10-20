using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Tecan.Sila2;

namespace SilaGeneratorWpf.Models
{
    /// <summary>
    /// 动态参数视图模型 - 用于显示和编辑命令参数
    /// </summary>
    public partial class DynamicParameterViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _value = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        public string Identifier { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DataTypeType? DataType { get; set; }
        public string TypeDescription { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Constraints { get; set; } = string.Empty;
        public bool IsRequired { get; set; } = true;

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        partial void OnValueChanged(string value)
        {
            ValidateValue();
        }

        /// <summary>
        /// 验证输入值
        /// </summary>
        private void ValidateValue()
        {
            ErrorMessage = string.Empty;

            if (IsRequired && string.IsNullOrWhiteSpace(Value))
            {
                ErrorMessage = "此参数为必填项";
                return;
            }

            // 根据类型进行基本验证
            if (!string.IsNullOrWhiteSpace(Value) && DataType != null)
            {
                var basicType = GetBasicType(DataType);
                switch (basicType)
                {
                    case BasicType.Integer:
                        if (!long.TryParse(Value, out _))
                            ErrorMessage = "请输入有效的整数";
                        break;
                    case BasicType.Real:
                        if (!double.TryParse(Value, out _))
                            ErrorMessage = "请输入有效的数字";
                        break;
                    case BasicType.Boolean:
                        if (!bool.TryParse(Value, out _))
                            ErrorMessage = "请输入 true 或 false";
                        break;
                }
            }
        }

        private BasicType? GetBasicType(DataTypeType dataType)
        {
            if (dataType?.Item is BasicType basicType)
                return basicType;
            if (dataType?.Item is ConstrainedType constrainedType)
                return GetBasicType(constrainedType.DataType);
            return null;
        }

        /// <summary>
        /// 尝试将字符串值转换为实际类型的值
        /// </summary>
        public object? GetTypedValue()
        {
            if (string.IsNullOrWhiteSpace(Value) || DataType == null)
                return null;

            var basicType = GetBasicType(DataType);
            try
            {
                switch (basicType)
                {
                    case BasicType.Integer:
                        return long.Parse(Value);
                    case BasicType.Real:
                        return double.Parse(Value);
                    case BasicType.Boolean:
                        return bool.Parse(Value);
                    case BasicType.String:
                        return Value;
                    default:
                        return Value;
                }
            }
            catch
            {
                return Value;
            }
        }
    }
}
