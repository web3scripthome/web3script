using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using web3script.Models;
using web3script.Services;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Hex.HexTypes;
using Nethereum.Util;
using NBitcoin;
using Task = System.Threading.Tasks.Task;
using Nethereum.RPC.Eth.DTOs;
using System.Numerics;
using System.Collections.ObjectModel;
using System.ComponentModel;
using web3script.ContractScript;
using web3script.ConScript;
using Ctrans;
using System.IO;
using System.Diagnostics; // 添加引用MonadStaking

namespace web3script.ucontrols
{
    /// <summary>
    /// BatchTransferDialog.xaml 的交互逻辑
    /// </summary>
    public partial class contractBatchTransferDialog : Window
    {
        private WalletService _walletService;
        private ProjectService _projectService;
        private List<Wallet> _targetWallets = new List<Wallet>();
        private Account _masterAccount;
        private bool _isTransferring = false;
        private decimal _transferAmount = 0;
        private List<TransferResult> _transferResults = new List<TransferResult>();
        private string _currentCurrencyUnit = " ";
        public string rpcUrl = "https://testnet-rpc.monad.xyz";
        
        // 添加钱包余额列表
        private ObservableCollection<WalletBalanceItem> _walletBalanceItems;
        
        // 用于更新UI的委托
        private delegate void UpdateUIDelegate(string message);
        private delegate void UpdateProgressDelegate(int current, int total);
        private delegate void TransferCompletedDelegate(List<TransferResult> results);
        
        public contractBatchTransferDialog(WalletService walletService)
        {
            InitializeComponent();
            
            _walletService = walletService;
            _projectService = new ProjectService();
            
            // 加载分组
            LoadGroups();
            
            // 加载项目
            LoadProjects();
            
            // 初始化钱包余额列表
            _walletBalanceItems = new ObservableCollection<WalletBalanceItem>();
            walletBalanceList.ItemsSource = _walletBalanceItems;
            
            // 初始化币种单位和关联文本框变更事件
            // 注意：在LoadProjects中已添加Project_SelectionChanged事件
            txtAmount.TextChanged += txtAmount_TextChanged;
            
            // 首次初始化币种单位
            Dispatcher.InvokeAsync(() => {
                UpdateCurrencyUnit();
            });
        }
        
        private void LoadGroups()
        {
            try
            {
                cmbTargetGroup.Items.Clear();
                
                foreach (var group in _walletService.GetGroups())
                {
                    int walletCount = _walletService.GetWalletsInGroup(group.Id).Count;
                    ComboBoxItem item = new ComboBoxItem
                    {
                        Content = $"{group.Name} ({walletCount})",
                        Tag = group.Id
                    };
                    cmbTargetGroup.Items.Add(item);
                }
                
                if (cmbTargetGroup.Items.Count > 0)
                {
                    cmbTargetGroup.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载钱包分组失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void LoadProjects()
        {
            try
            {
                cmbProject.Items.Clear();
                
                foreach (var project in _projectService.GetProjects())
                {
                    ComboBoxItem item = new ComboBoxItem
                    {
                        Content = project.Name,
                        Tag = project.Id
                    };
                    cmbProject.Items.Add(item);
                }
                
                if (cmbProject.Items.Count > 0)
                {
                    cmbProject.SelectedIndex = 0;
                }
                
                // 添加项目选择变更事件
                cmbProject.SelectionChanged += Project_SelectionChanged;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载项目失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void TargetGroup_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateWalletCount();
            UpdateTotalAmount();
        }
        
        private void UpdateWalletCount()
        {
            if (cmbTargetGroup.SelectedItem == null) return;
            
            string groupId = (cmbTargetGroup.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (string.IsNullOrEmpty(groupId)) return;
            
            _targetWallets = _walletService.GetWalletsInGroup(groupId);
            lblWalletCount.Text = _targetWallets.Count.ToString();
        }
        
        private void UpdateTotalAmount()
        {
            if (!decimal.TryParse(txtAmount.Text, out _transferAmount))
            {
                _transferAmount = 0;
            }
            
            decimal totalAmount = _transferAmount * _targetWallets.Count;
            lblTotalAmount.Text = $"{totalAmount:F6} {_currentCurrencyUnit}";
            
            // 更新gas预估
           
        }
        
        private async Task UpdateGasEstimateAsync()
        {
            try
            {
                if (_targetWallets.Count == 0)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        lblGasEstimate.Text = "0 ETH";
                    });
                    return;
                }
                
                // 预估每笔交易的gas为21000，gas价格为50 Gwei
                BigInteger gasPerTx = new BigInteger(21000);
                
                // 确保在后台线程执行网络操作
                Account account = new Nethereum.Web3.Accounts.Account(_privatekey);
                var web3 = new Web3(account, rpcUrl);

                BigInteger gasPriceWei = await web3.Eth.GasPrice.SendRequestAsync(); // 单位是 Wei
                BigInteger gasLimit = 21000; // 示例 Gas 估算（普通转账）

                BigInteger totalGasWei = gasPriceWei * gasLimit; // 总的 Wei 花费
                decimal totalGasEth = UnitConversion.Convert.FromWei(totalGasWei);

                var AllGas = totalGasEth * _targetWallets.Count;
                
                // 在UI线程更新UI
                await Dispatcher.InvokeAsync(() =>
                {
                    lblGasEstimate.Text = $"约 {AllGas:F6} ETH";
                });
            }
            catch (Exception ex)
            {
                // 在UI线程处理异常
                await Dispatcher.InvokeAsync(() =>
                {
                    lblGasEstimate.Text = "估算失败";
                    AppendLog($"Gas估算失败: {ex.Message}");
                });
            }
        }
        
        private void Amount_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 只允许输入数字和小数点
            Regex regex = new Regex("[^0-9.]+");
            e.Handled = regex.IsMatch(e.Text);
            
            // 确保只有一个小数点
            if (e.Text == "." && ((TextBox)sender).Text.Contains("."))
            {
                e.Handled = true;
            }
        }
        
        private void txtAmount_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateTotalAmount();
        } 
        
        public string _privatekey = "";
        private void ValidateWallet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string keyInput = txtMasterKey.Password;
                
