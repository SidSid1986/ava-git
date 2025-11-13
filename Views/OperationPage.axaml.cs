using System;
using System.Collections.Generic;
using System.Linq;
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

        // 设置数据字段
        private double _platformWidth = 400;   // Y轴长度
        private double _platformHeight = 300;  // X轴长度
        private double _blockWidth = 60;       // 方块宽度
        private double _blockHeight = 60;      // 方块高度

        public OperationPage()
        {
            InitializeComponent();
            this.Loaded += OnLoaded;
        }

        #region 设置数据方法
        public void SetPlatformSize(double width, double height)
        {
            _platformWidth = width;    // Y轴长度
            _platformHeight = height;  // X轴长度
            UpdatePlatformSize();
        }

        public void SetBlockSize(double width, double height)
        {
            _blockWidth = width;
            _blockHeight = height;
        }

        private void UpdatePlatformSize()
        {
            // 计算坐标轴和容器的尺寸
            double mainGridWidth = _platformWidth  ;    // 主Grid宽度 = Y轴长度 
            double mainGridHeight = _platformHeight + 10;  // 主Grid高度 = X轴长度 + 坐标轴高度(10)
            
            // 更新主容器Grid尺寸
            if (MainContainerGrid != null)
            {
                MainContainerGrid.Width = mainGridWidth;
                MainContainerGrid.Height = mainGridHeight;
            }

            // 更新X轴坐标（左侧的垂直坐标轴）
            if (XAxisBorder != null)
            {
                XAxisBorder.Height = mainGridHeight;  // X轴坐标的高度 = 主Grid高度
            }

            // 更新Y轴坐标（上方的水平坐标轴）
            if (YAxisBorder != null)
            {
                YAxisBorder.Width = mainGridWidth;    // Y轴坐标的宽度 = 主Grid宽度
            }

            // 更新外部容器（托盘）尺寸
            if (OuterContainerBorder != null)
            {
                OuterContainerBorder.Width = _platformWidth;   // 托盘宽度 = Y轴长度
                OuterContainerBorder.Height = _platformHeight; // 托盘高度 = X轴长度
            }

            // 更新Canvas尺寸
            if (DragCanvas != null)
            {
                DragCanvas.Width = _platformWidth;   // Canvas宽度 = Y轴长度
                DragCanvas.Height = _platformHeight; // Canvas高度 = X轴长度
            }

            // 更新坐标显示
            UpdateCoordinateDisplay();
        }

        private void UpdateCoordinateDisplay()
        {
            // 更新X轴最大值显示（左侧垂直坐标轴）
            if (XMaxText != null)
            {
                XMaxText.Text = _platformHeight.ToString(); // X轴最大值 = X轴长度
            }

            // 更新Y轴最大值显示（上方水平坐标轴）
            if (YMaxText != null)
            {
                YMaxText.Text = _platformWidth.ToString(); // Y轴最大值 = Y轴长度
            }
        }
        #endregion

        #region 初始化方法
        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            if (DragCanvas != null)
            {
                // 为初始的三个方块添加事件
                AddDragEventsToChildren(DragCanvas);
                SaveCurrentState();
            }
            
            // 根据设置更新UI
            UpdatePlatformSize();
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

            // 使用设置中的托盘尺寸
            double containerWidth = _platformWidth;   // Y轴长度
            double containerHeight = _platformHeight; // X轴长度

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

                // 检查是否会超出边界
                if (!CheckBoundary(xCount, yCount, xMargin, yMargin))
                {
                    // 超出边界，显示警告
                    // ShowWarningMessage("添加的工件数量超出托盘边界！");
                    return;
                }

                AddWorkpieces(xCount, yCount, xMargin, yMargin);
                SaveCurrentState();
                ResetInputFields();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"添加工件失败: {ex.Message}");
                ShowWarningMessage($"添加工件失败: {ex.Message}");
            }
        }
        
        // 检查边界是否足够容纳所有工件
        // 检查边界是否足够容纳所有工件
        private bool CheckBoundary(int xCount, int yCount, double xMargin, double yMargin)
        {
            if (xCount <= 0 && yCount <= 0) return true;

            // 计算单个工件的总尺寸（包括边距）
            double totalBlockWidth = _blockWidth + 2 * xMargin;   // 工件总宽度 = 方块宽度 + 2 * X边距
            double totalBlockHeight = _blockHeight + 2 * yMargin; // 工件总高度 = 方块高度 + 2 * Y边距

            if (xCount > 0)
            {
                // X轴方向排列：检查宽度是否足够
                double requiredWidth = xCount * totalBlockWidth;
                if (requiredWidth > _platformWidth)
                {
                    Console.WriteLine($"X轴工件超出边界: 需要{requiredWidth}mm, 但托盘宽度只有{_platformWidth}mm");
                    Console.WriteLine($"单个工件宽度: {totalBlockWidth}mm (方块{_blockWidth}mm + 边距{2 * xMargin}mm)");
                    Console.WriteLine($"最多可添加: {CalculateMaxXCount(xMargin, yMargin)} 个X轴工件");
            
                    // 修改这里：调用详细的警告弹窗
                    ShowDetailedWarningMessage(xCount, yCount, xMargin, yMargin);
                    return false;
                }
            }
            else if (yCount > 0)
            {
                // Y轴方向排列：检查高度是否足够
                double requiredHeight = yCount * totalBlockHeight;
                if (requiredHeight > _platformHeight)
                {
                    Console.WriteLine($"Y轴工件超出边界: 需要{requiredHeight}mm, 但托盘高度只有{_platformHeight}mm");
                    Console.WriteLine($"单个工件高度: {totalBlockHeight}mm (方块{_blockHeight}mm + 边距{2 * yMargin}mm)");
                    Console.WriteLine($"最多可添加: {CalculateMaxYCount(xMargin, yMargin)} 个Y轴工件");
            
                    // 修改这里：调用详细的警告弹窗
                    ShowDetailedWarningMessage(xCount, yCount, xMargin, yMargin);
                    return false;
                }
            }

            return true;
        }
        
        // 计算X轴方向最多能添加多少个工件
