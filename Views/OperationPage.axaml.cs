using System;
using System.Collections.Generic;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ava_demo_new.Views
{
    public partial class OperationPage : UserControl
    {
        private Control? _draggedControl;
        private Point _dragStartPoint;
        private Point _controlStartPoint;
        
        // 新功能：历史记录和网格吸附
        private readonly Stack<LayoutState> _undoStack = new();
        private readonly Stack<LayoutState> _redoStack = new();
        private const double GridSize = 10.0; // 网格大小
        private bool _enableSnapToGrid = true;
        private bool _enableCollisionDetection = true; // 碰撞检测开关

        public OperationPage()
        {
            InitializeComponent();
            this.Loaded += OnLoaded;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            if (DragCanvas != null)
            {
                AddDragEventsToChildren(DragCanvas);
                SaveCurrentState(); // 初始状态
            }
        }

        private void AddDragEventsToChildren(Panel panel)
        {
            foreach (Control child in panel.Children)
            {
                // 只给方块添加事件，跳过其他类型的控件
                if (child is Border)
                {
                    child.PointerPressed += OnPointerPressed;
                    child.PointerMoved += OnPointerMoved;
                    child.PointerReleased += OnPointerReleased;
                }
            }
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Control control && e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
            {
                _draggedControl = control;
                _dragStartPoint = e.GetPosition(DragCanvas);
                
                double left = control.GetValue(Canvas.LeftProperty);
                double top = control.GetValue(Canvas.TopProperty);
                _controlStartPoint = new Point(left, top);
                
                e.Pointer.Capture(control);
                control.Opacity = 0.7;
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_draggedControl != null && e.Pointer.Captured == _draggedControl)
            {
                var currentPoint = e.GetPosition(DragCanvas);
                var delta = currentPoint - _dragStartPoint;
                
                // 计算新的位置
                double newX = _controlStartPoint.X + delta.X;
                double newY = _controlStartPoint.Y + delta.Y;
                
                // 功能1：边界限制
                newX = ApplyBoundaryConstraint(newX, _draggedControl.Bounds.Width, DragCanvas?.Bounds.Width ?? 400);
                newY = ApplyBoundaryConstraint(newY, _draggedControl.Bounds.Height, DragCanvas?.Bounds.Height ?? 300);
                
                // 功能2：网格吸附
                if (_enableSnapToGrid)
                {
                    newX = SnapToGrid(newX);
                    newY = SnapToGrid(newY);
                }
                
                // 功能5：碰撞检测
                if (_enableCollisionDetection && CheckCollision(_draggedControl, newX, newY))
                {
                    // 如果发生碰撞，不允许移动
                    return;
                }
                
                // 更新位置
                Canvas.SetLeft(_draggedControl, newX);
                Canvas.SetTop(_draggedControl, newY);
            }
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_draggedControl != null)
            {
                _draggedControl.Opacity = 1.0;
                e.Pointer.Capture(null);
                
                // 拖拽结束后保存状态（用于撤销/重做）
                SaveCurrentState();
                _draggedControl = null;
            }
        }

        // ========== 功能1：边界限制 ==========
        private double ApplyBoundaryConstraint(double position, double controlSize, double containerSize)
        {
            return Math.Max(0, Math.Min(position, containerSize - controlSize));
        }

        // ========== 功能2：网格吸附 ==========
        private double SnapToGrid(double value)
        {
            return Math.Round(value / GridSize) * GridSize;
        }

        // ========== 功能5：碰撞检测 ==========
        private bool CheckCollision(Control movingControl, double newX, double newY)
        {
            // 创建移动方块的矩形区域
            var movingRect = new Rect(newX, newY, movingControl.Bounds.Width, movingControl.Bounds.Height);
            
            foreach (Control otherControl in DragCanvas.Children)
            {
                // 跳过自身和非方块控件
                if (otherControl == movingControl || !(otherControl is Border))
                    continue;
                
                // 获取其他方块的位置
                double otherLeft = otherControl.GetValue(Canvas.LeftProperty);
                double otherTop = otherControl.GetValue(Canvas.TopProperty);
                var otherRect = new Rect(otherLeft, otherTop, otherControl.Bounds.Width, otherControl.Bounds.Height);
                
                // 检查两个矩形是否相交（碰撞）
                if (movingRect.Intersects(otherRect))
                {
                    // 碰撞时给其他方块添加视觉反馈
                    otherControl.Classes.Add("colliding");
                    DispatcherTimer.RunOnce(() => 
                    {
                        otherControl.Classes.Remove("colliding");
                    }, TimeSpan.FromMilliseconds(200));
                    
                    return true;
                }
            }
            
            return false;
        }

        // ========== 功能3：撤销/重做 ==========
        private void SaveCurrentState()
        {
            var state = new LayoutState();
            foreach (Control child in DragCanvas.Children)
            {
                if (child is Border) // 只保存方块的位置
                {
                    var elementState = new ElementState
                    {
                        Name = child.Name ?? "Unknown",
                        Left = child.GetValue(Canvas.LeftProperty),
                        Top = child.GetValue(Canvas.TopProperty)
                    };
                    state.Elements.Add(elementState);
                }
            }
            _undoStack.Push(state);
            _redoStack.Clear(); // 新的操作后清空重做栈
        }

        private void ApplyLayoutState(LayoutState state)
        {
            foreach (var elementState in state.Elements)
            {
                var control = FindControlByName(elementState.Name);
                if (control != null)
                {
                    Canvas.SetLeft(control, elementState.Left);
                    Canvas.SetTop(control, elementState.Top);
                }
            }
        }

        private Control? FindControlByName(string name)
        {
            foreach (Control child in DragCanvas.Children)
            {
                if (child.Name == name)
                    return child;
            }
            return null;
        }

        // ========== 功能4：保存/加载布局 ==========
        private string SerializeLayout()
        {
            var layout = new LayoutState();
            foreach (Control child in DragCanvas.Children)
            {
                if (child is Border)
                {
                    layout.Elements.Add(new ElementState
                    {
                        Name = child.Name ?? "Unknown",
                        Left = child.GetValue(Canvas.LeftProperty),
                        Top = child.GetValue(Canvas.TopProperty)
                    });
                }
            }
            return JsonSerializer.Serialize(layout, new JsonSerializerOptions { WriteIndented = true });
        }

        private void DeserializeLayout(string json)
        {
            try
            {
                var layout = JsonSerializer.Deserialize<LayoutState>(json);
                if (layout != null)
                {
                    ApplyLayoutState(layout);
                    SaveCurrentState();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载布局失败: {ex.Message}");
            }
        }

        // ========== 公共方法（可以从XAML调用） ==========
        
        public void Undo()
        {
            if (_undoStack.Count > 1) // 保留初始状态
            {
                _redoStack.Push(_undoStack.Pop());
                ApplyLayoutState(_undoStack.Peek());
            }
        }

        public void Redo()
        {
            if (_redoStack.Count > 0)
            {
                var state = _redoStack.Pop();
                _undoStack.Push(state);
                ApplyLayoutState(state);
            }
        }

        public void ToggleGridSnap()
        {
            _enableSnapToGrid = !_enableSnapToGrid;
        }

        public void ToggleCollisionDetection()
        {
            _enableCollisionDetection = !_enableCollisionDetection;
        }

        public void SaveLayout()
        {
            var json = SerializeLayout();
            // 在实际应用中，这里可以保存到文件或数据库
            Console.WriteLine("布局已保存:");
            Console.WriteLine(json);
        }

        public void LoadLayout(string json)
        {
            DeserializeLayout(json);
        }

        // ========== 按钮事件处理方法 ==========
        
        private void BackButton_Click(object? sender, RoutedEventArgs e)
        {
            if (this.Parent is ContentControl contentControl && OriginalContent != null)
            {
                contentControl.Content = OriginalContent;
            }
        }

        private void UndoButton_Click(object? sender, RoutedEventArgs e)
        {
            Undo();
        }

        private void RedoButton_Click(object? sender, RoutedEventArgs e)
        {
            Redo();
        }

        private void ToggleGridButton_Click(object? sender, RoutedEventArgs e)
        {
            ToggleGridSnap();
            if (sender is Button button)
            {
                button.Content = _enableSnapToGrid ? "网格吸附:开" : "网格吸附:关";
            }
        }

        private void ToggleCollisionButton_Click(object? sender, RoutedEventArgs e)
        {
            ToggleCollisionDetection();
            if (sender is Button button)
            {
                button.Content = _enableCollisionDetection ? "碰撞检测:开" : "碰撞检测:关";
            }
        }

        private void SaveLayoutButton_Click(object? sender, RoutedEventArgs e)
        {
            SaveLayout();
        }

        private void ResetLayoutButton_Click(object? sender, RoutedEventArgs e)
        {
            // 重置到初始位置
            var block1 = this.FindControl<Border>("Block1");
            var block2 = this.FindControl<Border>("Block2");
            var block3 = this.FindControl<Border>("Block3");
            
            if (block1 != null)
            {
                Canvas.SetLeft(block1, 50);
                Canvas.SetTop(block1, 50);
            }
            if (block2 != null)
            {
                Canvas.SetLeft(block2, 150);
                Canvas.SetTop(block2, 50);
            }
            if (block3 != null)
            {
                Canvas.SetLeft(block3, 250);
                Canvas.SetTop(block3, 50);
            }
            
            SaveCurrentState();
        }

        public Control? OriginalContent { get; set; }
    }
}

// ========== 数据模型（放在 OperationPage 类外面） ==========
namespace ava_demo_new.Views
{
    public class LayoutState
    {
        public List<ElementState> Elements { get; set; } = new();
    }

    public class ElementState
    {
        public string Name { get; set; } = string.Empty;
        public double Left { get; set; }
        public double Top { get; set; }
    }
}