using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace ava_demo_new.Views
{
    public partial class SettingPage : UserControl
    {
        // 定义事件，用于通知设置保存
        public event EventHandler<SettingsSavedEventArgs>? SettingsSaved;
        // 添加 OriginalContent 属性
        public Control? OriginalContent { get; set; }
        
        public SettingPage()
        {
            InitializeComponent();
        }

        private void SaveButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                // 获取设置的值
                double platformWidth = double.Parse(PlatformWidth?.Text ?? "400");
                double platformHeight = double.Parse(PlatformHeight?.Text ?? "300");
                double blockWidth = double.Parse(BlockWidth?.Text ?? "60");
                double blockHeight = double.Parse(BlockHeight?.Text ?? "60");

                // 验证输入
                if (platformWidth <= 0 || platformHeight <= 0 || blockWidth <= 0 || blockHeight <= 0)
                {
                    // 可以在这里添加错误提示
                    Console.WriteLine("错误：所有尺寸必须大于0");
                    return;
                }

                // 触发设置保存事件
                SettingsSaved?.Invoke(this, new SettingsSavedEventArgs
                {
                    PlatformWidth = platformWidth,
                    PlatformHeight = platformHeight,
                    BlockWidth = blockWidth,
                    BlockHeight = blockHeight
                });

                // 跳转到OperationPage
                NavigateToOperationPage(platformWidth, platformHeight, blockWidth, blockHeight);
            }
            catch (FormatException)
            {
                Console.WriteLine("错误：请输入有效的数字");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存设置时出错: {ex.Message}");
            }
        }

        private void BackButton_Click(object? sender, RoutedEventArgs e)
        {
            // 返回主页
            if (this.Parent is ContentControl contentControl)
            {
                // 这里需要根据你的主页实现来调整
                // contentControl.Content = new MainPage();
            }
        }

        private void NavigateToOperationPage(double platformWidth, double platformHeight, double blockWidth, double blockHeight)
        {
            if (this.Parent is ContentControl contentControl)
            {
                var operationPage = new OperationPage();
                
                // 传递设置数据到OperationPage
                operationPage.SetPlatformSize(platformWidth, platformHeight);
                operationPage.SetBlockSize(blockWidth, blockHeight);
                
                contentControl.Content = operationPage;
            }
        }
    }

    // 设置保存事件参数
    public class SettingsSavedEventArgs : EventArgs
    {
        public double PlatformWidth { get; set; }
        public double PlatformHeight { get; set; }
        public double BlockWidth { get; set; }
        public double BlockHeight { get; set; }
    }
}