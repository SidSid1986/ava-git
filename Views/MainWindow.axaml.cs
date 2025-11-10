using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using AvaloniaEdit;
using Avalonia.Input;
using Avalonia.Media;
namespace ava_demo_new.Views;

public partial class MainWindow : Window

{
    
    
    // 保存 MainContent 的原始内容
    private Control? _originalContent;
    public MainWindow()
    {
        InitializeComponent();
        // 保存 MainContent 的初始内容
        // 修复：添加 null 检查
        if (MainContent.Content is Control content)
        {
            _originalContent = content;
        }
    }
    
    private void homeClick(object? sender, RoutedEventArgs e)
    
    {
        Console.WriteLine("homeClick");
        if (_originalContent != null)
        {
            MainContent.Content = _originalContent;
        }
        
    }
    
    private void treeClick(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine("treeClick - 跳转到树形页面");
    
        // 创建树形页面
        var treePage = new TreePage();
    
        // 告诉树形页面原始内容，用于返回
        treePage.OriginalContent = _originalContent;
    
        // 显示树形页面
        MainContent.Content = treePage;
    }
    
    private void codeClick(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine("codeClick - 跳转到树形页面");
    
        // 创建code页面
        var codeEdit = new CodeEdit();
    
        // 告诉code页面原始内容，用于返回
        codeEdit.OriginalContent = _originalContent;
    
        // 显示code页面
        MainContent.Content = codeEdit;
    }
    
    // 添加 pcClick 事件处理方法
    private bool isClicked = false;  // 状态变量
    private void PcClick(object? sender, RoutedEventArgs e)
    {
      
        if (isClicked)
        {
            // 恢复原来文字
            ButtonText.Text = "按钮";
        }
        else
        {
            // 改为已点击
            ButtonText.Text = "已点击";
        }
    
        isClicked = !isClicked;  // 切换状态
    }
    
      // 添加跳转按钮点击事件处理方法
    private async void ShowCustomDialog(object sender, RoutedEventArgs e)
    {
        // 创建自定义弹窗
        var dialog = new Window
        {
            Title = "确认跳转",
            Width = 300,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            SizeToContent = SizeToContent.Manual
        };

        var stackPanel = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 20
        };

        // 提示文字
        var textBlock = new TextBlock
        {
            Text = "确认要跳转到操作页吗？",
            FontSize = 14,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        // 按钮容器
        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Spacing = 15
        };

        // 确认按钮
        var confirmButton = new Button
        {
            Content = "确认",
            Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
            Foreground = Brushes.White,
            Padding = new Thickness(15, 5),
            Width = 80
        };

        // 取消按钮
        var cancelButton = new Button
        {
            Content = "取消",
            Background = new SolidColorBrush(Color.FromRgb(108, 117, 125)),
            Foreground = Brushes.White,
            Padding = new Thickness(15, 5),
            Width = 80
        };

        confirmButton.Click += (s, args) =>
        {
            Console.WriteLine("跳转到操作页");
            var operationPage = new OperationPage();           // 创建操作页
            operationPage.OriginalContent = _originalContent;  // 告诉操作页这是要返回的内容"
            MainContent.Content = operationPage;               // 显示操作页
            dialog.Close();
            // 这里添加跳转逻辑
        };

        cancelButton.Click += (s, args) =>
        {
            Console.WriteLine("取消跳转");
            dialog.Close();
        };

        buttonPanel.Children.Add(confirmButton);
        buttonPanel.Children.Add(cancelButton);

        stackPanel.Children.Add(textBlock);
        stackPanel.Children.Add(buttonPanel);

        dialog.Content = stackPanel;

        // 显示弹窗
        await dialog.ShowDialog(this);
    }
    
    

  
}

 
