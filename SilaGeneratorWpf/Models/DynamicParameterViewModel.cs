using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Tecan.Sila2;

namespace SilaGeneratorWpf.Models
{
    /// <summary>
    /// 动态参数视图模型 - 用于显示和编辑命令参数
    /// </summary>
    public class DynamicParameterViewModel : INotifyPropertyChanged
    {
        private string _value = string.Empty;
        private string _errorMessage = string.Empty;

        public string Identifier { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DataTypeType? DataType { get; set; }
        public string TypeDescription { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Constraints { get; set; } = string.Empty;
        public bool IsRequired { get; set; } = true;

        /// <summary>
        /// 用户输入的值（字符串形式）
        /// </summary>
        public string Value
        {
            get => _value;
            set
            {
                _value = value;
                OnPropertyChanged();
                ValidateValue();
            }
        }

        /// <summary>
        /// 验证错误消息
        /// </summary>
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasError));
            }
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 命令执行上下文视图模型
    /// </summary>
    public class CommandExecutionViewModel : INotifyPropertyChanged
    {
        private bool _isExecuting;
        private bool _canCancel;
        private string _result = string.Empty;
        private string _statusMessage = "就绪";

        public CommandInfoViewModel? Command { get; set; }
        public ObservableCollection<DynamicParameterViewModel> Parameters { get; set; } = new();

        public bool IsExecuting
        {
            get => _isExecuting;
            set
            {
                _isExecuting = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanExecute));
            }
        }

        public bool CanExecute => !IsExecuting;

        public bool CanCancel
        {
            get => _canCancel;
            set
            {
                _canCancel = value;
                OnPropertyChanged();
            }
        }

        public string Result
        {
            get => _result;
            set
            {
                _result = value;
                OnPropertyChanged();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 属性执行上下文视图模型
    /// </summary>
    public class PropertyExecutionViewModel : INotifyPropertyChanged
    {
        private bool _isLoading;
        private bool _isSubscribed;
        private string _value = string.Empty;
        private string _statusMessage = "就绪";

        public PropertyInfoViewModel? Property { get; set; }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanGet));
            }
        }

        public bool CanGet => !IsLoading && !IsSubscribed;

        public bool IsSubscribed
        {
            get => _isSubscribed;
            set
            {
                _isSubscribed = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanGet));
                OnPropertyChanged(nameof(CanUnsubscribe));
            }
        }

        public bool CanUnsubscribe => IsSubscribed;

        public string Value
        {
            get => _value;
            set
            {
                _value = value;
                OnPropertyChanged();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

