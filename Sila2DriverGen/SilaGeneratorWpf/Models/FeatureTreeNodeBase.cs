using CommunityToolkit.Mvvm.ComponentModel;

namespace SilaGeneratorWpf.Models
{
    /// <summary>
    /// 特性树节点基类（用于统一在线服务器和本地节点）
    /// </summary>
    public abstract partial class FeatureTreeNodeBase : ObservableObject
    {
        /// <summary>
        /// 显示文本
        /// </summary>
        [ObservableProperty]
        private string _displayText = string.Empty;

        /// <summary>
        /// 是否选中
        /// </summary>
        [ObservableProperty]
        private bool _isSelected = false;

        /// <summary>
        /// 是否展开
        /// </summary>
        [ObservableProperty]
        private bool _isExpanded = false;

        /// <summary>
        /// 是否有部分子项被选中（半选中状态）
        /// </summary>
        [ObservableProperty]
        private bool? _isPartiallySelected;

        /// <summary>
        /// 节点类型（用于标识）
        /// </summary>
        public abstract string NodeType { get; }

        /// <summary>
        /// 更新父节点的部分选中状态（由子类实现）
        /// </summary>
        public virtual void UpdatePartialSelection()
        {
            // 默认不做任何操作，由子类根据需要覆盖
        }
    }

    /// <summary>
    /// 在线服务器节点类型
    /// </summary>
    public enum TreeNodeType
    {
        /// <summary>
        /// 在线服务器
        /// </summary>
        OnlineServer,

        /// <summary>
        /// 在线服务器的特性
        /// </summary>
        OnlineFeature,

        /// <summary>
        /// 本地节点（文件夹）
        /// </summary>
        LocalNode,

        /// <summary>
        /// 本地特性文件
        /// </summary>
        LocalFeature
    }
}


