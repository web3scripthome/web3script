using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Collections.ObjectModel;

namespace web3script.Models
{
    /// <summary>
    /// 报表数据基类，定义通用属性和方法
    /// </summary>
    public abstract class BaseReportData : INotifyPropertyChanged
    {
        private string _projectName;
        private string _walletAddress;
        private int _interactionCount;
        private int _successCount;
        private int _failedCount;
        private decimal _currentBalance;
        private decimal _gMonTokens;
        private decimal _sMonTokens;
        private string _interactionType;
        private Dictionary<string, decimal> _taskBalances;
        private DateTime? _lastBalanceUpdate;
        private bool _isMonadReport;

        /// <summary>
        /// 项目名称
        /// </summary>
        public string ProjectName
        {
            get => _projectName;
            set
            {
                if (_projectName != value)
                {
                    _projectName = value;
                    OnPropertyChanged(nameof(ProjectName));
                }
            }
        }

        /// <summary>
        /// 钱包地址
        /// </summary>
        public string WalletAddress
        {
            get => _walletAddress;
            set
            {
                if (_walletAddress != value)
                {
                    _walletAddress = value;
                    OnPropertyChanged(nameof(WalletAddress));
                }
            }
        }

        /// <summary>
        /// 交互次数
        /// </summary>
        public int InteractionCount
        {
            get => _interactionCount;
            set
            {
                if (_interactionCount != value)
                {
                    _interactionCount = value;
                    OnPropertyChanged(nameof(InteractionCount));
                }
            }
        }

        /// <summary>
        /// 成功次数
        /// </summary>
        public int SuccessCount
        {
            get => _successCount;
            set
            {
                if (_successCount != value)
                {
                    _successCount = value;
                    OnPropertyChanged(nameof(SuccessCount));
                }
            }
        }

        /// <summary>
        /// 失败次数
        /// </summary>
        public int FailedCount
        {
            get => _failedCount;
            set
            {
                if (_failedCount != value)
                {
                    _failedCount = value;
                    OnPropertyChanged(nameof(FailedCount));
                }
            }
        }

        /// <summary>
        /// 当前余额
        /// </summary>
        public decimal CurrentBalance
        {
            get => _currentBalance;
            set
            {
                if (_currentBalance != value)
                {
                    _currentBalance = value;
                    OnPropertyChanged(nameof(CurrentBalance));
                }
            }
        }

        /// <summary>
        /// 获取的gMon代币数量
        /// </summary>
        public decimal GMonTokens
        {
            get => _gMonTokens;
            set
            {
                if (_gMonTokens != value)
                {
                    _gMonTokens = value;
                    OnPropertyChanged(nameof(GMonTokens));
                }
            }
        }
        
        /// <summary>
        /// 获取的sMon代币数量
        /// </summary>
        public decimal SMonTokens
        {
            get => _sMonTokens;
            set
            {
                if (_sMonTokens != value)
                {
                    _sMonTokens = value;
                    OnPropertyChanged(nameof(SMonTokens));
                }
            }
        }
        
        /// <summary>
        /// 交互类型，如"gMon"或"sMon"等
        /// </summary>
        public string InteractionType
        {
            get => _interactionType;
            set
            {
                if (_interactionType != value)
                {
                    _interactionType = value;
                    OnPropertyChanged(nameof(InteractionType));
                }
            }
        }

        /// <summary>
        /// 任务余额字典
        /// </summary>
        public Dictionary<string, decimal> TaskBalances
        {
            get => _taskBalances;
            set
            {
                if (_taskBalances != value)
                {
                    _taskBalances = value;
                    OnPropertyChanged(nameof(TaskBalances));
                }
            }
        }

        /// <summary>
        /// 记录最后刷新余额的时间
        /// </summary>
        public DateTime? LastBalanceUpdate
        {
            get => _lastBalanceUpdate;
            set
            {
                if (_lastBalanceUpdate != value)
                {
                    _lastBalanceUpdate = value;
                    OnPropertyChanged(nameof(LastBalanceUpdate));
                }
            }
        }

        /// <summary>
        /// 用于模型内部区分不同项目类型
        /// </summary>
        public bool IsMonadReport
        {
            get => _isMonadReport;
            set
            {
                if (_isMonadReport != value)
                {
                    _isMonadReport = value;
                    OnPropertyChanged(nameof(IsMonadReport));
                }
            }
        }

        /// <summary>
        /// 获取展示详情所需的列名和对应值
        /// </summary>
        /// <returns>列名和值的键值对</returns>
        public abstract Dictionary<string, string> GetDisplayDetails();

        /// <summary>
        /// 获取项目的展示数据
        /// </summary>
        /// <returns>展示数据项的键值对列表</returns>
        public Dictionary<string, string> GetBasicDisplayData()
        {
            var data = new Dictionary<string, string>
            {
                { "项目名称", ProjectName },
                { "钱包地址", WalletAddress },
                { "交互次数", InteractionCount.ToString() },
                { "成功次数", SuccessCount.ToString() },
                { "失败次数", FailedCount.ToString() },
                { "当前余额", $"{CurrentBalance:F4}" }
            };

            return data;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Monad项目报表数据
    /// </summary>
    public class MonadReportData : BaseReportData
    {
        public override Dictionary<string, string> GetDisplayDetails()
        {
            var data = GetBasicDisplayData();
            
            // 添加Monad特有的数据
            data.Add("gMon代币数量", $"{GMonTokens:F6}");
            data.Add("sMon代币数量", $"{SMonTokens:F6}");
            data.Add("交互类型", InteractionType ?? "未指定");
            
            return data;
        }
    }

    /// <summary>
    /// 以太坊项目报表数据示例
    /// </summary>
    public class EthereumReportData : BaseReportData
    {
        private decimal _ethGas;
        private int _txCount;

        /// <summary>
        /// 消耗的ETH Gas
        /// </summary>
        public decimal EthGas
        {
            get => _ethGas;
            set
            {
                if (_ethGas != value)
                {
                    _ethGas = value;
                    OnPropertyChanged(nameof(EthGas));
                }
            }
        }

        /// <summary>
        /// 交易数量
        /// </summary>
        public int TxCount
        {
            get => _txCount;
            set
            {
                if (_txCount != value)
                {
                    _txCount = value;
                    OnPropertyChanged(nameof(TxCount));
                }
            }
        }

        public override Dictionary<string, string> GetDisplayDetails()
        {
            var data = GetBasicDisplayData();
            
            // 添加Ethereum特有的数据
            data.Add("消耗Gas", $"{EthGas:F8} ETH");
            data.Add("交易数量", TxCount.ToString());
            
            return data;
        }
    }

    /// <summary>
    /// 默认报表数据，用于未特别指定的项目
    /// </summary>
    public class DefaultReportData : BaseReportData
    {
        public override Dictionary<string, string> GetDisplayDetails()
        {
            return GetBasicDisplayData();
        }
    }

    /// <summary>
    /// 报表数据工厂，用于创建不同项目的报表数据
    /// </summary>
    public static class ReportDataFactory
    {
        /// <summary>
        /// 根据项目名称创建对应的报表数据对象
        /// </summary>
        public static BaseReportData CreateReportData(string projectName)
        {
            switch (projectName.ToLower())
            {
                case "monad":
                    return new MonadReportData();
                case "ethereum":
                    return new EthereumReportData();
                default:
                    return new DefaultReportData();
            }
        }
    }
} 