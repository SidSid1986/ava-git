using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ava_demo_new.Views;

public partial class CodeEdit : UserControl
{
    public CodeEdit()
    {
        InitializeComponent();
        
        
    }
    // 添加 OriginalContent 属性
    public object? OriginalContent { get; set; }
    
    private void BackButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (this.Parent is ContentControl contentControl && OriginalContent != null)
        {
            contentControl.Content = OriginalContent;
        }
    }
    
        // 复制功能
    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(CodeEditor.SelectedText))
        {
            CodeEditor.Copy();
            Console.WriteLine("已复制选中文本");
        }
        else
        {
            Console.WriteLine("没有选中文本");
        }
    }

    // 剪切功能
    private void CutButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(CodeEditor.SelectedText))
        {
            CodeEditor.Cut();
            Console.WriteLine("已剪切选中文本");
        }
        else
        {
            Console.WriteLine("没有选中文本");
        }
    }

    // 粘贴功能
    private async void PasteButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 获取剪贴板内容
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard == null) return;
        
            var text = await clipboard.GetTextAsync();
            if (string.IsNullOrEmpty(text))
            {
                Console.WriteLine("剪贴板为空或不含文本");
                return;
            }

            // 确保编辑器获得焦点
            CodeEditor.Focus();

            // 执行粘贴操作
            CodeEditor.Paste();

            // 手动触发光标显示
            CodeEditor.TextArea.Caret.Show();
            CodeEditor.TextArea.Caret.BringCaretToView();

            Console.WriteLine($"已粘贴 {text.Length} 个字符");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"粘贴失败: {ex.Message}");
        }
    }
 
    // 删除功能 - 模拟标准文本编辑器的删除行为
   
    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!string.IsNullOrEmpty(CodeEditor.SelectedText))
            {
                // 情况1：有选中文本 - 删除选中内容
                var selectionLength = CodeEditor.SelectedText.Length;
                CodeEditor.SelectedText = "";
                Console.WriteLine($"已删除选中文本 ({selectionLength} 个字符)");
            }
            else if (CodeEditor.CaretOffset > 0)
            {
                // 情况2：没有选中文本 - 删除光标前的字符
                // 保存当前光标位置
                var currentCaretOffset = CodeEditor.CaretOffset;
            
                // 删除光标前的字符
                CodeEditor.Document.Remove(CodeEditor.CaretOffset - 1, 1);
            
                // 光标自动前移一位
                CodeEditor.CaretOffset = currentCaretOffset - 1;
            
                Console.WriteLine("已删除光标前字符 (Backspace)");
            }
            else
            {
                Console.WriteLine("已在文本开头，无法删除");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"删除操作失败: {ex.Message}");
        }
    }

    // 撤销功能
    private void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        if (CodeEditor.CanUndo)
        {
            CodeEditor.Undo();
            Console.WriteLine("已撤销");
        }
        else
        {
            Console.WriteLine("无法撤销");
        }
    }

    // 重做功能
    private void RedoButton_Click(object sender, RoutedEventArgs e)
    {
        if (CodeEditor.CanRedo)
        {
            CodeEditor.Redo();
            Console.WriteLine("已重做");
        }
        else
        {
            Console.WriteLine("无法重做");
        }
    }
}