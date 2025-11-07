using Avalonia.Controls;
using Avalonia.Media;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ava_demo_new.Models;
using ava_demo_new.Services;
using Avalonia;
using Avalonia.Layout;
using System.Net.Http;

namespace ava_demo_new.Views
{
    public partial class TreePage : UserControl
    {
        private readonly IDeviceService _deviceService;
        private ObservableCollection<Device>? _devices; // 改为可为 null

        public TreePage()
        {
            InitializeComponent();

            // 使用 HttpDeviceService（里面包含模拟数据）
            // 创建 HttpClient 实例
            var httpClient = new HttpClient();
            _deviceService = new HttpDeviceService(httpClient);
            
            LoadDeviceDataAsync();
        }

        private async Task LoadDeviceDataAsync() // 改为返回 Task
        {
            try
            {
                // 显示加载状态
                ShowLoadingState();

                // 调用 API 服务获取数据 - 现在使用 HttpDeviceService
                _devices = await _deviceService.GetDevicesAsync();

                // 更新 UI
                UpdateTreeView();

                // 隐藏加载状态
                HideLoadingState();
            }
            catch (System.Exception ex)
            {
                // 处理错误
                ShowErrorState($"加载数据失败: {ex.Message}");
            }
        }

        private void UpdateTreeView()
        {
            DeviceTreeView.Items.Clear();

            if (_devices == null) return; // 添加 null 检查

            foreach (var deviceGroup in _devices)
            {
                var groupNode = new TreeViewItem
                {
                    Header = CreateHeaderContent($"{deviceGroup.Name} ({deviceGroup.Children.Count})", "#007BFF"),
                    Tag = deviceGroup
                };

                AddDeviceNodes(groupNode, deviceGroup.Children);
                DeviceTreeView.Items.Add(groupNode);
            }
        }

        private void AddDeviceNodes(TreeViewItem parentNode, ObservableCollection<Device> devices)
        {
            foreach (var device in devices)
            {
                string statusColor = device.Status switch
                {
                    "在线" => "#28A745",
                    "离线" => "#6C757D",
                    "维护中" => "#FFC107",
                    _ => "#6C757D"
                };

                var node = new TreeViewItem
                {
                    Header = CreateHeaderContent($"{device.Name} - {device.Status}", statusColor),
                    Tag = device
                };

                if (device.Children.Count > 0)
                {
                    AddDeviceNodes(node, device.Children);
                }

                parentNode.Items.Add(node);
            }
        }

        // 加载状态相关方法
        private void ShowLoadingState()
        {
            // 这里可以显示加载动画或禁用按钮
            // 例如：显示加载中文字
            var loadingText = new TextBlock
            {
                Text = "加载中...",
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            // 如果需要在 TreeView 区域显示加载状态，可以在这里添加
        }

        private void HideLoadingState()
        {
            // 隐藏加载状态
            // 例如：移除加载中文字
        }

        private void ShowErrorState(string message)
        {
            // 显示错误信息
            // 例如：在界面某个位置显示错误信息
            var errorText = new TextBlock
            {
                Text = message,
                FontSize = 14,
                Foreground = new SolidColorBrush(Colors.Red),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            // 可以添加到界面或使用对话框显示
        }

        private StackPanel CreateHeaderContent(string text, string color)
        {
            return new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new Border
                    {
                        Width = 12,
                        Height = 12,
                        Background = new SolidColorBrush(Color.Parse(color)),
                        CornerRadius = new CornerRadius(6)
                    },
                    new TextBlock
                    {
                        Text = text,
                        FontSize = 14,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            };
        }

        private async void RefreshButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await LoadDeviceDataAsync(); // 现在可以正常 await
        }

        private void ExpandAllButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ExpandAllTreeNodes();
        }

        private void CollapseAllButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            CollapseAllTreeNodes();
        }

        private void ExpandAllTreeNodes()
        {
            if (DeviceTreeView.Items == null) return;

            foreach (var item in DeviceTreeView.Items)
            {
                if (item is TreeViewItem treeViewItem)
                {
                    ExpandTreeViewItem(treeViewItem);
                }
            }
        }

        private void CollapseAllTreeNodes()
        {
            if (DeviceTreeView.Items == null) return;

            foreach (var item in DeviceTreeView.Items)
            {
                if (item is TreeViewItem treeViewItem)
                {
                    CollapseTreeViewItem(treeViewItem);
                }
            }
        }

        private void ExpandTreeViewItem(TreeViewItem item)
        {
            if (item == null) return;

            item.IsExpanded = true;
            
            if (item.Items == null) return;

            foreach (var childItem in item.Items)
            {
                if (childItem is TreeViewItem treeViewChild)
                {
                    ExpandTreeViewItem(treeViewChild);
                }
            }
        }

        private void CollapseTreeViewItem(TreeViewItem item)
        {
            if (item == null) return;

            item.IsExpanded = false;
            
            if (item.Items == null) return;

            foreach (var childItem in item.Items)
            {
                if (childItem is TreeViewItem treeViewChild)
                {
                    CollapseTreeViewItem(treeViewChild);
                }
            }
        }

        public Control? OriginalContent { get; set; }

        private void BackButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (this.Parent is ContentControl contentControl && OriginalContent != null)
            {
                contentControl.Content = OriginalContent;
            }
        }

        // 新增：设备点击事件处理（可选）
        private void DeviceTreeView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DeviceTreeView.SelectedItem is TreeViewItem selectedItem && selectedItem.Tag is Device device)
            {
                // 可以在这里处理设备点击事件
                // 例如：显示设备详情、更新状态等
                System.Console.WriteLine($"选中设备: {device.Name}, ID: {device.Id}, 状态: {device.Status}");
                
                // 可以调用服务方法
                // await _deviceService.GetDeviceByIdAsync(device.Id);
            }
        }
    }
}