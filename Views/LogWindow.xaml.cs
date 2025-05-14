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
            LogTextBox.Document.Blocks.Add(new Paragraph(new Run(message)));
            LogTextBox.ScrollToEnd();
        }

        public void ClearLogs()
        {
            LogTextBox.Document.Blocks.Clear();
        }
    }
} 
