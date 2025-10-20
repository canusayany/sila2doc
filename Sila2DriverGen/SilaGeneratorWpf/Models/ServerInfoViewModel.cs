using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SilaGeneratorWpf.Models
{
    /// <summary>
    /// 服务器信息视图模型
    /// </summary>
    public class ServerInfoViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isExpanded;

        public string ServerName { get; set; } = string.Empty;
        public string IPAddress { get; set; } = string.Empty;
        public int Port { get; set; }
        public Guid Uuid { get; set; }
        public string ServerType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime LastSeen { get; set; }

        public ObservableCollection<FeatureInfoViewModel> Features { get; set; } = new();

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
                // 同步选择所有特性
                foreach (var feature in Features)
                {
                    feature.IsSelected = value;
                }
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                OnPropertyChanged();
            }
        }

        public string DisplayText 
        {
            get
            {
                // 如果有有效的 IP 和端口，显示完整信息
                if (!string.IsNullOrEmpty(IPAddress) && IPAddress != "mDNS" && Port > 0)
                {
                    return $"{ServerName} ({IPAddress}:{Port})";
                }
                // 如果只有 IP 没有端口
                else if (!string.IsNullOrEmpty(IPAddress) && IPAddress != "mDNS" && IPAddress != "Discovered")
                {
                    return $"{ServerName} ({IPAddress})";
                }
                // 否则只显示服务器名称
                else
                {
                    return ServerName;
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 特性信息视图模型
    /// </summary>
    public class FeatureInfoViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isExpanded;

        public string Identifier { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string FeatureXml { get; set; } = string.Empty;

        public ServerInfoViewModel? ParentServer { get; set; }

        public ObservableCollection<CommandInfoViewModel> Commands { get; set; } = new();
        public ObservableCollection<PropertyInfoViewModel> Properties { get; set; } = new();
        public ObservableCollection<MetadataInfoViewModel> Metadata { get; set; } = new();

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                OnPropertyChanged();
            }
        }

        public string DisplayText => $"{DisplayName ?? Identifier} (v{Version})";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 命令信息视图模型
    /// </summary>
    public class CommandInfoViewModel
    {
        public string Identifier { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Observable { get; set; }
        public string DisplayText => $"📝 {DisplayName ?? Identifier} {(Observable ? "(Observable)" : "")}";
    }

    /// <summary>
    /// 属性信息视图模型
    /// </summary>
    public class PropertyInfoViewModel
    {
        public string Identifier { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Observable { get; set; }
        public string DisplayText => $"🔧 {DisplayName ?? Identifier} {(Observable ? "(Observable)" : "")}";
    }

    /// <summary>
    /// 元数据信息视图模型
    /// </summary>
    public class MetadataInfoViewModel
    {
        public string Identifier { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DisplayText => $"ℹ️ {DisplayName ?? Identifier}";
    }
}

