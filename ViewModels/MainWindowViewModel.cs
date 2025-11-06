// ViewModels/MainWindowViewModel.cs

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;

namespace ava_demo_new.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string windowTitle = "ava_demo";

        [RelayCommand]
        public void MyCustomButtonClick()
        {
            Debug.WriteLine("自定义按钮被点击了！");
            Console.WriteLine("自定义按钮被点击了！2");
            WindowTitle = "按钮被点击 - ava_demo_new2";
        }
    }
}