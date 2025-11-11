using System;
using System.Collections.Generic;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Media;
using Avalonia.Layout; // 添加这个命名空间

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
                double canvasWidth = DragCanvas?.Bounds.Width ?? 400;
                double canvasHeight = DragCanvas?.Bounds.Height ?? 300;

                // 边界检查
                newX = Math.Max(0, Math.Min(newX, canvasWidth - _draggedControl.Bounds.Width));
                newY = Math.Max(0, Math.Min(newY, canvasHeight - _draggedControl.Bounds.Height));

                // 功能2：网格吸附
                if (_enableSnapToGrid)
                {
                    newX = SnapToGrid(newX);
                    newY = SnapToGrid(newY);

                    // 网格吸附后重新检查边界
                    newX = Math.Max(0, Math.Min(newX, canvasWidth - _draggedControl.Bounds.Width));
                    newY = Math.Max(0, Math.Min(newY, canvasHeight - _draggedControl.Bounds.Height));
                }

                // 功能5：碰撞检测
                if (_enableCollisionDetection && CheckCollision(_draggedControl, newX, newY))
                {
                    // 修复：碰撞发生时，更新起始点，让方块"粘"在障碍物边缘
                    _controlStartPoint = new Point(
                        _draggedControl.GetValue(Canvas.LeftProperty),
                        _draggedControl.GetValue(Canvas.TopProperty)
                    );
                    _dragStartPoint = currentPoint;
            
                    // 如果发生碰撞，不允许移动
                    return;
                }

                // 更新位置
                Canvas.SetLeft(_draggedControl, newX);
                Canvas.SetTop(_draggedControl, newY);

                // 坐标转换
                double displayX = newX;
                double displayY = newY;

                // 调试输出显示坐标
                Console.WriteLine($"显示坐标: ({displayX}, {displayY}), 实际位置: ({newX}, {newY})");

                // 更新UI坐标显示（如果有的话）
                UpdateCoordinateDisplay(displayX, displayY);
            }
        }

        // 更新坐标显示的方法
        private void UpdateCoordinateDisplay(double x, double y)
        {
            // 如果有坐标显示控件，更新它
            // CoordinateDisplay.Text = $"X: {x} mm, Y: {y} mm";
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

                // 精确碰撞检测：使用严格的不等式避免重叠
                if (CheckExactCollision(movingRect, otherRect))
                {
                    // 碰撞时给其他方块添加视觉反馈
                    if (!otherControl.Classes.Contains("colliding"))
                    {
                        otherControl.Classes.Add("colliding");
                        DispatcherTimer.RunOnce(() => { otherControl.Classes.Remove("colliding"); },
                            TimeSpan.FromMilliseconds(200));
                    }

                    return true;
                }
            }

            return false;
        }

        // 精确碰撞检测：允许相邻但不允许重叠
        private bool CheckExactCollision(Rect rect1, Rect rect2)
        {
            // 使用严格的不等式检查，允许边界接触但不允许重叠
            // 添加一个很小的容差值来处理浮点数精度问题
            double tolerance = 0.001;

            bool collisionX = (rect1.Right - tolerance) > rect2.Left &&
                              (rect1.Left + tolerance) < rect2.Right;
            bool collisionY = (rect1.Bottom - tolerance) > rect2.Top &&
                              (rect1.Top + tolerance) < rect2.Bottom;

            return collisionX && collisionY;
        }

        // 检查是否是边界强制相邻的情况
        private bool IsForcedAdjacentDueToBoundary(Control movingControl, Control otherControl,
            double newX, double newY, double otherX, double otherY)
        {
            double canvasWidth = DragCanvas?.Bounds.Width ?? 400;
            double canvasHeight = DragCanvas?.Bounds.Height ?? 300;

            // 检查移动方向
            bool movingFromLeft = newX > otherX;
            bool movingFromRight = newX < otherX;
            bool movingFromTop = newY > otherY;
            bool movingFromBottom = newY < otherY;

            // 检查其他方块是否在边界上
            bool otherAtLeftBoundary = otherX <= 0;
            bool otherAtRightBoundary = otherX >= canvasWidth - otherControl.Bounds.Width;
            bool otherAtTopBoundary = otherY <= 0;
            bool otherAtBottomBoundary = otherY >= canvasHeight - otherControl.Bounds.Height;

            // 如果是向边界方向移动，且目标方块在边界上，允许相邻
            if ((movingFromLeft && otherAtLeftBoundary) ||
                (movingFromRight && otherAtRightBoundary) ||
                (movingFromTop && otherAtTopBoundary) ||
                (movingFromBottom && otherAtBottomBoundary))
            {
                return true;
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

        // ========== 新增：添加工件功能 ==========
        private void AddWorkpiecesButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                // 直接使用XAML中定义的控件
                int xCount = int.Parse(XWorkpieceCount?.Text ?? "1");
                int yCount = int.Parse(YWorkpieceCount?.Text ?? "1");
                double xMargin = double.Parse(XMargin?.Text ?? "10");
                double yMargin = double.Parse(YMargin?.Text ?? "10");

                // 调用添加工件的方法
                AddWorkpieces(xCount, yCount, xMargin, yMargin);
                
                // 保存状态
                SaveCurrentState();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"添加工件失败: {ex.Message}");
            }
        }

        // 新增：添加工件的方法
        private void AddWorkpieces(int xCount, int yCount, double xMargin, double yMargin)
        {
            if (DragCanvas == null) return;

            // 清除现有的方块（除了初始的三个）
            var blocksToRemove = new List<Control>();
            foreach (Control child in DragCanvas.Children)
            {
                if (child is Border border && 
                    child.Name != "Block1" && 
                    child.Name != "Block2" && 
                    child.Name != "Block3")
                {
                    blocksToRemove.Add(child);
                }
            }
            
            foreach (var block in blocksToRemove)
            {
                DragCanvas.Children.Remove(block);
            }

            // 方块尺寸
            double blockWidth = 60;
            double blockHeight = 60;
            
            // 计算起始位置（居中布置）
            double totalWidth = (blockWidth + xMargin) * xCount - xMargin;
            double totalHeight = (blockHeight + yMargin) * yCount - yMargin;
            
            double startX = (DragCanvas.Bounds.Width - totalWidth) / 2;
            double startY = (DragCanvas.Bounds.Height - totalHeight) / 2;

            // 创建新的工件
            int blockNumber = 4; // 从4开始编号
            
            for (int y = 0; y < yCount; y++)
            {
                for (int x = 0; x < xCount; x++)
                {
                    double posX = startX + x * (blockWidth + xMargin);
                    double posY = startY + y * (blockHeight + yMargin);

                    // 边界检查
                    posX = Math.Max(0, Math.Min(posX, DragCanvas.Bounds.Width - blockWidth));
                    posY = Math.Max(0, Math.Min(posY, DragCanvas.Bounds.Height - blockHeight));

                    // 创建新的方块
                    var newBlock = new Border
                    {
                        Width = blockWidth,
                        Height = blockHeight,
                        Background = GetBlockColor(blockNumber),
                        CornerRadius = new CornerRadius(0),
                        Name = $"Block{blockNumber}",
                        Cursor = new Cursor(StandardCursorType.SizeAll)
                    };

                    // 添加文本 - 修正命名空间引用
                    var textBlock = new TextBlock
                    {
                        Text = blockNumber.ToString(),
                        Foreground = Brushes.White,
                        FontSize = 16,
                        HorizontalAlignment = HorizontalAlignment.Center, // 直接使用 HorizontalAlignment
                        VerticalAlignment = VerticalAlignment.Center      // 直接使用 VerticalAlignment
                    };
                    
                    newBlock.Child = textBlock;

                    // 设置位置
                    Canvas.SetLeft(newBlock, posX);
                    Canvas.SetTop(newBlock, posY);

                    // 添加拖拽事件
                    newBlock.PointerPressed += OnPointerPressed;
                    newBlock.PointerMoved += OnPointerMoved;
                    newBlock.PointerReleased += OnPointerReleased;

                    // 添加到画布
                    DragCanvas.Children.Add(newBlock);
                    
                    blockNumber++;
                }
            }
        }

        // 新增：获取方块颜色
        private IBrush GetBlockColor(int blockNumber)
        {
            // 使用不同的颜色来区分方块
            var colors = new[]
            {
                Brushes.Purple,
                Brushes.Orange,
                Brushes.Teal,
                Brushes.Brown,
                Brushes.Pink,
                Brushes.Gray,
                Brushes.DeepSkyBlue,
                Brushes.Gold,
                Brushes.LimeGreen,
                Brushes.IndianRed
            };
            
            return colors[(blockNumber - 1) % colors.Length];
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