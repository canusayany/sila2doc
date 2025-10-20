using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using SilaGeneratorWpf.ViewModels;

namespace SilaGeneratorWpf.Views
{
    /// <summary>
    /// FileGenerationView.xaml 的交互逻辑
    /// </summary>
    public partial class FileGenerationView : UserControl
    {
        public FileGenerationView()
        {
            InitializeComponent();
        }

        private void DropZone_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                
                if (sender is Border border)
                {
                    border.BorderBrush = new SolidColorBrush(Color.FromRgb(46, 204, 113));
                    border.Background = new SolidColorBrush(Color.FromRgb(232, 248, 245));
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void DropZone_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is Border border)
            {
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(52, 152, 219));
                border.Background = new SolidColorBrush(Color.FromRgb(236, 240, 241));
            }
        }

        private void DropZone_Drop(object sender, DragEventArgs e)
        {
            if (sender is Border border)
            {
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(52, 152, 219));
                border.Background = new SolidColorBrush(Color.FromRgb(236, 240, 241));
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (DataContext is FileGenerationViewModel vm)
                {
                    vm.AddFilesCommand.Execute(files);
                }
            }
        }

        private void SelectFiles_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "选择 Feature 文件",
                Filter = "SiLA Feature Files (*.sila.xml)|*.sila.xml|XML Files (*.xml)|*.xml|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                if (DataContext is FileGenerationViewModel vm)
                {
                    vm.AddFilesCommand.Execute(openFileDialog.FileNames);
                }
            }
        }
    }
}

