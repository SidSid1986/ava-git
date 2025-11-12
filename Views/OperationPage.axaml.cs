using System;
using System.Collections.Generic;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Media;
using Avalonia.Layout;

namespace ava_demo_new.Views
{
    public partial class OperationPage : UserControl
    {
        private Control? _draggedControl;
        private Point _dragStartPoint;
        private Point _controlStartPoint;

        private readonly Stack<LayoutState> _undoStack = new();
        private readonly Stack<LayoutState> _redoStack = new();
        private const double GridSize = 10.0;
        private bool _enableSnapToGrid = true;
        private bool _enableCollisionDetection = true;

        public OperationPage()
        {
            InitializeComponent();
            this.Loaded += OnLoaded;
        }

        #region 初始化方法
        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            if (DragCanvas != null)
            {
                // 为初始的三个方块添加事件
                AddDragEventsToChildren(DragCanvas);
                SaveCurrentState();
            }
        }

        private void AddDragEventsToChildren(Panel panel)
        {
            foreach (Control child in panel.Children)
            {
                AddDragEvents(child);
            }
        }
        #endregion

        #region 拖拽事件处理
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
                
                Console.WriteLine($"开始拖拽: {control.Name}, 位置: ({left}, {top})");
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_draggedControl != null && e.Pointer.Captured == _draggedControl)
            {
                var currentPoint = e.GetPosition(DragCanvas);
                var delta = currentPoint - _dragStartPoint;

                double newX = _controlStartPoint.X + delta.X;
                double newY = _controlStartPoint.Y + delta.Y;

                // 边界检查和网格吸附
                ApplyConstraints(ref newX, ref newY);

                // 碰撞检测
                if (_enableCollisionDetection && CheckCollision(_draggedControl, newX, newY))
                {
                    ResetDragStart(currentPoint);
                    return;
                }

                // 更新位置
                Canvas.SetLeft(_draggedControl, newX);
                Canvas.SetTop(_draggedControl, newY);

                Console.WriteLine($"拖拽中: ({newX}, {newY})");
                UpdateCoordinateDisplay(newX, newY);
            }
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_draggedControl != null)
            {
                _draggedControl.Opacity = 1.0;
                e.Pointer.Capture(null);
                SaveCurrentState();
                Console.WriteLine($"拖拽结束: {_draggedControl.Name}");
                _draggedControl = null;
            }
        }
        #endregion

        #region 约束和碰撞检测
        private void ApplyConstraints(ref double newX, ref double newY)
        {
            if (_draggedControl == null) return;

            double containerWidth = OuterContainerBorder?.Bounds.Width ?? 400;
            double containerHeight = OuterContainerBorder?.Bounds.Height ?? 300;

            var controlRect = GetControlRect(_draggedControl);
            double controlWidth = controlRect.Width;
            double controlHeight = controlRect.Height;

            // 边界约束
            newX = Math.Max(0, Math.Min(newX, containerWidth - controlWidth));
            newY = Math.Max(0, Math.Min(newY, containerHeight - controlHeight));

            // 网格吸附
            if (_enableSnapToGrid)
            {
                newX = SnapToGrid(newX);
                newY = SnapToGrid(newY);
                
                newX = Math.Max(0, Math.Min(newX, containerWidth - controlWidth));
                newY = Math.Max(0, Math.Min(newY, containerHeight - controlHeight));
            }
        }

        private bool CheckCollision(Control movingControl, double newX, double newY)
        {
            if (DragCanvas == null) return false;

            Rect movingRect = GetControlRect(movingControl, newX, newY);
            double xMargin = GetCurrentXMargin();
            double yMargin = GetCurrentYMargin();

            foreach (Control otherControl in DragCanvas.Children)
            {
                if (otherControl == movingControl) continue;

                Rect otherRect = GetControlRect(otherControl);

                if (CheckCollisionWithMargin(movingRect, otherRect, xMargin, yMargin))
                {
                    ShowCollisionFeedback(otherControl);
                    return true;
                }
            }

            return false;
        }

        private bool CheckCollisionWithMargin(Rect rect1, Rect rect2, double xMargin, double yMargin)
        {
            // 直接比较两个矩形是否重叠，不需要额外的边距计算
            // 因为边框已经包含了边距信息
            double tolerance = 0.001;
            bool collisionX = (rect1.Right - tolerance) > rect2.Left &&
                              (rect1.Left + tolerance) < rect2.Right;
            bool collisionY = (rect1.Bottom - tolerance) > rect2.Top &&
                              (rect1.Top + tolerance) < rect2.Bottom;

            return collisionX && collisionY;
        }
        #endregion

        #region 工件添加功能
        private void AddWorkpiecesButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                int xCount = int.Parse(XWorkpieceCount?.Text ?? "0");
                int yCount = int.Parse(YWorkpieceCount?.Text ?? "0");
                double xMargin = double.Parse(XMargin?.Text ?? "10");
                double yMargin = double.Parse(YMargin?.Text ?? "10");

                if (!ValidateInput(xCount, yCount)) return;

                AddWorkpieces(xCount, yCount, xMargin, yMargin);
                SaveCurrentState();
                ResetInputFields();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"添加工件失败: {ex.Message}");
            }
        }

        private void OnWorkpieceCountTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (!string.IsNullOrEmpty(textBox.Text) && !int.TryParse(textBox.Text, out _))
                {
                    textBox.Text = "0";
                    return;
                }

                // 互斥逻辑
                if (textBox == XWorkpieceCount && !string.IsNullOrEmpty(textBox.Text) && textBox.Text != "0")
                {
                    YWorkpieceCount!.Text = "0";
                }
                else if (textBox == YWorkpieceCount && !string.IsNullOrEmpty(textBox.Text) && textBox.Text != "0")
                {
                    XWorkpieceCount!.Text = "0";
                }
            }
        }

        private void AddWorkpieces(int xCount, int yCount, double xMargin, double yMargin)
        {
            if (DragCanvas == null || OuterContainerBorder == null) return;

            ClearExistingWorkpieces();

            double innerBlockWidth = 60;  // 内部方块宽度
            double innerBlockHeight = 60; // 内部方块高度
            
            // 外部边框的总尺寸 = 内部方块尺寸 + 2 * 边距
            double totalWidth = innerBlockWidth + 2 * xMargin;
            double totalHeight = innerBlockHeight + 2 * yMargin;

            double startX = 0; // 从左上角开始
            double startY = 0;
            int blockNumber = 4;

            if (xCount > 0)
            {
                for (int x = 0; x < xCount; x++)
                {
                    double posX = startX + x * totalWidth;
                    double posY = startY;
                    
                    // 边界检查
                    posX = Math.Max(0, Math.Min(posX, OuterContainerBorder.Bounds.Width - totalWidth));
                    posY = Math.Max(0, Math.Min(posY, OuterContainerBorder.Bounds.Height - totalHeight));
                    
                    CreateWorkpiece(blockNumber, posX, posY, innerBlockWidth, innerBlockHeight, xMargin, yMargin);
                    blockNumber++;
                }
            }
            else if (yCount > 0)
            {
                for (int y = 0; y < yCount; y++)
                {
                    double posX = startX;
                    double posY = startY + y * totalHeight;
                    
                    // 边界检查
                    posX = Math.Max(0, Math.Min(posX, OuterContainerBorder.Bounds.Width - totalWidth));
                    posY = Math.Max(0, Math.Min(posY, OuterContainerBorder.Bounds.Height - totalHeight));
                    
                    CreateWorkpiece(blockNumber, posX, posY, innerBlockWidth, innerBlockHeight, xMargin, yMargin);
                    blockNumber++;
                }
            }
            
            Console.WriteLine($"添加了 {xCount + yCount} 个工件");
        }

        private void CreateWorkpiece(int blockNumber, double posX, double posY, double innerWidth, double innerHeight, double xMargin, double yMargin)
        {
            // 创建内部方块（作为内容）
            var innerBlock = CreateInnerBlock(blockNumber, innerWidth, innerHeight);

            // 创建外部边框（作为实际的拖拽控件）
            var outerBorder = new Border
            {
                Name = $"Workpiece{blockNumber}", // 统一命名，便于识别
                Width = innerWidth + 2 * xMargin, // 总宽度 = 内部宽度 + 2 * 边距
                Height = innerHeight + 2 * yMargin, // 总高度 = 内部高度 + 2 * 边距
                Background = Brushes.Transparent,
                BorderBrush = xMargin > 0 || yMargin > 0 ? Brushes.Blue : Brushes.Transparent,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Cursor = new Cursor(StandardCursorType.SizeAll)
            };

            // 创建容器来放置内部方块
            var container = new Grid
            {
                Width = innerWidth + 2 * xMargin,
                Height = innerHeight + 2 * yMargin,
                Background = Brushes.Transparent
            };

            // 设置内部方块在容器中的位置（居中）
            innerBlock.HorizontalAlignment = HorizontalAlignment.Center;
            innerBlock.VerticalAlignment = VerticalAlignment.Center;
            innerBlock.Margin = new Thickness(0);

            container.Children.Add(innerBlock);
            outerBorder.Child = container;

            // 设置外部边框的位置
            Canvas.SetLeft(outerBorder, posX);
            Canvas.SetTop(outerBorder, posY);

            // 为外部边框添加拖拽事件
            AddDragEvents(outerBorder);
            DragCanvas.Children.Add(outerBorder);
            
            Console.WriteLine($"创建工件: Workpiece{blockNumber}, 位置: ({posX}, {posY}), 尺寸: {outerBorder.Width}x{outerBorder.Height}");
        }

        private Border CreateInnerBlock(int blockNumber, double width, double height)
        {
            var innerBlock = new Border
            {
                Width = width,
                Height = height,
                Background = GetBlockColor(blockNumber),
                CornerRadius = new CornerRadius(0)
            };

            var textBlock = new TextBlock
            {
                Text = blockNumber.ToString(),
                Foreground = Brushes.White,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            innerBlock.Child = textBlock;
            return innerBlock;
        }
        #endregion

        #region 工具方法
        private Rect GetControlRect(Control control, double? newX = null, double? newY = null)
        {
            double left = newX ?? control.GetValue(Canvas.LeftProperty);
            double top = newY ?? control.GetValue(Canvas.TopProperty);

            // 所有工件都是Border，直接使用Border的尺寸
            if (control is Border border)
            {
                return new Rect(left, top, border.Width, border.Height);
            }

            return new Rect(left, top, control.Bounds.Width, control.Bounds.Height);
        }

        private double GetCurrentXMargin() => double.Parse(XMargin?.Text ?? "10");
        private double GetCurrentYMargin() => double.Parse(YMargin?.Text ?? "10");

        private double SnapToGrid(double value) => Math.Round(value / GridSize) * GridSize;

        private bool ValidateInput(int xCount, int yCount)
        {
            if (xCount > 0 && yCount > 0)
            {
                Console.WriteLine("错误：X轴和Y轴工件数不能同时大于0");
                return false;
            }

            if (xCount <= 0 && yCount <= 0)
            {
                Console.WriteLine("错误：请至少设置一个方向的工件数");
                return false;
            }

            return true;
        }

        private void ClearExistingWorkpieces()
        {
            var blocksToRemove = new List<Control>();
            foreach (Control child in DragCanvas!.Children)
            {
                if (child is Border border && border.Name?.StartsWith("Workpiece") == true)
                {
                    blocksToRemove.Add(child);
                }
            }

            foreach (var block in blocksToRemove)
            {
                DragCanvas.Children.Remove(block);
            }
            
            Console.WriteLine($"清除了 {blocksToRemove.Count} 个现有工件");
        }

        private void ResetInputFields()
        {
            XWorkpieceCount!.Text = "0";
            YWorkpieceCount!.Text = "0";
        }

        private void ResetDragStart(Point currentPoint)
        {
            if (_draggedControl != null)
            {
                _controlStartPoint = new Point(
                    _draggedControl.GetValue(Canvas.LeftProperty),
                    _draggedControl.GetValue(Canvas.TopProperty)
                );
                _dragStartPoint = currentPoint;
            }
        }

        private void ShowCollisionFeedback(Control control)
        {
            if (!control.Classes.Contains("colliding"))
            {
                control.Classes.Add("colliding");
                DispatcherTimer.RunOnce(() => control.Classes.Remove("colliding"), 
                    TimeSpan.FromMilliseconds(200));
            }
        }

        // 统一的拖拽事件添加方法
        private void AddDragEvents(Control control)
        {
            control.PointerPressed += OnPointerPressed;
            control.PointerMoved += OnPointerMoved;
            control.PointerReleased += OnPointerReleased;
        }

        private IBrush GetBlockColor(int blockNumber)
        {
            var colors = new[]
            {
                Brushes.Purple, Brushes.Orange, Brushes.Teal, Brushes.Brown, Brushes.Pink,
                Brushes.Gray, Brushes.DeepSkyBlue, Brushes.Gold, Brushes.LimeGreen, Brushes.IndianRed
            };
            return colors[(blockNumber - 1) % colors.Length];
        }

        private void UpdateCoordinateDisplay(double x, double y)
        {
            // 坐标显示逻辑
        }
        #endregion

        #region 撤销重做功能
        private void SaveCurrentState()
        {
            var state = new LayoutState();
            foreach (Control child in DragCanvas!.Children)
            {
                if (child is Border border && border.Name?.StartsWith("Workpiece") == true)
                {
                    state.Elements.Add(new ElementState
                    {
                        Name = child.Name ?? "Unknown",
                        Left = child.GetValue(Canvas.LeftProperty),
                        Top = child.GetValue(Canvas.TopProperty)
                    });
                }
            }

            _undoStack.Push(state);
            _redoStack.Clear();
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
            foreach (Control child in DragCanvas!.Children)
            {
                if (child.Name == name)
                    return child;
            }
            return null;
        }
        #endregion

        #region 按钮事件
        private void UndoButton_Click(object? sender, RoutedEventArgs e) => Undo();
        private void RedoButton_Click(object? sender, RoutedEventArgs e) => Redo();

        private void ToggleGridButton_Click(object? sender, RoutedEventArgs e)
        {
            _enableSnapToGrid = !_enableSnapToGrid;
            if (sender is Button button)
            {
                button.Content = _enableSnapToGrid ? "网格吸附:开" : "网格吸附:关";
            }
        }

        private void ToggleCollisionButton_Click(object? sender, RoutedEventArgs e)
        {
            _enableCollisionDetection = !_enableCollisionDetection;
            if (sender is Button button)
            {
                button.Content = _enableCollisionDetection ? "碰撞检测:开" : "碰撞检测:关";
            }
        }

        private void SaveLayoutButton_Click(object? sender, RoutedEventArgs e) => SaveLayout();
        
        private void BackButton_Click(object? sender, RoutedEventArgs e)
        {
            if (this.Parent is ContentControl contentControl && OriginalContent != null)
            {
                contentControl.Content = OriginalContent;
            }
        }

        private void ResetLayoutButton_Click(object? sender, RoutedEventArgs e)
        {
            // 重置初始三个方块的位置
            var block1 = this.FindControl<Border>("Block1");
            var block2 = this.FindControl<Border>("Block2");
            var block3 = this.FindControl<Border>("Block3");

            if (block1 != null) { Canvas.SetLeft(block1, 50); Canvas.SetTop(block1, 50); }
            if (block2 != null) { Canvas.SetLeft(block2, 150); Canvas.SetTop(block2, 50); }
            if (block3 != null) { Canvas.SetLeft(block3, 250); Canvas.SetTop(block3, 50); }

            SaveCurrentState();
        }
        #endregion

        #region 公共方法
        public void Undo()
        {
            if (_undoStack.Count > 1)
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

        public void ToggleGridSnap() => _enableSnapToGrid = !_enableSnapToGrid;
        public void ToggleCollisionDetection() => _enableCollisionDetection = !_enableCollisionDetection;

        public void SaveLayout()
        {
            var json = JsonSerializer.Serialize(_undoStack.Peek(), new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine("布局已保存:");
            Console.WriteLine(json);
        }

        public void LoadLayout(string json)
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

        public Control? OriginalContent { get; set; }
        #endregion
    }

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