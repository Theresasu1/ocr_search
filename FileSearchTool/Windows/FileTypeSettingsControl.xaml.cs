using System.Windows.Controls;
using FileSearchTool.Services;
using WPFUserControl = System.Windows.Controls.UserControl;

namespace FileSearchTool.Windows
{
    public partial class FileTypeSettingsControl : WPFUserControl
    {
        public FileTypeSettingsControl(ScheduledIndexingService scheduledIndexingService)
        {
            InitializeComponent();
            
            // 将现有的FileTypeSettingsWindow的内容嵌入到这里
            var window = new FileTypeSettingsWindow(scheduledIndexingService);
            Content = window.Content;
        }
    }
}
