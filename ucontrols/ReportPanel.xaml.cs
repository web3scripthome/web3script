using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using web3script.Models;
using web3script.ViewModels;
using System.Windows.Data;
using System.Diagnostics;

namespace web3script.ucontrols
{
    /// <summary>
    /// ReportPanel.xaml 的交互逻辑
    /// </summary>
    public partial class ReportPanel : UserControl
    {
        private ReportViewModel _viewModel;
        private TextBlock _tokenBalanceHeader;

        public ReportPanel()
        {
            InitializeComponent();
            
            // 初始化ViewModel并设置为DataContext
            _viewModel = new ReportViewModel();
            DataContext = _viewModel;
            
            // 等待UI加载完成后设置列标题和事件处理
            Loaded += (s, e) => 
            {
                // 获取代币余额列的标题TextBlock
                _tokenBalanceHeader = ReportDataGrid.Columns[6].Header as TextBlock;
                
                // 监听交互类型变化
                _viewModel.PropertyChanged += (sender, args) => 
                {
                    if (args.PropertyName == nameof(ReportViewModel.SelectedInteractionType))
                    {
                        UpdateTokenBalanceColumnHeader();
                    }
                };
                
                // 初始设置列标题
                UpdateTokenBalanceColumnHeader();
            };
            
            // 添加面板可见性变更事件处理
            this.IsVisibleChanged += ReportPanel_IsVisibleChanged;
        }

        // 当面板变为可见时刷新数据
        private void ReportPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue == true && _viewModel != null)
            {
                Debug.WriteLine("ReportPanel变为可见状态，刷新交互类型列表");
                RefreshInteractionTypes();
            }
        }
        
        // 刷新交互类型列表
        private void RefreshInteractionTypes()
        {
            try
            {
                // 调用ViewModel的方法重新加载交互类型
                _viewModel.ReloadInteractionTypes();
                Debug.WriteLine($"已刷新交互类型列表，现有 {_viewModel.InteractionTypes.Count} 个选项");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"刷新交互类型列表时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新代币余额列的标题，使用交互类型中的代币名称
        /// </summary>
        private void UpdateTokenBalanceColumnHeader()
        {
            if (_tokenBalanceHeader != null && _viewModel.SelectedInteractionType != null)
            {
                string tokenName = ExtractTokenName(_viewModel.SelectedInteractionType);
                
                if (!string.IsNullOrEmpty(tokenName))
                {
                    _tokenBalanceHeader.Text = $"{tokenName}";
                }
                else
                {
                    _tokenBalanceHeader.Text = "代币余额";
                }
            }
        }
        
        /// <summary>
        /// 从交互任务名称中提取代币名称（括号内的内容）
        /// </summary>
        private string ExtractTokenName(string interactionType)
        {
            if (string.IsNullOrEmpty(interactionType) || interactionType == "全部")
                return null;
                
            // 查找左括号和右括号的位置
            int startIndex = interactionType.IndexOf('(');
            int endIndex = interactionType.IndexOf(')', startIndex);
            
            // 如果找到了括号并且位置有效
            if (startIndex >= 0 && endIndex > startIndex)
            {
                // 提取括号内的内容
                string tokenName = interactionType.Substring(startIndex + 1, endIndex - startIndex - 1);
                return !string.IsNullOrWhiteSpace(tokenName) ? tokenName : null;
            }
            
            return null;
        }

        /// <summary>
        /// 刷新按钮点击事件
        /// </summary>
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                // 先刷新交互类型列表
                RefreshInteractionTypes();
                
                // 输出当前选择的筛选条件
                Debug.WriteLine($"DEBUG-UI - 点击刷新按钮，当前选择: 项目={_viewModel.SelectedProject}, 分组={_viewModel.SelectedGroup}, 交互类型={_viewModel.SelectedInteractionType}");
                
                // 确保选择的交互类型正确
                if (_viewModel.SelectedInteractionType == null)
                {
                    Debug.WriteLine("DEBUG-UI - 警告: 选择的交互类型为空，设置为'全部'");
                    _viewModel.SelectedInteractionType = "全部";
                }
                
                // 执行刷新命令
                if (_viewModel.RefreshCommand.CanExecute(null))
                {
                    Debug.WriteLine("DEBUG-UI - 执行刷新命令");
                    _viewModel.RefreshCommand.Execute(null);
                }
                else
                {
                    Debug.WriteLine("DEBUG-UI - 刷新命令无法执行，可能是因为当前正在加载中");
                }
                
                // 输出刷新后的数据状态
                Debug.WriteLine($"DEBUG-UI - 刷新完成，数据项数: {_viewModel.ReportData.Count}");
            }
        }
    }
}
