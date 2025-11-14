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
        private double _platformWidth = 400; // Y轴长度
        private double _platformHeight = 300; // X轴长度
        private double _blockWidth = 60; // 方块宽度
        private double _blockHeight = 60; // 方块高度

        // 新增：记录工件布局状态
        private int _currentBlockNumber = 1;
        private double _currentYPosition = 0; // 当前Y轴位置（用于换行）
        private double _currentXPosition = 0; // 当前X轴位置（用于换列）

        // 新增：选中的工件
        private Border? _selectedWorkpiece;

        // 存储每个工件的旋转角度
        private readonly Dictionary<string, double> _workpieceRotations = new();

        public OperationPage()
        {
            InitializeComponent();
            this.Loaded += OnLoaded;
        }

        #region 设置数据方法

        public void SetPlatformSize(double width, double height)
        {
            _platformWidth = width; // Y轴长度
            _platformHeight = height; // X轴长度
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
            double mainGridWidth = _platformWidth; // 主Grid宽度 = Y轴长度 
            double mainGridHeight = _platformHeight + 10; // 主Grid高度 = X轴长度 + 坐标轴高度(10)

            // 更新主容器Grid尺寸
            if (MainContainerGrid != null)
            {
                MainContainerGrid.Width = mainGridWidth;
                MainContainerGrid.Height = mainGridHeight;
            }

            // 更新X轴坐标（左侧的垂直坐标轴）
            if (XAxisBorder != null)
            {
                XAxisBorder.Height = mainGridHeight; // X轴坐标的高度 = 主Grid高度
            }

            // 更新Y轴坐标（上方的水平坐标轴）
            if (YAxisBorder != null)
            {
                YAxisBorder.Width = mainGridWidth; // Y轴坐标的宽度 = 主Grid宽度
            }

            // 更新外部容器（托盘）尺寸
            if (OuterContainerBorder != null)
            {
                OuterContainerBorder.Width = _platformWidth; // 托盘宽度 = Y轴长度
                OuterContainerBorder.Height = _platformHeight; // 托盘高度 = X轴长度
            }

            // 更新Canvas尺寸
            if (DragCanvas != null)
            {
                DragCanvas.Width = _platformWidth; // Canvas宽度 = Y轴长度
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
                // 为现有的工件添加事件
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

        #region 旋转功能 - 完全重新设计

        private void RotateLeftButton_Click(object? sender, RoutedEventArgs e)
        {
            RotateWorkpiece(-90); // 向左旋转90度
        }

        private void RotateRightButton_Click(object? sender, RoutedEventArgs e)
        {
            RotateWorkpiece(90); // 向右旋转90度
        }

        private void RotateWorkpiece(double angle)
        {
            if (_selectedWorkpiece == null)
            {
                ShowTemporaryMessage("请先选择一个工件");
                return;
            }

            // 获取当前工件的旋转角度
            string workpieceName = _selectedWorkpiece.Name ?? "";
            if (!_workpieceRotations.ContainsKey(workpieceName))
            {
                _workpieceRotations[workpieceName] = 0;
            }

            double currentRotation = _workpieceRotations[workpieceName];

            // 计算新的旋转角度
            double newRotation = (currentRotation + angle) % 360;
            if (newRotation < 0) newRotation += 360;

            _workpieceRotations[workpieceName] = newRotation;

            Console.WriteLine($"旋转工件 {workpieceName}: {currentRotation}° -> {newRotation}°");

            // 应用旋转
            ApplyRotationToWorkpiece(_selectedWorkpiece, newRotation);
            SaveCurrentState();
        }

        private void ApplyRotationToWorkpiece(Border workpiece, double rotation)
        {
            // 获取边距
            double xMargin = GetCurrentXMargin();
            double yMargin = GetCurrentYMargin();

            // 保存旋转前的位置
            double originalLeft = Canvas.GetLeft(workpiece);
            double originalTop = Canvas.GetTop(workpiece);
            double originalWidth = workpiece.Width;
            double originalHeight = workpiece.Height;

            // 计算旋转后的尺寸
            double newWidth, newHeight;
            double innerNewWidth, innerNewHeight;

            if (rotation % 180 == 90) // 90度或270度
            {
                // 外部边框：宽高交换，边距也要交换
                newWidth = _blockHeight + 2 * yMargin;
                newHeight = _blockWidth + 2 * xMargin;
                // 内部块：宽高交换
                innerNewWidth = _blockHeight;
                innerNewHeight = _blockWidth;
            }
            else // 0度或180度
            {
                // 外部边框：原始尺寸
                newWidth = _blockWidth + 2 * xMargin;
                newHeight = _blockHeight + 2 * yMargin;
                // 内部块：原始尺寸
                innerNewWidth = _blockWidth;
                innerNewHeight = _blockHeight;
            }

            // 更新外部边框尺寸
            workpiece.Width = newWidth;
            workpiece.Height = newHeight;

            // 更新内部容器和内部块的尺寸
            if (workpiece.Child is Grid container)
            {
                container.Width = newWidth;
                container.Height = newHeight;

                if (container.Children[0] is Border innerBlock)
                {
                    // 更新内部块的尺寸
                    innerBlock.Width = innerNewWidth;
                    innerBlock.Height = innerNewHeight;
                    innerBlock.HorizontalAlignment = HorizontalAlignment.Center;
                    innerBlock.VerticalAlignment = VerticalAlignment.Center;

                    // 给内部块中的文字添加旋转变换
                    if (innerBlock.Child is TextBlock textBlock)
                    {
                        textBlock.RenderTransform = new RotateTransform(rotation);
                        textBlock.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
                    }

                    Console.WriteLine($"内部块尺寸: {innerNewWidth}x{innerNewHeight}, 文字旋转: {rotation}°");
                }
            }

            // 计算新的位置，保持中心点不变
            double centerX = originalLeft + originalWidth / 2;
            double centerY = originalTop + originalHeight / 2;
            double newLeft = centerX - newWidth / 2;
            double newTop = centerY - newHeight / 2;

            // 应用新位置
            Canvas.SetLeft(workpiece, newLeft);
            Canvas.SetTop(workpiece, newTop);

            Console.WriteLine($"应用旋转: 外部边框 {originalWidth}x{originalHeight} -> {newWidth}x{newHeight}");
            Console.WriteLine($"位置: ({newLeft}, {newTop})");
        }

        private void SelectWorkpiece(Border workpiece)
        {
            // 取消之前选中的工件
            if (_selectedWorkpiece != null)
            {
                _selectedWorkpiece.Classes.Remove("selected");
                ResetInnerBlockColor(_selectedWorkpiece); // 恢复内部方块颜色
                Console.WriteLine($"取消选中: {_selectedWorkpiece.Name}");
            }

            // 选中新的工件
            _selectedWorkpiece = workpiece;

            // 确保添加selected类
            if (!_selectedWorkpiece.Classes.Contains("selected"))
            {
                _selectedWorkpiece.Classes.Add("selected");
            }

            // 设置内部方块为蓝色
            SetInnerBlockColor(_selectedWorkpiece, Brushes.Blue);

            // 获取当前旋转角度
            string workpieceName = workpiece.Name ?? "";
            double rotation = _workpieceRotations.ContainsKey(workpieceName) ? _workpieceRotations[workpieceName] : 0;

            Console.WriteLine($"选中工件: {workpieceName}, 当前旋转: {rotation}°, 尺寸: {workpiece.Width}x{workpiece.Height}");
        }

        private void DeselectWorkpiece()
        {
            if (_selectedWorkpiece != null)
            {
                _selectedWorkpiece.Classes.Remove("selected");
                ResetInnerBlockColor(_selectedWorkpiece); // 恢复内部方块颜色
                Console.WriteLine($"取消选中: {_selectedWorkpiece.Name}");
                _selectedWorkpiece = null;
            }
        }

// 设置内部方块颜色
        private void SetInnerBlockColor(Border outerBorder, IBrush color)
        {
            if (outerBorder.Child is Grid container && container.Children[0] is Border innerBlock)
            {
                innerBlock.Background = color;
            }
        }

// 恢复内部方块颜色为粉色
        private void ResetInnerBlockColor(Border outerBorder)
        {
            if (outerBorder.Child is Grid container && container.Children[0] is Border innerBlock)
            {
                innerBlock.Background = Brushes.Pink;
            }
        }

        #endregion

        #region 删除和清空功能

        private void DeleteButton_Click(object? sender, RoutedEventArgs e)
        {
            DeleteLastWorkpiece();
        }

        private void ClearAllButton_Click(object? sender, RoutedEventArgs e)
        {
            ClearAllWorkpieces();
        }

        private void DeleteLastWorkpiece()
        {
            if (DragCanvas == null) return;

            var lastWorkpiece = DragCanvas.Children
                .OfType<Border>()
                .Where(b => b.Name?.StartsWith("Workpiece") == true)
                .OrderByDescending(b => b.Name)
                .FirstOrDefault();

            if (lastWorkpiece != null)
            {
                if (_selectedWorkpiece == lastWorkpiece)
                {
                    DeselectWorkpiece();
                }

                // 从字典中移除
                _workpieceRotations.Remove(lastWorkpiece.Name ?? "");

                DragCanvas.Children.Remove(lastWorkpiece);
                _currentBlockNumber--;
                SaveCurrentState();
                ShowTemporaryMessage("已删除最后一个工件");
                UpdateLayoutState();
            }
            else
            {
                ShowTemporaryMessage("没有可删除的工件");
            }
        }

        private void ClearAllWorkpieces()
        {
            if (DragCanvas == null) return;

            DeselectWorkpiece();

            var workpiecesToRemove = DragCanvas.Children
                .OfType<Border>()
                .Where(b => b.Name?.StartsWith("Workpiece") == true)
                .ToList();

            foreach (var workpiece in workpiecesToRemove)
            {
                _workpieceRotations.Remove(workpiece.Name ?? "");
                DragCanvas.Children.Remove(workpiece);
            }

            _currentBlockNumber = 1;
            _currentYPosition = 0;
            _currentXPosition = 0;
            SaveCurrentState();
            ShowTemporaryMessage("已清空所有工件");
        }

        private void UpdateLayoutState()
        {
            if (DragCanvas == null) return;

            var workpieces = DragCanvas.Children
                .OfType<Border>()
                .Where(b => b.Name?.StartsWith("Workpiece") == true)
                .OrderBy(b => b.Name)
                .ToList();

            if (workpieces.Count == 0)
            {
                _currentBlockNumber = 1;
                _currentYPosition = 0;
                _currentXPosition = 0;
                return;
            }

            _currentBlockNumber = workpieces.Count + 1;
            var lastWorkpiece = workpieces.Last();
            double lastTop = Canvas.GetTop(lastWorkpiece);
            double lastLeft = Canvas.GetLeft(lastWorkpiece);
            double lastHeight = lastWorkpiece.Height;
            double lastWidth = lastWorkpiece.Width;

            _currentYPosition = lastTop;
            _currentXPosition = lastLeft + lastWidth;
        }

        #endregion

        #region 拖拽事件处理

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Control control && e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
            {
                if (control is Border border && border.Name?.StartsWith("Workpiece") == true)
                {
                    SelectWorkpiece(border);
                }

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

                // 使用简单的边界约束（基于实际尺寸）
                ApplySimpleConstraints(ref newX, ref newY);

                if (_enableCollisionDetection && CheckCollision(_draggedControl, newX, newY))
                {
                    ResetDragStart(currentPoint);
                    return;
                }

                Canvas.SetLeft(_draggedControl, newX);
                Canvas.SetTop(_draggedControl, newY);

                Console.WriteLine($"拖拽中: ({newX:F1}, {newY:F1})");
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

        #region 约束和碰撞检测 - 简化版本（基于实际尺寸）

        private void ApplySimpleConstraints(ref double newX, ref double newY)
        {
            if (_draggedControl == null) return;

            double containerWidth = _platformWidth;
            double containerHeight = _platformHeight;

            // 使用工件的实际尺寸进行边界约束
            double controlWidth = _draggedControl.Width;
            double controlHeight = _draggedControl.Height;

            // 简单的边界约束
            newX = Math.Max(0, Math.Min(newX, containerWidth - controlWidth));
            newY = Math.Max(0, Math.Min(newY, containerHeight - controlHeight));

            // 网格吸附
            if (_enableSnapToGrid)
            {
                newX = SnapToGrid(newX);
                newY = SnapToGrid(newY);

                // 重新约束边界
                newX = Math.Max(0, Math.Min(newX, containerWidth - controlWidth));
                newY = Math.Max(0, Math.Min(newY, containerHeight - controlHeight));
            }

            Console.WriteLine($"边界约束: ({newX:F1}, {newY:F1}), 尺寸: {controlWidth:F1}x{controlHeight:F1}");
        }

        private bool CheckCollision(Control movingControl, double newX, double newY)
        {
            if (DragCanvas == null) return false;

            // 获取移动工件的边界
            Rect movingBounds = new Rect(newX, newY, movingControl.Width, movingControl.Height);

            // 首先检查边界碰撞
            if (CheckBoundaryCollision(movingBounds))
            {
                Console.WriteLine($"边界碰撞: 移动工件 {movingControl.Name} 超出边界");
                return true;
            }

            foreach (Control otherControl in DragCanvas.Children)
            {
                if (otherControl == movingControl) continue;

                // 获取其他工件的边界
                Rect otherBounds = new Rect(
                    otherControl.GetValue(Canvas.LeftProperty),
                    otherControl.GetValue(Canvas.TopProperty),
                    otherControl.Width,
                    otherControl.Height
                );

                // 使用精确的碰撞检测
                if (movingBounds.Intersects(otherBounds))
                {
                    ShowCollisionFeedback(otherControl);

                    // 调试信息
                    Console.WriteLine($"碰撞检测: 移动工件 {movingControl.Name} 与 {otherControl.Name} 发生碰撞");
                    Console.WriteLine($"移动边界: {movingBounds}, 其他边界: {otherBounds}");

                    return true;
                }
            }

            return false;
        }

        // 检查边界碰撞
        private bool CheckBoundaryCollision(Rect bounds)
        {
            return bounds.Left < 0 ||
                   bounds.Top < 0 ||
                   bounds.Right > _platformWidth ||
                   bounds.Bottom > _platformHeight;
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

                if (!CheckBoundary(xCount, yCount, xMargin, yMargin))
                {
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

        private bool CheckBoundary(int xCount, int yCount, double xMargin, double yMargin)
        {
            if (xCount <= 0 && yCount <= 0) return true;

            double totalBlockWidth = _blockWidth + 2 * xMargin;
            double totalBlockHeight = _blockHeight + 2 * yMargin;

            if (xCount > 0)
            {
                double requiredWidth = xCount * totalBlockWidth;
                if (requiredWidth > _platformWidth)
                {
                    ShowDetailedWarningMessage(xCount, yCount, xMargin, yMargin);
                    return false;
                }
            }
            else if (yCount > 0)
            {
                double requiredHeight = yCount * totalBlockHeight;
                if (requiredHeight > _platformHeight)
                {
                    ShowDetailedWarningMessage(xCount, yCount, xMargin, yMargin);
                    return false;
                }
            }

            return true;
        }

        private int CalculateMaxXCount(double xMargin, double yMargin)
        {
            double totalBlockWidth = _blockWidth + 2 * xMargin;
            int maxCount = (int)Math.Floor(_platformWidth / totalBlockWidth);
            return Math.Max(0, maxCount);
        }

        private int CalculateMaxYCount(double xMargin, double yMargin)
        {
            double totalBlockHeight = _blockHeight + 2 * yMargin;
            int maxCount = (int)Math.Floor(_platformHeight / totalBlockHeight);
            return Math.Max(0, maxCount);
        }

        private void AddWorkpieces(int xCount, int yCount, double xMargin, double yMargin)
        {
            if (DragCanvas == null || OuterContainerBorder == null) return;

            double innerBlockWidth = _blockWidth;
            double innerBlockHeight = _blockHeight;

            double totalWidth = innerBlockWidth + 2 * xMargin;
            double totalHeight = innerBlockHeight + 2 * yMargin;

            if (xCount > 0)
            {
                AddWorkpiecesInXDirection(xCount, totalWidth, totalHeight, innerBlockWidth, innerBlockHeight, xMargin,
                    yMargin);
            }
            else if (yCount > 0)
            {
                AddWorkpiecesInYDirection(yCount, totalWidth, totalHeight, innerBlockWidth, innerBlockHeight, xMargin,
                    yMargin);
            }

            Console.WriteLine($"添加了 {xCount + yCount} 个工件");
            ShowTemporaryMessage($"成功添加 {xCount + yCount} 个工件");
        }

        private void AddWorkpiecesInXDirection(int count, double totalWidth, double totalHeight,
            double innerWidth, double innerHeight, double xMargin, double yMargin)
        {
            double startX = _currentXPosition;
            double startY = _currentYPosition;

            for (int i = 0; i < count; i++)
            {
                double posX = startX;
                double posY = startY + i * totalHeight;

                // 检查是否需要换行
                if (posY + totalHeight > _platformHeight)
                {
                    startX += totalWidth;
                    startY = 0;
                    posX = startX;
                    posY = 0;

                    // 检查是否超出托盘宽度
                    if (posX + totalWidth > _platformWidth)
                    {
                        ShowWarningMessage("无法添加更多工件，托盘已满");
                        break;
                    }
                }

                // 确保位置在边界内
                posX = Math.Max(0, Math.Min(posX, _platformWidth - totalWidth));
                posY = Math.Max(0, Math.Min(posY, _platformHeight - totalHeight));

                CreateWorkpiece(_currentBlockNumber, posX, posY, innerWidth, innerHeight, xMargin, yMargin);
                _currentBlockNumber++;

                // 更新当前位置
                _currentXPosition = posX;
                _currentYPosition = posY + totalHeight;

                Console.WriteLine($"添加X方向工件 {_currentBlockNumber - 1} 到位置: ({posX}, {posY})");
            }
        }

        private void AddWorkpiecesInYDirection(int count, double totalWidth, double totalHeight,
            double innerWidth, double innerHeight, double xMargin, double yMargin)
        {
            double startX = _currentXPosition;
            double startY = _currentYPosition;

            for (int i = 0; i < count; i++)
            {
                double posX = startX + i * totalWidth;
                double posY = startY;

                // 检查是否需要换列
                if (posX + totalWidth > _platformWidth)
                {
                    startX = 0;
                    startY += totalHeight;
                    posX = 0;
                    posY = startY;

                    // 检查是否超出托盘高度
                    if (posY + totalHeight > _platformHeight)
                    {
                        ShowWarningMessage("无法添加更多工件，托盘已满");
                        break;
                    }
                }

                // 确保位置在边界内
                posX = Math.Max(0, Math.Min(posX, _platformWidth - totalWidth));
                posY = Math.Max(0, Math.Min(posY, _platformHeight - totalHeight));

                CreateWorkpiece(_currentBlockNumber, posX, posY, innerWidth, innerHeight, xMargin, yMargin);
                _currentBlockNumber++;

                // 更新当前位置
                _currentXPosition = posX + totalWidth;
                _currentYPosition = posY;

                Console.WriteLine($"添加Y方向工件 {_currentBlockNumber - 1} 到位置: ({posX}, {posY})");
            }
        }

        private void CreateWorkpiece(int blockNumber, double posX, double posY, double innerWidth, double innerHeight,
            double xMargin, double yMargin)
        {
            // 创建内部方块（默认粉色）
            var innerBlock = new Border
            {
                Width = innerWidth, // 初始宽度 = 块宽度
                Height = innerHeight, // 初始高度 = 块高度
                Background = Brushes.Pink, // 默认粉色
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

            // 初始化文字旋转为0度
            textBlock.RenderTransform = new RotateTransform(0);
            textBlock.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

            innerBlock.Child = textBlock;

            // 创建外部边框
            var outerBorder = new Border
            {
                Name = $"Workpiece{blockNumber}",
                Width = innerWidth + 2 * xMargin,
                Height = innerHeight + 2 * yMargin,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Blue, // 蓝色边框
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(0),
                Cursor = new Cursor(StandardCursorType.SizeAll)
            };

            var container = new Grid
            {
                Width = innerWidth + 2 * xMargin,
                Height = innerHeight + 2 * yMargin,
                Background = Brushes.Transparent
            };

            innerBlock.HorizontalAlignment = HorizontalAlignment.Center;
            innerBlock.VerticalAlignment = VerticalAlignment.Center;
            innerBlock.Margin = new Thickness(0);

            container.Children.Add(innerBlock);
            outerBorder.Child = container;

            Canvas.SetLeft(outerBorder, posX);
            Canvas.SetTop(outerBorder, posY);

            // 初始化旋转角度为0
            _workpieceRotations[outerBorder.Name] = 0;

            AddDragEvents(outerBorder);

            outerBorder.PointerPressed += (sender, e) =>
            {
                if (e.GetCurrentPoint(outerBorder).Properties.IsLeftButtonPressed)
                {
                    SelectWorkpiece(outerBorder);
                    e.Handled = true;
                }
            };

            DragCanvas!.Children.Add(outerBorder);

            Console.WriteLine(
                $"创建工件: Workpiece{blockNumber}, 位置: ({posX}, {posY}), 尺寸: {outerBorder.Width}x{outerBorder.Height}");
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

        #endregion

        #region 工具方法

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

        #region 弹窗警告功能

        private void ShowWarningMessage(string message)
        {
            var dialogWindow = new Window
            {
                Title = "警告",
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
            };

            var stackPanel = new StackPanel
            {
                Spacing = 20,
                Margin = new Thickness(20),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

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

            if (VisualRoot is Window parentWindow)
            {
                dialogWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                dialogWindow.ShowDialog(parentWindow);
            }
            else
            {
                dialogWindow.Show();
            }
        }

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
                message =
                    $"❌ X轴工件数量超出托盘边界！\n\n当前设置：{xCount}个工件\n所需宽度：{requiredWidth:F1}mm\n托盘宽度：{_platformWidth:F1}mm\n单个工件：{totalBlockWidth:F1}mm\n\n💡 建议：最多可添加 {maxXCount} 个Y轴工件";
            }
            else
            {
                double requiredHeight = yCount * totalBlockHeight;
                message =
                    $"❌ Y轴工件数量超出托盘边界！\n\n当前设置：{yCount}个工件\n所需高度：{requiredHeight:F1}mm\n托盘高度：{_platformHeight:F1}mm\n单个工件：{totalBlockHeight:F1}mm\n\n💡 建议：最多可添加 {maxYCount} 个X轴工件";
            }

            ShowWarningMessage(message);
        }

        private void ShowTemporaryMessage(string message)
        {
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

            if (this.FindControl<Grid>("MainGrid") is Grid mainGrid)
            {
                var existingWarning =
                    mainGrid.Children.FirstOrDefault(c => c is Border b && b.Background?.ToString() == "#FFFFFF00");
                if (existingWarning != null)
                {
                    mainGrid.Children.Remove(existingWarning);
                }

                mainGrid.Children.Add(warningPanel);

                DispatcherTimer.RunOnce(() => { mainGrid.Children.Remove(warningPanel); }, TimeSpan.FromSeconds(3));
            }
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
                    string name = border.Name ?? "";
                    double rotation = _workpieceRotations.ContainsKey(name) ? _workpieceRotations[name] : 0;

                    state.Elements.Add(new ElementState
                    {
                        Name = name,
                        Left = child.GetValue(Canvas.LeftProperty),
                        Top = child.GetValue(Canvas.TopProperty),
                        Rotation = rotation,
                        Width = child.Width,
                        Height = child.Height
                    });
                }
            }

            _undoStack.Push(state);
            _redoStack.Clear();
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
            ClearAllWorkpieces();
        }

        #endregion

        #region 公共方法

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
        public double Rotation { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }
}