using Avalonia;
using System;
using System.Threading.Tasks;
using ava_demo_new.ViewModels;
using ava_demo_new.Views;
using Avalonia.Controls.ApplicationLifetimes;

namespace ava_demo_new
{
    class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace()
                .AfterSetup(_ =>
                {
                    // 在应用设置完成后显示启动画面
                    ShowSplashAndInitialize();
                });
        
        private static async void ShowSplashAndInitialize()
        {
            var splash = new SplashScreen();
            splash.Show();
            
            try
            {
                splash.Progress?.Report("正在加载配置...");
                await Task.Delay(1000);
                
                splash.Progress?.Report("正在初始化服务...");
                await Task.Delay(1000);
                
                splash.Progress?.Report("正在启动主界面...");
                await Task.Delay(500);
                
                // 关键修改：通过 Application.Current 获取应用实例
                var app = Application.Current;
                if (app?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    // 先关闭可能已经创建的默认主窗口
                    if (desktop.MainWindow != null && desktop.MainWindow.IsVisible)
                    {
                        desktop.MainWindow.Hide();
                    }
                    
                    // 创建新的主窗口并设置为应用主窗口
                    var mainWindow = new MainWindow();
                    
                    // 设置 DataContext
                    mainWindow.DataContext = new MainWindowViewModel();
                    

// 确保主窗口完全加载后再关闭启动画面
                    mainWindow.Loaded += (s, e) => 
                    {
                        // 小延迟确保主窗口完全渲染
                        Task.Delay(100).ContinueWith(_ => 
                        {
                            Avalonia.Threading.Dispatcher.UIThread.Post(() => splash.Close());
                        });
                    };

                    desktop.MainWindow = mainWindow;
                    mainWindow.Show();
                }
                else
                {
                    // 备用方案
                    var mainWindow = new MainWindow();
                    mainWindow.Show();
                }
                
                // 关闭启动画面
                splash.Close();
            }
            catch (Exception ex)
            {
                splash.Progress?.Report($"启动失败: {ex.Message}");
                await Task.Delay(2000);
                splash.Close();
            }
        }
    }
}