private int CalculateMaxXCount(double xMargin, double yMargin)
{
    double totalBlockWidth = _blockWidth + 2 * xMargin;
    int maxCount = (int)Math.Floor(_platformWidth / totalBlockWidth);
    return Math.Max(0, maxCount);
}

// 计算Y轴方向最多能添加多少个工件
private int CalculateMaxYCount(double xMargin, double yMargin)
{
    double totalBlockHeight = _blockHeight + 2 * yMargin;
    int maxCount = (int)Math.Floor(_platformHeight / totalBlockHeight);
    return Math.Max(0, maxCount);
}

// 显示警告消息
private async void ShowWarningMessage(string message)
{
    // 创建一个自定义弹窗窗口
    var dialogWindow = new Window
    {
        Title = "警告",
        Width = 400,
        Height = 400,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        CanResize = false,
        SizeToContent = SizeToContent.Manual
    };

    // 弹窗内容
    var stackPanel = new StackPanel
    {
        Spacing = 20,
        Margin = new Thickness(20),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    // 警告图标和文字
    var warningIcon = new TextBlock
    {
        Text = "⚠️",
        FontSize = 32,
        HorizontalAlignment = HorizontalAlignment.Center
    };

    var messageText = new TextBlock
    {
        Text = message,
        FontSize = 14,
        TextWrapping = TextWrapping.Wrap,
        HorizontalAlignment = HorizontalAlignment.Center,
        TextAlignment = TextAlignment.Center
    };

    // 确定按钮
    var okButton = new Button
    {
        Content = "确定",
        Width = 80,
        Height = 30,
        Background = new SolidColorBrush(Color.FromRgb(220, 53, 69)),
        Foreground = Brushes.White,
        HorizontalAlignment = HorizontalAlignment.Center
    };

    okButton.Click += (s, e) => dialogWindow.Close();

    stackPanel.Children.Add(warningIcon);
    stackPanel.Children.Add(messageText);
    stackPanel.Children.Add(okButton);

    dialogWindow.Content = stackPanel;

    // 设置所有者窗口，确保弹窗在应用内居中
    if (VisualRoot is Window parentWindow)
    {
        dialogWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        dialogWindow.ShowDialog(parentWindow); // 移除了 await
    }
    else
    {
        dialogWindow.Show(); // 如果没有父窗口，使用 Show 而不是 ShowDialog
    }
}

// 显示详细信息弹窗（包含最大可添加数量）
private void ShowDetailedWarningMessage(int xCount, int yCount, double xMargin, double yMargin)
{
    double totalBlockWidth = _blockWidth + 2 * xMargin;
    double totalBlockHeight = _blockHeight + 2 * yMargin;
    
    int maxXCount = CalculateMaxXCount(xMargin, yMargin);
    int maxYCount = CalculateMaxYCount(xMargin, yMargin);

    string message;
    if (xCount > 0)
    {
        double requiredWidth = xCount * totalBlockWidth;
        message = $"❌ X轴工件数量超出托盘边界！\n\n" +
                  $"当前设置：{xCount}个工件\n" +
                  $"所需宽度：{requiredWidth:F1}mm\n" +
                  $"托盘宽度：{_platformWidth:F1}mm\n" +
                  $"单个工件：{totalBlockWidth:F1}mm (方块{_blockWidth:F1}mm + 边距{2 * xMargin:F1}mm)\n\n" +
                  $"💡 建议：最多可添加 {maxXCount} 个Y轴工件";
    }
    else
    {
        double requiredHeight = yCount * totalBlockHeight;
        message = $"❌ Y轴工件数量超出托盘边界！\n\n" +
                  $"当前设置：{yCount}个工件\n" +
                  $"所需高度：{requiredHeight:F1}mm\n" +
                  $"托盘高度：{_platformHeight:F1}mm\n" +
                  $"单个工件：{totalBlockHeight:F1}mm (方块{_blockHeight:F1}mm + 边距{2 * yMargin:F1}mm)\n\n" +
                  $"💡 建议：最多可添加 {maxYCount} 个X轴工件";
    }

    ShowWarningMessage(message);
}

// 在界面上显示临时提示消息
private void ShowTemporaryMessage(string message)
{
    // 创建一个临时提示控件
    var warningPanel = new Border
    {
        Background = new SolidColorBrush(Colors.Yellow),
        BorderBrush = new SolidColorBrush(Colors.Orange),
        BorderThickness = new Thickness(2),
        CornerRadius = new CornerRadius(5),
        Padding = new Thickness(10),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Top,
        Margin = new Thickness(0, 10, 0, 0),
        Child = new TextBlock
        {
            Text = message,
            Foreground = new SolidColorBrush(Colors.Black),
            FontSize = 12,
            FontWeight = FontWeight.Bold
        }
    };

    // 添加到主Grid
    if (this.FindControl<Grid>("MainGrid") is Grid mainGrid)
    {
        // 移除之前可能存在的警告
        var existingWarning = mainGrid.Children.FirstOrDefault(c => c is Border b && b.Background?.ToString() == "#FFFFFF00");
        if (existingWarning != null)
        {
            mainGrid.Children.Remove(existingWarning);
        }

        mainGrid.Children.Add(warningPanel);

        // 3秒后自动移除
        DispatcherTimer.RunOnce(() =>
        {
            mainGrid.Children.Remove(warningPanel);
        }, TimeSpan.FromSeconds(3));
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

            // 使用设置中的方块尺寸
            double innerBlockWidth = _blockWidth;   // 使用设置中的方块宽度
            double innerBlockHeight = _blockHeight; // 使用设置中的方块高度
    
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
            
                    // 边界检查 - 使用设置中的托盘尺寸
                    posX = Math.Max(0, Math.Min(posX, _platformWidth - totalWidth));   // Y轴边界
                    posY = Math.Max(0, Math.Min(posY, _platformHeight - totalHeight)); // X轴边界
            
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
            
                    // 边界检查 - 使用设置中的托盘尺寸
                    posX = Math.Max(0, Math.Min(posX, _platformWidth - totalWidth));   // Y轴边界
                    posY = Math.Max(0, Math.Min(posY, _platformHeight - totalHeight)); // X轴边界
            
                    CreateWorkpiece(blockNumber, posX, posY, innerBlockWidth, innerBlockHeight, xMargin, yMargin);
                    blockNumber++;
                }
            }
    
            Console.WriteLine($"添加了 {xCount + yCount} 个工件");
            Console.WriteLine($"托盘尺寸: 宽度(Y轴){_platformWidth} x 高度(X轴){_platformHeight}, 方块尺寸: {_blockWidth}x{_blockHeight}");
    
            // 显示成功消息
            ShowTemporaryMessage($"成功添加 {xCount + yCount} 个工件");
        }

        private void CreateWorkpiece(int blockNumber, double posX, double posY, double innerWidth, double innerHeight, double xMargin, double yMargin)
        {
            // 创建内部方块（作为内容）
            var innerBlock = CreateInnerBlock(blockNumber, innerWidth, innerHeight);

            // 创建外部边框（作为实际的拖拽控件）
            var outerBorder = new Border
            {
                Name = $"Workpiece{blockNumber}",
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
        #endregion

        #region 撤销重做功能
        private void SaveCurrentState()
        {
            var state = new LayoutState();
            foreach (Control child in DragCanvas!.Children)
            {
                if (child is Border border && (border.Name?.StartsWith("Workpiece") == true || 
                    border.Name == "Block1" || border.Name == "Block2" || border.Name == "Block3"))
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

        public void SaveLayout()
        {
            var json = JsonSerializer.Serialize(_undoStack.Peek(), new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine("布局已保存:");
            Console.WriteLine(json);
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