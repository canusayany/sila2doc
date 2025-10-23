using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;
using SilaGeneratorWpf.Models;

namespace SilaGeneratorWpf.Behaviors
{
    /// <summary>
    /// 三态树选择逻辑行为类
    /// 实现父节点和子节点之间的三态选择逻辑
    /// </summary>
    public class ThreeStateTreeViewBehavior : Behavior<CheckBox>
    {
        private bool _isUpdating = false;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.PreviewMouseLeftButtonDown += OnPreviewMouseDown;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.PreviewMouseLeftButtonDown -= OnPreviewMouseDown;
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_isUpdating) return;

            var checkBox = sender as CheckBox;
            if (checkBox == null) return;

            // 获取数据上下文
            var dataContext = checkBox.DataContext;

            if (dataContext is ServerInfoViewModel serverNode)
            {
                HandleServerNodeClick(serverNode, checkBox);
                e.Handled = true; // 阻止CheckBox的默认行为
            }
            else if (dataContext is LocalFeatureNodeViewModel localNode)
            {
                HandleLocalNodeClick(localNode, checkBox);
                e.Handled = true; // 阻止CheckBox的默认行为
            }
        }

        /// <summary>
        /// 处理服务器节点点击
        /// </summary>
        private void HandleServerNodeClick(ServerInfoViewModel serverNode, CheckBox checkBox)
        {
            _isUpdating = true;

            try
            {
                var currentState = serverNode.IsPartiallySelected;

                if (currentState == false) // 未选 → 全选
                {
                    serverNode.IsPartiallySelected = true;
                    foreach (var feature in serverNode.Features)
                    {
                        feature.SilentSetSelection(true);
                    }
                }
                else if (currentState == true) // 全选 → 未选
                {
                    serverNode.IsPartiallySelected = false;
                    foreach (var feature in serverNode.Features)
                    {
                        feature.SilentSetSelection(false);
                    }
                }
                else // 半选 → 未选
                {
                    serverNode.IsPartiallySelected = false;
                    foreach (var feature in serverNode.Features)
                    {
                        feature.SilentSetSelection(false);
                    }
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        /// <summary>
        /// 处理本地节点点击
        /// </summary>
        private void HandleLocalNodeClick(LocalFeatureNodeViewModel localNode, CheckBox checkBox)
        {
            _isUpdating = true;

            try
            {
                var currentState = localNode.IsPartiallySelected;

                if (currentState == false) // 未选 → 全选
                {
                    localNode.IsPartiallySelected = true;
                    foreach (var file in localNode.Files)
                    {
                        file.SilentSetSelection(true);
                    }
                }
                else if (currentState == true) // 全选 → 未选
                {
                    localNode.IsPartiallySelected = false;
                    foreach (var file in localNode.Files)
                    {
                        file.SilentSetSelection(false);
                    }
                }
                else // 半选 → 未选
                {
                    localNode.IsPartiallySelected = false;
                    foreach (var file in localNode.Files)
                    {
                        file.SilentSetSelection(false);
                    }
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }
    }
}

