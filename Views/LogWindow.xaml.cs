using System.Windows;
using System.Windows.Documents;

namespace web3script.Views
{
    public partial class LogWindow : Window
    {
        public LogWindow()
        {
            InitializeComponent();
        }

        public void AppendLog(string message)
        {
            var document = LogTextBox.Document;
            var paragraphs = document.Blocks.OfType<Paragraph>().ToList();

            // 如果超过100行则清空
            if (paragraphs.Count >= 100)
            {
                document.Blocks.Clear();
            }

            // 添加新日志行
            document.Blocks.Add(new Paragraph(new Run(message)));

            // 滚动到末尾
            LogTextBox.ScrollToEnd();
        }

        public void ClearLogs()
        {
            LogTextBox.Document.Blocks.Clear();
        }
    }
} 