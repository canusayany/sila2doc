using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SilaGeneratorWpf.Models
{
    /// <summary>
    /// 服务器信息视图模型
    /// </summary>
    public partial class ServerInfoViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isExpanded;

        /// <summary>
        /// 是否有部分子项被选中（三态选择）
        /// false = 未选，null = 半选，true = 全选
        /// 默认为 false（未选择）
        /// </summary>
        [ObservableProperty]
        private bool? _isPartiallySelected = false;

        public string ServerName { get; set; } = string.Empty;
        public string IPAddress { get; set; } = string.Empty;
        public int Port { get; set; }
        public Guid Uuid { get; set; }
        public string ServerType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime LastSeen { get; set; }

        public ObservableCollection<FeatureInfoViewModel> Features { get; set; } = new();

        /// <summary>
        /// ServerData 缓存（用于后续获取特性等操作）
        /// </summary>
        public Tecan.Sila2.ServerData? ServerDataCache { get; set; }

        /// <summary>
        /// 用于回调通知的委托
        /// </summary>
        public Action<ServerInfoViewModel, FeatureInfoViewModel>? OnFeatureSelectionChanged { get; set; }

        partial void OnIsSelectedChanged(bool value)
        {
            // 同步选择所有特性（不触发回调，避免循环）
            foreach (var feature in Features)
            {
                feature.SilentSetSelection(value);
            }
            
            UpdatePartialSelection();
        }

        /// <summary>
        /// 更新父节点的部分选中状态
        /// </summary>
        public void UpdatePartialSelection()
        {
            if (!Features.Any())
            {
                IsPartiallySelected = false;  // 没有子项时默认未选
                return;
            }

            var selectedCount = Features.Count(f => f.IsSelected);
            
            if (selectedCount == 0)
            {
                IsPartiallySelected = false;  // 未选
            }
            else if (selectedCount == Features.Count)
            {
                IsPartiallySelected = true;  // 全选
            }
            else
            {
                IsPartiallySelected = null;  // 半选
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
    }

    /// <summary>
    /// 特性信息视图模型
    /// </summary>
    public partial class FeatureInfoViewModel : ObservableObject
    {
        private bool _suppressNotification = false;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isExpanded;

        public string Identifier { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string FeatureXml { get; set; } = string.Empty;

        public ServerInfoViewModel? ParentServer { get; set; }
        
        /// <summary>
        /// 对应的本地特性节点（如果是本地特性）
        /// </summary>
        public FeatureTreeNodeBase? ParentNode { get; set; }

        public ObservableCollection<CommandInfoViewModel> Commands { get; set; } = new();
        public ObservableCollection<PropertyInfoViewModel> Properties { get; set; } = new();
        public ObservableCollection<MetadataInfoViewModel> Metadata { get; set; } = new();

        public string DisplayText => $"{DisplayName ?? Identifier} (v{Version})";

        partial void OnIsSelectedChanged(bool value)
        {
            if (_suppressNotification)
                return;

            // 通知父节点更新状态
            if (ParentServer != null)
            {
                ParentServer.UpdatePartialSelection();
                ParentServer.OnFeatureSelectionChanged?.Invoke(ParentServer, this);
            }
            
            // 更新本地特性节点的父节点状态
            if (ParentNode != null)
            {
                ParentNode.UpdatePartialSelection();
            }
        }

        /// <summary>
        /// 静默设置选中状态（不触发通知）
        /// </summary>
        public void SilentSetSelection(bool value)
        {
            _suppressNotification = true;
            IsSelected = value;
            _suppressNotification = false;
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
