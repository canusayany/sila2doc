using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SilaGeneratorWpf.Models
{
    /// <summary>
    /// 本地特性节点（以父文件夹为节点）
    /// </summary>
    public partial class LocalFeatureNodeViewModel : ObservableObject
    {
        /// <summary>
        /// 节点名称（父文件夹名称）
        /// </summary>
        [ObservableProperty]
        private string _nodeName = string.Empty;

        /// <summary>
        /// 节点路径（完整路径）
        /// </summary>
        [ObservableProperty]
        private string _nodePath = string.Empty;

        /// <summary>
        /// 特性文件列表
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<LocalFeatureFileViewModel> _files = new();

        /// <summary>
        /// 节点是否展开
        /// </summary>
        [ObservableProperty]
        private bool _isExpanded = true;

        /// <summary>
        /// 节点是否选中（用于删除）
        /// </summary>
        [ObservableProperty]
        private bool _isSelected = false;

        /// <summary>
        /// 是否有部分子项被选中（三态选择）
        /// false = 未选，null = 半选，true = 全选
        /// 默认为 false（未选择）
        /// </summary>
        [ObservableProperty]
        private bool? _isPartiallySelected = false;

        partial void OnIsSelectedChanged(bool value)
        {
            // 同步选择所有特性文件
            foreach (var file in Files)
            {
                file.SilentSetSelection(value);
            }
            
            UpdatePartialSelection();
        }

        /// <summary>
        /// 更新父节点的部分选中状态
        /// </summary>
        public void UpdatePartialSelection()
        {
            if (!Files.Any())
            {
                IsPartiallySelected = false;  // 没有子项时默认未选
                return;
            }

            var selectedCount = Files.Count(f => f.IsSelected);
            
            if (selectedCount == 0)
            {
                IsPartiallySelected = false;  // 未选
            }
            else if (selectedCount == Files.Count)
            {
                IsPartiallySelected = true;  // 全选
            }
            else
            {
                IsPartiallySelected = null;  // 半选
            }
        }
    }

    /// <summary>
    /// 本地特性文件
    /// </summary>
    public partial class LocalFeatureFileViewModel : ObservableObject
    {
        private bool _suppressNotification = false;

        /// <summary>
        /// 文件名
        /// </summary>
        [ObservableProperty]
        private string _fileName = string.Empty;

        /// <summary>
        /// 文件完整路径
        /// </summary>
        [ObservableProperty]
        private string _filePath = string.Empty;

        /// <summary>
        /// 特性标识符（从XML中解析）
        /// </summary>
        [ObservableProperty]
        private string _identifier = string.Empty;

        /// <summary>
        /// 特性显示名称
        /// </summary>
        [ObservableProperty]
        private string _displayName = string.Empty;

        /// <summary>
        /// 是否选中（用于生成）
        /// </summary>
        [ObservableProperty]
        private bool _isSelected = false;

        /// <summary>
        /// 是否展开（用于TreeView，但文件节点通常不需要展开）
        /// </summary>
        [ObservableProperty]
        private bool _isExpanded = false;

        /// <summary>
        /// 父节点引用
        /// </summary>
        public LocalFeatureNodeViewModel? ParentNode { get; set; }

        partial void OnIsSelectedChanged(bool value)
        {
            if (_suppressNotification)
                return;

            // 通知父节点更新状态
            ParentNode?.UpdatePartialSelection();
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
}


