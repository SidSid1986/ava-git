using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ava_demo_new.ViewModels
{
    public class OperationViewModel : INotifyPropertyChanged
    {
        private double _platformWidth = 400;   // Y轴长度
        private double _platformHeight = 300;  // X轴长度
        private double _blockWidth = 60;
        private double _blockHeight = 60;

        // 坐标轴尺寸属性
        public double XAxisBorderWidth => 10; // 固定值
        public double YAxisBorderHeight => 10; // 固定值
        
        public double XAxisBorderHeight => PlatformHeight + 10;
        public double YAxisBorderWidth => PlatformWidth + 10;
        
        public double OuterContainerWidth => PlatformWidth;
        public double OuterContainerHeight => PlatformHeight;
        
        public double MainContainerWidth => PlatformWidth + 10;
        public double MainContainerHeight => PlatformHeight + 10;
        
        public string XMaxValue => PlatformHeight.ToString();
        public string YMaxValue => PlatformWidth.ToString();

        // 主要属性
        public double PlatformWidth
        {
            get => _platformWidth;
            set
            {
                if (_platformWidth != value)
                {
                    _platformWidth = value;
                    OnPropertyChanged();
                    // 通知所有依赖属性变更
                    OnPropertyChanged(nameof(YAxisBorderWidth));
                    OnPropertyChanged(nameof(OuterContainerWidth));
                    OnPropertyChanged(nameof(MainContainerWidth));
                    OnPropertyChanged(nameof(YMaxValue));
                }
            }
        }

        public double PlatformHeight
        {
            get => _platformHeight;
            set
            {
                if (_platformHeight != value)
                {
                    _platformHeight = value;
                    OnPropertyChanged();
                    // 通知所有依赖属性变更
                    OnPropertyChanged(nameof(XAxisBorderHeight));
                    OnPropertyChanged(nameof(OuterContainerHeight));
                    OnPropertyChanged(nameof(MainContainerHeight));
                    OnPropertyChanged(nameof(XMaxValue));
                }
            }
        }

        public double BlockWidth
        {
            get => _blockWidth;
            set => SetProperty(ref _blockWidth, value);
        }

        public double BlockHeight
        {
            get => _blockHeight;
            set => SetProperty(ref _blockHeight, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}