                if (string.IsNullOrEmpty(keyInput))
                {
                    MessageBox.Show("请输入主钱包的助记词或私钥", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 尝试解析助记词或私钥
                _masterAccount = null;
                string address = null;
                
                // 检查是否是助记词
                if (IsMnemonic(keyInput))
                {
                    var mnemonic = new Mnemonic(keyInput);
                    var hdRoot = mnemonic.DeriveExtKey();
                    var extKey = hdRoot.Derive(new NBitcoin.KeyPath("m/44'/60'/0'/0/0")); 
                    var privateKey = new Nethereum.Signer.EthECKey(extKey.PrivateKey.ToBytes(), true); 
                    _masterAccount = new Account(privateKey);
                    _privatekey = privateKey.GetPrivateKey();
                    address = _masterAccount.Address;
                }
                // 检查是否是私钥
                else if (IsPrivateKey(keyInput))
                {
                    // 确保私钥格式正确
                    if (!keyInput.StartsWith("0x"))
                    {
                        keyInput = "0x" + keyInput;
                    }
                    _privatekey = keyInput;
                    _masterAccount = new Account(keyInput);
                    address = _masterAccount.Address;
                }
                else
                {
                    MessageBox.Show("无效的助记词或私钥格式", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // 显示地址
                txtMasterAddress.Text = address;
                
                // 验证成功，启用转账按钮
                btnTransfer.IsEnabled = true;
                
                // 添加到日志
                AppendLog($"主钱包验证成功，地址: {address}");
                UpdateTotalAmount();
              //  UpdateGasEstimateAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"验证钱包失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                btnTransfer.IsEnabled = false;
                txtMasterAddress.Text = string.Empty;
            }
        }
        
        private bool IsMnemonic(string input)
        {
            string[] words = input.Split(new[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            return (words.Length == 12 || words.Length == 15 || words.Length == 18 || 
                    words.Length == 21 || words.Length == 24);
        }
        
        private bool IsPrivateKey(string input)
        {
            // 移除可能的0x前缀
            if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                input = input.Substring(2);
            }
            
            // 检查是否是64个十六进制字符
            return input.Length == 64 && input.All(c => "0123456789abcdefABCDEF".Contains(c));
        }
        
        private void StartTransfer_Click(object sender, RoutedEventArgs e)
        {
            if (_isTransferring)
            {
                MessageBox.Show("已有转账正在进行，请等待完成", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            if (_masterAccount == null)
            {
                MessageBox.Show("请先验证主钱包", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            if (_targetWallets.Count == 0)
            {
                MessageBox.Show("所选分组没有钱包", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            if (!decimal.TryParse(txtAmount.Text, out _transferAmount) || _transferAmount <= 0)
            {
                MessageBox.Show("请输入有效的转账金额", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            // 计算所需的总金额（包括转账金额和预估的gas费）
            decimal gasPerTx = 21000m;
            decimal gasPriceGwei = 50m;
            decimal totalGasEth = (_targetWallets.Count * gasPerTx * gasPriceGwei) / 1_000_000_000m;
            decimal totalAmount = (_transferAmount * _targetWallets.Count) + totalGasEth;
            
            // 确认转账
            if (MessageBox.Show(
                $"确认要从主钱包向 {_targetWallets.Count} 个钱包转账吗？\n\n" +
                $"每个钱包: {_transferAmount:F6} {_currentCurrencyUnit}\n",
                "确认转账",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }
            
            // 开始转账
            StartTransferProcess();
        }
        
        private async void StartTransferProcess()
        {
            try
            {
                _isTransferring = true;
                
                // 设置UI状态
                btnValidate.IsEnabled = false;
                btnTransfer.IsEnabled = false;
                btnCancel.Content = "关闭";
                cmbTargetGroup.IsEnabled = false;
                txtAmount.IsEnabled = false;
                cmbProject.IsEnabled = false;
                txtMasterKey.IsEnabled = false;
                
                // 清空日志
                txtTransferLog.Clear();
                _transferResults.Clear();
                
                // 设置进度条初始状态
                transferProgress.Value = 0;
                lblProgressPercent.Text = "0%";
                lblCurrentOperation.Text = "准备转账...";

                string projectName = "";


                await Dispatcher.InvokeAsync(() => {
                    ComboBoxItem selectedProjectItem = cmbProject.SelectedItem as ComboBoxItem;
                    projectName = selectedProjectItem?.Content?.ToString() ?? string.Empty;
                });

                // 根据不同项目执行不同的转账逻辑
                string transactionHash = string.Empty;

                switch (projectName)
                {
                    case "Monad":
                        await Task.Run(() => TransferTask());
                        break;
                    case "PharosNetwork":
                        await Task.Run(() => PharosTransferTask()); 
                        break;
                    default:
                        break;
                }




                // 开始转账任务
               
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动转账过程失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                _isTransferring = false;
                
                // 恢复UI状态
                btnValidate.IsEnabled = true;
                btnTransfer.IsEnabled = true;
                btnCancel.Content = "取消";
                cmbTargetGroup.IsEnabled = true;
                txtAmount.IsEnabled = true;
                cmbProject.IsEnabled = true;
                txtMasterKey.IsEnabled = true;
            }
        }
        
        private async void TransferTask()
        { 
            int total = _targetWallets.Count;
            int current = 0;
            
            // 先清空并初始化钱包余额列表
            await Dispatcher.InvokeAsync(() => {
                InitializeWalletBalanceList();
            });
            AppendLog($"正在生成{total}临时钱包...");

          //  MessageBox.Show($"正在生成{total+1}临时钱包...");
            var toAddresses = WalletService.tempGenerateWallets(total); // 生成临时钱包 
            List<Wallet> tempWallets = new List<Wallet>(); // 存储临时钱包>
            Wallet tempWalletAddress = toAddresses.Last();
            _transferAmount = _transferAmount + 0.006m;
            foreach (var item in toAddresses)
            {
                if (item.Address == tempWalletAddress.Address)
                {
                    var transre = _transferAmount+0.1m;
                    AppendLog($"正在向临时中继合约创建钱包地址: {item.Address}转帐...{transre}");
                    var resultRe =    await  contractTransferToWallet(item.Address, transre);
                    if (resultRe.IsSuccess)
                    {
                        AppendLog($"向临时中继合约创建钱包地址: {item.Address}转帐完成...{transre}");
                        File.AppendAllText("log.txt", $"向临时中继合约创建钱包地址: {item.Address},{item.PrivateKey}转帐完成...{transre}" + Environment.NewLine);
                        tempWallets.Add(item);
                        await Task.Delay(2000);
                    }
                }
                else
                {
                    AppendLog($"正在向临时钱包地址: {item.Address}转帐...{_transferAmount}");
                    var result = await contractTransferToWallet(item.Address, _transferAmount);
                    if (result.IsSuccess)
                    {
                        AppendLog($"向临时钱包地址: {item.Address}转帐完成...{_transferAmount}");
                        File.AppendAllText("log.txt", $"向临时钱包地址: {item.Address},{item.PrivateKey}转帐完成...{_transferAmount}" + Environment.NewLine);
                        tempWallets.Add(item);
                        await Task.Delay(2000);
                    }
                }
              
            }
            AppendLog($"向临时钱包转帐完成...成功：{tempWallets.Count}个");


             

            var DeployAddress = "";
          




            int xi = 0;
            string relayAddres = "";
            foreach (var wallet in _targetWallets)
            {
                try
                {
                   
                    if (xi == 0)
                    {

                        
                        // 更新UI显示当前操作的钱包
                        await Dispatcher.InvokeAsync(() =>
                        {
                            UpdateCurrentOperation($"正在向 {wallet.Address} 转账...");
                            // 更新列表状态
                            UpdateWalletStatus(wallet.Address, "处理中");
                        });
                        AppendLog($"正在创建中继合约并转帐...");

                        int relayCount = 0;


                        AppendLog($"创建主合约");
                        while (true)
                        {
                            try
                            {
                                string rpcUrl = "https://testnet-rpc.monad.xyz"; // Monad测试网RPC
                                ContractService contractService = new ContractService(rpcUrl, _privatekey);
                                var Deployresult = await contractService.DeployMonadDistributor();
                                DeployAddress = Deployresult;
                                AppendLog($"创建主合约成功，合约地址：{Deployresult}");
                                break;
                            }
                            catch (Exception e)
                            {
                                await Task.Delay(5000);
                                if (relayCount >= 3)
                                {
                                    MessageBox.Show("主合约创建失败,请稍后再试");
                                    return;

                                }
                                relayCount++;
                              

                            }


                        }




                        relayCount = 0;

                        while (true)
                        {
                            try
                            {
                                string _rpcUrl = "https://testnet-rpc.monad.xyz"; // Monad测试网RPC
                                ContractService _contractService = new ContractService(_rpcUrl, tempWalletAddress.PrivateKey);
                                var relayAddress = await _contractService.Distribute(DeployAddress, wallet.Address, _transferAmount);// 创建新的中继合约并转账,返回创建的中继合约地址 
                                relayAddres = relayAddress.relayAddress;
                                AppendLog($"创建中继合约并转帐成功，合约地址：{relayAddress.relayAddress}");

                                // 更新日志和进度 - 确保在UI线程中执行
                                await Dispatcher.InvokeAsync(() =>
                                {
                                    // 更新钱包列表中的状态
                                    UpdateWalletStatus(wallet.Address, "成功");
                                    UpdateProgress(current, total);
                                    current++;
                                });
                                break;
                            }
                            catch (Exception e)
                            {
                                await Task.Delay(5000);
                                if (relayCount >= 3)
                                {
                                    MessageBox.Show("创建中继合约失败,请稍后再试");
                                    return;

                                }
                                relayCount++;

                            }
                        }
                       
                    }
                    else
                    {
                        try
                        {
                            string _rpcUrl = "https://testnet-rpc.monad.xyz"; // Monad测试网RPC
                            var privateKey3 = tempWallets[xi].PrivateKey;
                            AppendLog($"使用临时地址{tempWallets[xi].Address}调用中继合约{relayAddres}向{wallet.Address}转帐{_transferAmount} 除去GAS费用.");
                            var contractService3 = new ContractService(_rpcUrl, privateKey3);//这里我们使用了另一个私钥转帐 
                             
                            var relayTxHash = await contractService3.DistributeWithRelay(DeployAddress, relayAddres, wallet.Address, _transferAmount);
                            await Dispatcher.InvokeAsync(() =>
                            {
                                // 更新钱包列表中的状态
                                UpdateWalletStatus(wallet.Address, "成功");
                                UpdateProgress(current, total);
                                current++;
                            });
                        }
                        catch (Exception ex)
                        {
                            AppendLog($"使用临时地址{tempWallets[xi].Address}调用中继合约向{wallet.Address}转帐失败\r\n{ex.Message}");
                            await Dispatcher.InvokeAsync(() =>
                            {
                                // 更新钱包列表中的状态
                                UpdateWalletStatus(wallet.Address, "失败");
                                UpdateProgress(current, total);
                                current++;
                            });
                        }
                       
                    } 
                    
                    xi++;
                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    // 记录失败
                    _transferResults.Add(new TransferResult
                    {
                        WalletAddress = wallet.Address,
                        Amount = _transferAmount,
                        IsSuccess = false,
                        ErrorMessage = ex.Message
                    });
                    
                    // 更新日志和进度 - 确保在UI线程中执行
                    await Dispatcher.InvokeAsync(() =>
                    {
                        AppendLog($"错误: 向 {wallet.Address} 转账时发生异常: {ex.Message}");
                        // 更新钱包列表中的状态
                        UpdateWalletStatus(wallet.Address, "失败");
                        UpdateProgress(current, total);
                    });
                }
                AppendLog($"完成");
            }
             
        }

        private async void PharosTransferTask()
        {
            int total = _targetWallets.Count;
            int current = 0;

            // 先清空并初始化钱包余额列表
            await Dispatcher.InvokeAsync(() => {
                InitializeWalletBalanceList();
            });
            AppendLog($"正在生成{total}临时钱包..."); 
            var toAddresses = WalletService.tempGenerateWallets(total); // 生成临时钱包 
            List<Wallet> tempWallets = new List<Wallet>(); // 存储临时钱包>
            Wallet tempWalletAddress = toAddresses.Last();
            _transferAmount = _transferAmount + 0.0001m;
            foreach (var item in toAddresses)
            {
                if (item.Address == tempWalletAddress.Address)
                {
                    var transre = _transferAmount + 0.0002m;
                    AppendLog($"正在向临时中继合约创建钱包地址: {item.Address}转帐...{transre}");
                    var resultRe = await contractTransferToWallet(item.Address, transre);
                    if (resultRe.IsSuccess)
                    {
                        AppendLog($"向临时中继合约创建钱包地址: {item.Address}转帐完成...{transre}");
                        File.AppendAllText("log.txt", $"向临时中继合约创建钱包地址: {item.Address},{item.PrivateKey}转帐完成...{transre}" + Environment.NewLine);
                        item.Remark = "ower";
                        tempWallets.Add(item);
                        await Task.Delay(3000);
                    }

                }
                else
                {
                    AppendLog($"正在向临时钱包地址: {item.Address}转帐...{_transferAmount}");
                    var result = await contractTransferToWallet(item.Address, _transferAmount);
                    if (result.IsSuccess)
                    {
                        AppendLog($"向临时钱包地址: {item.Address}转帐完成...{_transferAmount}");
                        File.AppendAllText("log.txt", $"向临时钱包地址: {item.Address},{item.PrivateKey}转帐完成...{_transferAmount}" + Environment.NewLine);
                        tempWallets.Add(item);
                        await Task.Delay(3000);
                    }
                }

            }
            AppendLog($"向临时钱包转帐完成...成功：{tempWallets.Count}个");


            await Task.Delay(5000);


            var DeployAddress = "";





            int xi = 0;
            string relayAddres = "";
            foreach (var wallet in _targetWallets)
            {
                try
                {

                    if (xi == 0)
                    {
                         
                        int relayCount = 0;

                        // 更新UI显示当前操作的钱包
                        await Dispatcher.InvokeAsync(() =>
                        {
                            UpdateCurrentOperation($"创建主合约."); 
                        });
                        // AppendLog($"正在创建中继合约并转帐...");
                        while (true)
                        {
                            try
                            {
                                try
                                {
                                    AppendLog($"创建主合约");
                                    string rpcUrl = "https://testnet.dplabs-internal.com";  
                                    PharosContractService contractService = new PharosContractService(rpcUrl, tempWalletAddress.PrivateKey);
                                    var Deployresult = await contractService.DeployMonadDistributor();
                                    DeployAddress = Deployresult;
                                    AppendLog($"创建主合约成功，合约地址：{Deployresult}");
                                }
                                catch (Exception)
                                {

                                    continue;
                                }
                               
                                break;
                            }
                            catch (Exception e)
                            {
                                await Task.Delay(5000);
                                if (relayCount >= 3)
                                {
                                    MessageBox.Show("主合约创建失败,请稍后再试");
                                    return;

                                }
                                relayCount++;


                            }


                        }




                        relayCount = 0;
                        // 更新UI显示当前操作的钱包
                        await Dispatcher.InvokeAsync(() =>
                        {
                            UpdateCurrentOperation($"创建中继合约."); 
                        });
                        while (true)
                        {
                            try
                            {
                                AppendLog($"创建中继合约");
                                string _rpcUrl = "https://testnet.dplabs-internal.com";  
                                PharosContractService _contractService = new PharosContractService(_rpcUrl, tempWalletAddress.PrivateKey);
                                var relayAddress = await _contractService.DeployRelayContract(); 
                                relayAddres = relayAddress;
                                AppendLog($"创建中继合约成功，合约地址：{relayAddress}"); 
                                break;
                            }
                            catch (Exception e)
                            {
                                await Task.Delay(5000);
                                if (relayCount >= 5)
                                {
                                    MessageBox.Show("创建中继合约失败,请稍后再试");
                                    return;

                                }
                                relayCount++;

                            }
                        }

                        relayCount = 0;
                        await Dispatcher.InvokeAsync(() =>
                        {
                            UpdateCurrentOperation($"设置中继合约."); 
                        });
                        while (true)
                        {
                            try
                            {
                                AppendLog($"设置中继合约：:{relayAddres}::{DeployAddress}");
                                string _rpcUrl = "https://testnet.dplabs-internal.com"; 
                                PharosContractService _contractService = new PharosContractService(_rpcUrl, tempWalletAddress.PrivateKey);
                                var setRelayTxHash = await _contractService.SetRelayContract(relayAddres, DeployAddress); 
                                AppendLog($"设置中继合约成功。交易：{setRelayTxHash}"); 
                                await Task.Delay(5000);
                                break;
                            }
                            catch (Exception e)
                            {
                                await Task.Delay(5000);
                                if (relayCount >= 5)
                                {
                                    MessageBox.Show("设置中继合约失败,请稍后再试");
                                    return;

                                }
                                relayCount++;

                            }
                        }

                        try
                        {
                            await Dispatcher.InvokeAsync(() =>
                            {
                                UpdateCurrentOperation($"向{wallet.Address}转帐.");
                                // 更新列表状态
                                UpdateWalletStatus(wallet.Address, "处理中");
                            });
                            string _rpcUrl = "https://testnet.dplabs-internal.com";
                            var privateKey3 = tempWallets[xi].PrivateKey;
                            AppendLog($"使用临时地址{tempWallets[xi].Address}调用合约{DeployAddress}向{wallet.Address}转帐{_transferAmount} 除去GAS费用.");
                            PharosContractService _contractService = new PharosContractService(_rpcUrl, privateKey3);
                            var relayTxHash = await _contractService.ExecuteDistribute(DeployAddress, wallet.Address, _transferAmount);
                            await Dispatcher.InvokeAsync(() =>
                            {
                                // 更新钱包列表中的状态
                                UpdateWalletStatus(wallet.Address, "成功");
                                UpdateProgress(current, total);
                                current++;
                            });
                        }
                        catch (Exception ex)
                        {
                            AppendLog($"使用临时地址{tempWallets[xi].Address}调用合约向{wallet.Address}转帐失败\r\n{ex.Message}");
                            await Dispatcher.InvokeAsync(() =>
                            {
                                // 更新钱包列表中的状态
                                UpdateWalletStatus(wallet.Address, "失败");
                                UpdateProgress(current, total);
                                current++;
                            });
                        }
                    }
                    else
                    {
                        try
                        {
                            await Dispatcher.InvokeAsync(() =>
                            {
                                UpdateCurrentOperation($"向{wallet.Address}转帐.");
                                // 更新列表状态
                                UpdateWalletStatus(wallet.Address, "处理中");
                            });
                            string _rpcUrl = "https://testnet.dplabs-internal.com"; 
                            var privateKey3 = tempWallets[xi].PrivateKey;
                            AppendLog($"使用临时地址{tempWallets[xi].Address}调用合约{DeployAddress}向{wallet.Address}转帐{_transferAmount} 除去GAS费用.");
                            PharosContractService _contractService = new PharosContractService(_rpcUrl, privateKey3);  
                            var relayTxHash = await _contractService.ExecuteDistribute(DeployAddress,wallet.Address, _transferAmount);
                            await Dispatcher.InvokeAsync(() =>
                            {
                                // 更新钱包列表中的状态
                                UpdateWalletStatus(wallet.Address, "成功");
                                UpdateProgress(current, total);
                                current++;
                            });
                        }
                        catch (Exception ex)
                        {
                            AppendLog($"使用临时地址{tempWallets[xi].Address}调用合约向{wallet.Address}转帐失败\r\n{ex.Message}");
                            await Dispatcher.InvokeAsync(() =>
                            {
                                // 更新钱包列表中的状态
                                UpdateWalletStatus(wallet.Address, "失败");
                                UpdateProgress(current, total);
                                current++;
                            });
                        }

                    }

                    xi++;
                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    // 记录失败
                    _transferResults.Add(new TransferResult
                    {
                        WalletAddress = wallet.Address,
                        Amount = _transferAmount,
                        IsSuccess = false,
                        ErrorMessage = ex.Message
                    });

                    // 更新日志和进度 - 确保在UI线程中执行
                    await Dispatcher.InvokeAsync(() =>
                    {
                        AppendLog($"错误: 向 {wallet.Address} 转账时发生异常: {ex.Message}");
                        // 更新钱包列表中的状态
                        UpdateWalletStatus(wallet.Address, "失败");
                        UpdateProgress(current, total);
                        current++;
                    });
                }
                AppendLog($"完成");
            }

        }

        public static BigInteger EthToWei(string amountStr)
        {
            return Web3.Convert.ToWei(amountStr, Nethereum.Util.UnitConversion.EthUnit.Ether);
        }
        private async Task<string> SendMon(string toaddress, string amount)
        {
            int retryCount = 0;
            Account account = new Nethereum.Web3.Accounts.Account(_privatekey);
            var web3 = new Web3(account, rpcUrl);

            while (true)
            {
                try
                {
                    var gasPrice = await web3.Eth.GasPrice.SendRequestAsync();
                    var weiAmount = EthToWei(amount);

                    var transactionInput = new TransactionInput
                    {
                        From = account.Address,
                        To = toaddress,
                        Value = new HexBigInteger(weiAmount),
                        Gas = new HexBigInteger(21000),
                        GasPrice = gasPrice
                    };

                    var transactionReceipt = await web3.TransactionManager
                        .SendTransactionAndWaitForReceiptAsync(transactionInput);

                    if (transactionReceipt.Status.Value == 1)
                    {
                        return transactionReceipt.TransactionHash;
                    }
                    else
                    {
                        throw new Exception("交易失败");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"转账失败，正在重试... 原因: {ex.Message}");
                    await Task.Delay(3000);
                    if (retryCount >= 3)
                    {
                        throw;
                    }
                    retryCount++;
                }
            }
        }

        //private async Task<string> SendPharos(string toaddress, string amount)
        //{
        //    var rpcUrl = "https://testnet.dplabs-internal.com";
        //    int chainId = 688688;
        //    int retryCount = 0;

        //    Account account = new Nethereum.Web3.Accounts.Account(_privatekey, chainId);
        //    var web3 = new Web3(account, rpcUrl);

        //    string txnHash = null;
        //    string senderAddress = account.Address;
        //    var weiAmount = EthToWei(amount);

        //    // 获取 nonce（确保唯一）
        //    var nonce = await web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(senderAddress, BlockParameter.CreatePending());

        //    while (true)
        //    {
        //        try
        //        {
        //            if (txnHash == null)
        //            {
        //                Debug.WriteLine("🚀 正在准备发送转账交易...");

        //                var gasPrice = await web3.Eth.GasPrice.SendRequestAsync();

        //                var txInput = new TransactionInput
        //                {
        //                    From = senderAddress,
        //                    To = toaddress,
        //                    Value = new HexBigInteger(weiAmount),
        //                    Gas = new HexBigInteger(21000),
        //                    GasPrice = gasPrice,
        //                    Nonce = nonce
        //                };

        //                txnHash = await web3.Eth.Transactions.SendTransaction.SendRequestAsync(txInput);
        //                Debug.WriteLine($"📨 转账已发送，交易哈希: {txnHash}");
        //            }

        //            // 查询确认状态
        //            var startTime = DateTime.UtcNow;
        //            var timeout = TimeSpan.FromSeconds(120);

        //            while (DateTime.UtcNow - startTime < timeout)
        //            {
        //                var receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txnHash);
        //                if (receipt != null)
        //                {
        //                    if (receipt.Status.Value == 1)
        //                    {
        //                        Debug.WriteLine($"✅ 转账成功! Hash: {txnHash}");
        //                        return txnHash;
        //                    }
        //                    else
        //                    {
        //                        throw new Exception("交易失败（Receipt Status = 0）");
        //                    }
        //                }

        //                await Task.Delay(3000); // 每 3 秒轮询一次
        //            }

        //            throw new TimeoutException("交易未在规定时间内确认");
        //        }
        //        catch (Exception ex)
        //        {
        //            Debug.WriteLine($"❌ 转账异常: {ex.Message}");
        //            retryCount++;

        //            if (retryCount >= 20)
        //            {
        //                throw new Exception($"重试 5 次后仍然失败，最后的交易哈希: {txnHash ?? "未发送"}");
        //            }

        //            if (txnHash != null)
        //            {
        //                Debug.WriteLine($"📌 已发送交易（hash: {txnHash}），不再重发，只轮询...");
        //                await Task.Delay(3000); // 等待后继续轮询确认状态
        //            }
        //            else
        //            {
        //                Debug.WriteLine($"🔁 网络异常或发送失败，{retryCount} 秒后重试发送...");
        //                await Task.Delay(3000); // 网络发送失败，允许重发（相同 nonce）
        //            }
        //        }
        //    }
        //}

        //private async Task<string> SendPharos(string toAddress, string amount)
        //{
        //    var rpcUrl = "https://testnet.dplabs-internal.com";
        //    int chainId = 688688;
        //    int retryCount = 0;

        //    var account = new Account(_privatekey, chainId);
        //    var web3 = new Web3(account, rpcUrl);
        //    string senderAddress = account.Address;

        //    var weiAmount = EthToWei(amount);
        //    string txnHash = null;

        //    // 获取 nonce（建议使用 pending 防止 nonce 冲突）
        //    var nonce = await web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(senderAddress, BlockParameter.CreatePending());

        //    while (true)
        //    {
        //        try
        //        {
        //            if (txnHash == null)
        //            {
        //                Debug.WriteLine("🚀 正在构建并签名交易...");

        //                var gasPrice = await web3.Eth.GasPrice.SendRequestAsync();
        //                var tx = new TransactionInput
        //                {
        //                    From = senderAddress,
        //                    To = toAddress,
        //                    Value = new HexBigInteger(weiAmount),
        //                    Gas = new HexBigInteger(21000),
        //                    GasPrice = gasPrice,
        //                    Nonce = nonce
        //                };

        //                // ✅ 构建并签名 raw transaction
        //                var signer = new TransactionSigner();
        //                var rawTx = signer.SignTransaction(
        //                    _privatekey,
        //                    toAddress,
        //                    weiAmount,
        //                    nonce.Value,
        //                    gasPrice.Value,
        //                    new BigInteger(21000),
        //                    chainId);

        //                txnHash = await web3.Eth.Transactions.SendRawTransaction.SendRequestAsync("0x" + rawTx);
        //                Debug.WriteLine($"📨 转账已发送，交易哈希: {txnHash}");
        //            }

        //            // 等待确认
        //            var startTime = DateTime.UtcNow;
        //            var timeout = TimeSpan.FromSeconds(120);

        //            while (DateTime.UtcNow - startTime < timeout)
        //            {
        //                var receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txnHash);
        //                if (receipt != null)
        //                {
        //                    if (receipt.Status.Value == 1)
        //                    {
        //                        Debug.WriteLine($"✅ 转账成功! Hash: {txnHash}");
        //                        return txnHash;
        //                    }
        //                    else
        //                    {
        //                        throw new Exception("交易失败（Receipt Status = 0）");
        //                    }
        //                }

        //                await Task.Delay(3000);
        //            }

        //            throw new TimeoutException("交易未在规定时间内确认");
        //        }
        //        catch (Exception ex)
        //        {
        //            Debug.WriteLine($"❌ 异常: {ex.Message}");
        //            retryCount++;

        //            if (retryCount >= 20)
        //                throw new Exception($"重试失败，最后交易哈希: {txnHash ?? "未发送"}");

        //            if (txnHash != null)
        //            {
        //                Debug.WriteLine($"📌 已发送交易（hash: {txnHash}），继续轮询...");
        //                await Task.Delay(3000);
        //            }
        //            else
        //            {
        //                Debug.WriteLine($"🔁 网络或构建错误，重试中（{retryCount}）...");
        //                await Task.Delay(3000);
        //            }
        //        }
        //    }
        //}
        private async Task<string> SendPharos(string toAddress, string amount)
        {
            var rpcUrl = "https://testnet.dplabs-internal.com";
            int chainId = 688688;
            int retryCount = 0;
            int maxRetries = 20;

            Account account = new Nethereum.Web3.Accounts.Account(_privatekey, chainId);
            var web3 = new Web3(account, rpcUrl);

            string txnHash = null;
            string senderAddress = account.Address;
            var weiAmount = EthToWei(amount);

            // 获取 nonce（确保唯一）
            var nonce = await web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(senderAddress, BlockParameter.CreatePending());

            while (true)
            {
                try
                {
                    if (txnHash == null)
                    {
                        Debug.WriteLine("🚀 正在准备发送转账交易...");

                        var gasPrice = await web3.Eth.GasPrice.SendRequestAsync();
                        var gasLimit = new HexBigInteger(21000);

                        var transactionInput = new TransactionInput
                        {
                            From = senderAddress,
                            To = toAddress,
                            Value = new HexBigInteger(weiAmount),
                            Gas = gasLimit,
                            GasPrice = gasPrice,
                            Nonce = new HexBigInteger(nonce.Value)
                        };

                        // 直接使用账户签名并发送交易（等价于 account.TransactionManager.SendTransactionAsync）
                        txnHash = await account.TransactionManager.SendTransactionAsync(transactionInput);

                        Debug.WriteLine($"📨 转账已发送，交易哈希: {txnHash}");
                    }

                    // 查询确认状态
                    var startTime = DateTime.UtcNow;
                    var timeout = TimeSpan.FromSeconds(120);

                    while (DateTime.UtcNow - startTime < timeout)
                    {
                        var receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txnHash);
                        if (receipt != null)
                        {
                            if (receipt.Status.Value == 1)
                            {
                                Debug.WriteLine($"✅ 转账成功! Hash: {txnHash}");
                                return txnHash;
                            }
                            else
                            {
                                throw new Exception("交易失败（Receipt Status = 0）");
                            }
                        }

                        await Task.Delay(3000); // 每 3 秒轮询一次
                    }

                    throw new TimeoutException("交易未在规定时间内确认");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"❌ 转账异常: {ex.Message}");
                    retryCount++;

                    if (retryCount >= maxRetries)
                    {
                        throw new Exception($"重试 {maxRetries} 次后仍然失败，最后的交易哈希: {txnHash ?? "未发送"}");
                    }

                    if (txnHash != null)
                    {
                        Debug.WriteLine($"📌 已发送交易（hash: {txnHash}），不再重发，只轮询...");
                        await Task.Delay(3000); // 等待后继续轮询确认状态
                    }
                    else
                    {
                        Debug.WriteLine($"🔁 网络异常或发送失败，{retryCount} 秒后重试发送...");
                        await Task.Delay(3000); // 网络发送失败，允许重发（相同 nonce）
                    }
                }
            }
        }

        private async Task<TransferResult> contractTransferToWallet(string toAddress, decimal amount)
        {
             
            try
            {
               
                string projectName = ""; 
                await Dispatcher.InvokeAsync(() => {
                    ComboBoxItem selectedProjectItem = cmbProject.SelectedItem as ComboBoxItem;
                    projectName = selectedProjectItem?.Content?.ToString() ?? string.Empty;

                    
                    if (projectName == "Monad")
                    {
                       
                    }
                    else if (projectName == "Ethereum")
                    {
                       
                    }
                    else
                    {
                        
                    }
                });

                // 根据不同项目执行不同的转账逻辑
                string transactionHash = string.Empty;

                switch (projectName)
                {
                    case "Monad":
                        // Monad项目的转账逻辑
                        transactionHash = await SendMon(toAddress, amount.ToString());
                        // 返回成功结果
                        return new TransferResult
                        {
                            WalletAddress = toAddress,
                            Amount = amount,
                            TransactionHash = transactionHash,
                            IsSuccess = true
                        };
                    case "PharosNetwork":
                        // Monad项目的转账逻辑
                        transactionHash = await SendPharos(toAddress, amount.ToString());
                        // 返回成功结果
                        return new TransferResult
                        {
                            WalletAddress = toAddress,
                            Amount = amount,
                            TransactionHash = transactionHash,
                            IsSuccess = true
                        };

                    case "Ethereum":
                        break;
                    default:
                        break;
                }

                // 返回成功结果
                return new TransferResult
                {
                    WalletAddress = toAddress,
                    Amount = amount,
                    TransactionHash = transactionHash,
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                // 返回失败结果
                return new TransferResult
                {
                    WalletAddress = toAddress,
                    Amount = amount,
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                };
            }
        }


        private async Task<TransferResult> TransferToWallet(string toAddress, decimal amount)
        {
            
            try
            {
                // 获取当前选中的项目
                string projectName = "";
                
                // 在UI线程获取项目名称
                await Dispatcher.InvokeAsync(() => {
                    ComboBoxItem selectedProjectItem = cmbProject.SelectedItem as ComboBoxItem;
                    projectName = selectedProjectItem?.Content?.ToString() ?? string.Empty;
                    
                    // 在UI线程记录日志
                    if (projectName == "Monad") {
                        AppendLog($"使用Monad网络向 {toAddress} 转账 {amount} MON");
                    } else if (projectName == "Ethereum") {
                        AppendLog($"使用以太坊网络向 {toAddress} 转账 {amount} ETH");
                    } else {
                        AppendLog($"使用默认网络向 {toAddress} 转账 {amount} {_currentCurrencyUnit}");
                    }
                });
                
                // 根据不同项目执行不同的转账逻辑
                string transactionHash = string.Empty;
                
                switch (projectName)
                {
                    case "Monad":
                        // Monad项目的转账逻辑
                        transactionHash = await SendMon(toAddress, amount.ToString());
                        // 返回成功结果
                        return new TransferResult
                        {
                            WalletAddress = toAddress,
                            Amount = amount,
                            TransactionHash = transactionHash,
                            IsSuccess = true
                        };
                    
                    case "Ethereum":
                        break;
                    default: 
                        break;
                }

                // 返回成功结果
                return new TransferResult
                {
                    WalletAddress = toAddress,
                    Amount = amount,
                    TransactionHash = transactionHash,
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                // 返回失败结果
                return new TransferResult
                {
                    WalletAddress = toAddress,
                    Amount = amount,
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                };
            }
        }
        
        private void UpdateCurrentOperation(string message)
        {
            lblCurrentOperation.Text = message;
        }

        //private void AppendLog(string message)
        //{
        //    txtTransferLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
        //    txtTransferLog.ScrollToEnd();
        //}
        private async void AppendLog(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            try
            {
                // 检查 Dispatcher 是否可用
                if (Dispatcher == null)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
                    return;
                }

                // 检查控件是否存在
                if (txtTransferLog == null)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
                    return;
                }

                // 在UI线程处理异常
                await Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        txtTransferLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                        txtTransferLog.ScrollToEnd();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error appending log: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in AppendLog: {ex.Message}");
            }
        }
        private void UpdateProgress(int current, int total)
        {
            double percent = (double)current / total * 100;
            transferProgress.Value = percent;
            lblProgressPercent.Text = $"{percent:F0}%";
        }
        
        
        
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (_isTransferring)
            {
                if (MessageBox.Show("确定要关闭窗口吗？当前转账任务将继续在后台运行。", 
                                   "确认关闭", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    return;
                }
            }
            
            DialogResult = false;
            Close();
        }
        
        // 添加项目选择变更事件处理
        private void Project_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateCurrencyUnit();
            UpdateTotalAmount();
        }
        
        // 更新币种单位
        private void UpdateCurrencyUnit()
        {
            try 
            {
                if (cmbProject.SelectedItem == null) return;
                
                ComboBoxItem selectedItem = cmbProject.SelectedItem as ComboBoxItem;
                string projectName = selectedItem?.Content?.ToString();
                
                if (string.IsNullOrEmpty(projectName)) return;
                
                // 根据项目名称设置对应的币种单位
                switch (projectName)
                {
                    case "Monad":
                        _currentCurrencyUnit = "MON";
                        break;
                    case "Ethereum":
                        _currentCurrencyUnit = "ETH";
                        break;
                    default:
                        _currentCurrencyUnit = "ETH"; // 默认使用ETH
                        break;
                }
                
                // 更新UI上显示的单位 - 确保在UI线程中执行
                Dispatcher.Invoke(() => 
                {
                    // 更新转账金额单位
                    if (txtAmount != null && txtAmount.Parent is Grid grid)
                    {
                        for (int i = 0; i < grid.Children.Count; i++)
                        {
                            if (grid.Children[i] is TextBlock unitBlock && Grid.GetColumn(unitBlock) == 1)
                            {
                                unitBlock.Text = _currentCurrencyUnit;
                                break;
                            }
                        }
                    }
                    
                    // 更新其他使用币种单位的UI元素
                    UpdateTotalAmount();
                });
            }
            catch (Exception ex)
            {
                // 错误处理
                AppendLog($"更新币种单位失败: {ex.Message}");
            }
        }
        
        // 初始化钱包余额列表
        private void InitializeWalletBalanceList()
        {
            _walletBalanceItems.Clear();
            
            for (int i = 0; i < _targetWallets.Count; i++)
            {
                _walletBalanceItems.Add(new WalletBalanceItem
                {
                    Index = i + 1,
                    Address = _targetWallets[i].Address,
                    TransferAmount = $"{_transferAmount} {_currentCurrencyUnit}",
                    Balance = "待查询",
                    Status = "等待中"
                });
            }
        }
        
        // 更新钱包状态
        private void UpdateWalletStatus(string address, string status)
        {
            var item = _walletBalanceItems.FirstOrDefault(w => w.Address == address);
            if (item != null)
            {
                item.Status = status;
                
                // 强制刷新列表视图
                walletBalanceList.Items.Refresh();
            }
        }
        
        // 更新钱包余额
        private void UpdateWalletBalance(string address, string balance)
        {
            var item = _walletBalanceItems.FirstOrDefault(w => w.Address == address);
            if (item != null)
            {
                item.Balance = balance;
                
                // 强制刷新列表视图
                walletBalanceList.Items.Refresh();
            }
        }
        
        // 查询余额按钮点击事件
        private async void CheckBalances_Click(object sender, RoutedEventArgs e)
        {
            if (_targetWallets.Count == 0)
            {
                MessageBox.Show("没有可查询的钱包", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            btnCheckBalances.IsEnabled = false;
            btnCheckBalances.Content = "查询中...";
            
            try
            {
                // 初始化钱包余额列表
                InitializeWalletBalanceList();
                
                // 执行查询
                await FetchWalletBalances();
                
                MessageBox.Show("余额查询完成", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"查询余额时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnCheckBalances.IsEnabled = true;
                btnCheckBalances.Content = "查询余额";
            }
        }
        
        // 获取所有钱包余额
        private async Task FetchWalletBalances()
        {
            // 获取当前项目名称
            string projectName = "";
            ComboBoxItem selectedProjectItem = cmbProject.SelectedItem as ComboBoxItem;
            projectName = selectedProjectItem?.Content?.ToString() ?? string.Empty;
            
            // 获取交互类型（这里可以根据需要添加交互类型下拉框）
            string interactionType = "all"; // 默认查询所有类型
            
            AppendLog($"开始查询 {projectName} 项目的钱包余额...");
            
            // 依次查询每个钱包余额
            for (int i = 0; i < _targetWallets.Count; i++)
            {
                try
                {
                    var wallet = _targetWallets[i];
                    
                    // 更新操作状态
                    UpdateCurrentOperation($"正在查询 {wallet.Address} 的余额... ({i+1}/{_targetWallets.Count})");
                    UpdateProgress(i, _targetWallets.Count);
                    
                    // 查询该钱包的代币余额
                    var tokenBalance = await QueryTokenBalance(wallet.Address, projectName, interactionType);
                    
                    // 更新UI显示
                    var item = _walletBalanceItems.FirstOrDefault(w => w.Address == wallet.Address);
                    if (item != null)
                    {
                        // 更新余额和颜色
                        item.Balance = $"{tokenBalance:F4}";
                        item.BalanceColor = "#4CAF50"; // 绿色
                        item.Status = "查询完成";
                        // 强制刷新UI
                        walletBalanceList.Items.Refresh();
                    }
                    
                    // 添加延迟避免API限流
                    await Task.Delay(300);
                }
                catch (Exception ex)
                {
                    // 处理查询失败
                    var wallet = _targetWallets[i];
                    var item = _walletBalanceItems.FirstOrDefault(w => w.Address == wallet.Address);
                    if (item != null)
                    {
                        item.Balance = "查询失败";
                        item.BalanceColor = "#F44336"; // 红色
                        walletBalanceList.Items.Refresh();
                    }
                    AppendLog($"查询钱包 {wallet.Address} 余额失败: {ex.Message}");
                }
            }
            
            // 更新进度为100%
            UpdateProgress(_targetWallets.Count, _targetWallets.Count);
           // UpdateCurrentOperation("余额查询完成");
        }
        
        // 根据代币类型返回对应的颜色
        private string GetColorForToken(string tokenSymbol)
        {
            switch (tokenSymbol)
            {
                case "sMON": return "#4CAF50"; // 绿色
                case "gMON": return "#2196F3"; // 蓝色
                case "LP": return "#FF9800";   // 橙色
                default: return "#000000";     // 黑色
            }
        }
        
        // 查询代币余额的统一入口
        // 返回值: (代币符号, 代币余额, 交互次数)
        private async Task<decimal> QueryTokenBalance(string walletAddress, string projectName, string interactionType)
        {
            
            // 处理不同项目
            if (projectName == "Monad")
            {
                var Balances = await GetEVMBalance.GetEthBalanceAsync(rpcUrl,walletAddress);
                return Balances;
            } 
            else
            {
                // 其他项目返回默认代币
                return (0m);
            }
        }

        /*
         * 实际实现指南：
         * ============
         * 
         * 针对不同代币类型的实际查询实现：
         * 
         * 1. 原生代币余额查询 (MON/ETH)
         *    使用Web3.Eth.GetBalance方法获取原生代币余额：
         *    
         *    private async Task<decimal> GetNativeBalance(string address)
         *    {
         *        try
         *        {
         *            var web3 = new Web3(rpcUrl);
         *            var balance = await web3.Eth.GetBalance.SendRequestAsync(address);
         *            return Web3.Convert.FromWei(balance); // 将Wei转换为ETH单位
         *        }
         *        catch (Exception ex)
         *        {
         *            AppendLog($"获取原生代币余额失败: {ex.Message}");
         *            return 0;
         *        }
         *    }
         *    
         * 2. sMON代币余额查询
         *    使用MonadStaking合约的GetBalanceAsync方法：
         *    
         *    private async Task<decimal> GetStakedMonBalance(string address)
         *    {
         *        try
         *        {
         *            // 创建MonadStaking实例
         *            var staking = new ConScript.MonadStaking(rpcUrl);
         *            // 查询质押余额
         *            var result = await staking.GetBalanceAsync(address);
         *            if (result.Success)
         *            {
         *                return result.Value;
         *            }
         *            else
         *            {
         *                AppendLog($"获取sMON余额失败: {result.Message}");
         *                return 0;
         *            }
         *        }
         *        catch (Exception ex)
         *        {
         *            AppendLog($"获取sMON余额异常: {ex.Message}");
         *            return 0;
         *        }
         *    }
         *    
         * 3. gMON代币余额查询（ERC20代币）
         *    使用通用的ERC20合约接口查询：
         *    
         *    private async Task<decimal> GetERC20Balance(string tokenAddress, string walletAddress, int decimals = 18)
         *    {
         *        try
         *        {
         *            var web3 = new Web3(rpcUrl);
         *            // 创建合约服务
         *            var contract = web3.Eth.GetContract(ERC20ABI, tokenAddress);
         *            // 获取balanceOf函数
         *            var balanceFunction = contract.GetFunction("balanceOf");
         *            // 调用函数
         *            var balance = await balanceFunction.CallAsync<BigInteger>(walletAddress);
         *            // 根据代币精度转换
         *            return (decimal)balance / (decimal)Math.Pow(10, decimals);
         *        }
         *        catch (Exception ex)
         *        {
         *            AppendLog($"获取ERC20代币余额失败: {ex.Message}");
         *            return 0;
         *        }
         *    }
         *    
         *    // ERC20标准ABI
         *    private const string ERC20ABI = @"[{""constant"":true,""inputs"":[{""name"":""_owner"",""type"":""address""}],""name"":""balanceOf"",""outputs"":[{""name"":""balance"",""type"":""uint256""}],""type"":""function""}]";
         *    
         * 4. LP代币余额查询
         *    同样使用ERC20接口，但需要指定LP代币的合约地址：
         *    
         *    private async Task<decimal> GetLPTokenBalance(string walletAddress)
         *    {
         *        // LP代币合约地址，需要根据实际情况替换
         *        string lpTokenAddress = "0x1234567890123456789012345678901234567890";
         *        return await GetERC20Balance(lpTokenAddress, walletAddress);
         *    }
         *    
         * 5. 实际的QueryTokenBalance实现
         *    整合上述方法，根据交互类型返回相应的代币余额：
         *    
         *    private async Task<(string tokenSymbol, decimal tokenBalance, int interactionCount)> ActualQueryTokenBalance(string walletAddress, string projectName, string interactionType)
         *    {
         *        try
         *        {
         *            // 获取交互次数（可以从ReportService中查询）
         *            int interactionCount = GetInteractionCount(walletAddress, projectName, interactionType);
         *            
         *            if (projectName == "Monad")
         *            {
         *                switch (interactionType.ToLower())
         *                {
         *                    case "stake":
         *                        var smonBalance = await GetStakedMonBalance(walletAddress);
         *                        return ("sMON", smonBalance, interactionCount);
         *                    
         *                    case "unstake":
         *                        var monBalance = await GetNativeBalance(walletAddress);
         *                        return ("MON", monBalance, interactionCount);
         *                    
         *                    case "gmon":
         *                        // gMON代币地址，需要替换为实际地址
         *                        string gMonAddress = "0xgMonContractAddress";
         *                        var gmonBalance = await GetERC20Balance(gMonAddress, walletAddress);
         *                        return ("gMON", gmonBalance, interactionCount);
         *                    
         *                    case "swap":
         *                        var lpBalance = await GetLPTokenBalance(walletAddress);
         *                        return ("LP", lpBalance, interactionCount);
         *                    
         *                    default:
         *                        var nativeBalance = await GetNativeBalance(walletAddress);
         *                        return ("MON", nativeBalance, interactionCount);
         *                }
         *            }
         *            else if (projectName == "Ethereum")
         *            {
         *                // Ethereum项目的代币查询逻辑
         *                // ...类似实现...
         *            }
         *            
         *            // 默认返回原生代币余额
         *            var defaultBalance = await GetNativeBalance(walletAddress);
         *            return (_currentCurrencyUnit, defaultBalance, interactionCount);
         *        }
         *        catch (Exception ex)
         *        {
         *            AppendLog($"查询代币余额失败: {ex.Message}");
         *            return (_currentCurrencyUnit, 0, 0);
         *        }
         *    }
         */
    }
    
    // 钱包余额项数据模型
    public class WalletBalanceItem : INotifyPropertyChanged
    {
        private int _index;
        private string _address;
        private string _transferAmount;
        private string _balance;
        private string _status;
        private string _balanceColor = "#000000"; // 默认黑色
        
        public int Index 
        { 
            get { return _index; }
            set 
            { 
                if (_index != value)
                {
                    _index = value;
                    OnPropertyChanged(nameof(Index));
                }
            }
        }
        
        public string Address 
        { 
            get { return _address; }
            set 
            { 
                if (_address != value)
                {
                    _address = value;
                    OnPropertyChanged(nameof(Address));
                }
            }
        }
        
        public string TransferAmount 
        { 
            get { return _transferAmount; }
            set 
            { 
                if (_transferAmount != value)
                {
                    _transferAmount = value;
                    OnPropertyChanged(nameof(TransferAmount));
                }
            }
        }
        
        public string Balance 
        { 
            get { return _balance; }
            set 
            { 
                if (_balance != value)
                {
                    _balance = value;
                    OnPropertyChanged(nameof(Balance));
                }
            }
        }
        
        public string Status 
        { 
            get { return _status; }
            set 
            { 
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                }
            }
        }
        
        public string BalanceColor
        {
            get { return _balanceColor; }
            set 
            { 
                if (_balanceColor != value)
                {
                    _balanceColor = value;
                    OnPropertyChanged(nameof(BalanceColor));
                }
            }
        }
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    
    // 转账结果类
    public class TransferResult
    {
        public string WalletAddress { get; set; }
        public decimal Amount { get; set; }
        public string TransactionHash { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
    }
}  
 