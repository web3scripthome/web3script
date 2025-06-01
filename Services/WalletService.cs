using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using web3script.Models;
using NBitcoin;
using Nethereum.HdWallet;
using Newtonsoft.Json;
using Wallet = web3script.Models.Wallet;
using Nethereum.Web3;
using System.Security.Cryptography;
using System.Diagnostics;

namespace web3script.Services
{
    public class WalletService
    {
        private readonly string _walletsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "wallets.json");
        private readonly string _walletGroupsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "walletgroups.json");
        
        public List<Models.Wallet> Wallets { get; private set; } = new List<Models.Wallet>();
        public List<WalletGroup> WalletGroups { get; private set; } = new List<WalletGroup>();
        
        // 当钱包分组变更时触发的事件
        public event EventHandler WalletGroupsChanged;

        public WalletService()
        {
            LoadWallets();
            LoadWalletGroups();
        }

        public void LoadWallets()
        {
            try
            {
                if (File.Exists(_walletsFilePath))
                {
                    string json = File.ReadAllText(_walletsFilePath);
                    Wallets = JsonConvert.DeserializeObject<List<Wallet>>(json) ?? new List<Wallet>();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading wallets: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Wallets = new List<Wallet>();
            }
        }

        public void LoadWalletGroups()
        {
            try
            {
                if (File.Exists(_walletGroupsFilePath))
                {
                    string json = File.ReadAllText(_walletGroupsFilePath);
                    WalletGroups = JsonConvert.DeserializeObject<List<WalletGroup>>(json) ?? new List<WalletGroup>();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading wallet groups: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                WalletGroups = new List<WalletGroup>();
            }
        }

        public void SaveWallets()
        {
            try
            {
                string directory = Path.GetDirectoryName(_walletsFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonConvert.SerializeObject(Wallets, Formatting.Indented);
                File.WriteAllText(_walletsFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving wallets: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void SaveWalletGroups()
        {
            try
            {
                string directory = Path.GetDirectoryName(_walletGroupsFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonConvert.SerializeObject(WalletGroups, Formatting.Indented);
                File.WriteAllText(_walletGroupsFilePath, json);
                
                // 触发分组变更事件
                WalletGroupsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving wallet groups: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public List<Wallet> GenerateWallets(int count)
        {
            List<Wallet> newWallets = new List<Wallet>();
            
            for (int i = 0; i < count; i++)
            {
                try
                {
                    var wallet = new Wallet();

                    Mnemonic menmonic =  new Mnemonic(Wordlist.English,WordCount.Twelve);
                    ExtKey hdroot = menmonic.DeriveExtKey();
                    var extkey = hdroot.Derive(new NBitcoin.KeyPath("m/44'/60'/0'/0/0"));
                    var eth1 = new Nethereum.Signer.EthECKey(extkey.PrivateKey.ToBytes(), true); 
                    wallet.Address = eth1.GetPublicAddress();
                    wallet.PrivateKey = eth1.GetPrivateKey();
                    wallet.Mnemonic = menmonic.ToString();
                    wallet.Remark = $"Wallet {i + 1}"; 
                    newWallets.Add(wallet);
                    Wallets.Add(wallet);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error generating wallet #{i + 1}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            
            SaveWallets();
            return newWallets;
        }
        public static List<Wallet> tempGenerateWallets(int count)
        {
            List<Wallet> newWallets = new List<Wallet>();

            for (int i = 0; i < count; i++)
            {
                try
                {
                    var wallet = new Wallet();

                    Mnemonic menmonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
                    ExtKey hdroot = menmonic.DeriveExtKey();
                    var extkey = hdroot.Derive(new NBitcoin.KeyPath("m/44'/60'/0'/0/0"));
                    var eth1 = new Nethereum.Signer.EthECKey(extkey.PrivateKey.ToBytes(), true);
                    wallet.Address = eth1.GetPublicAddress();
                    wallet.PrivateKey = eth1.GetPrivateKey();
                    wallet.Mnemonic = menmonic.ToString();
                    wallet.Remark = $"Wallet {i + 1}";
                    newWallets.Add(wallet);
                   
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error generating wallet #{i + 1}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            
            return newWallets;
        }
        public WalletGroup AddGroup(string name)
        {
            var group = new WalletGroup
            {
                Name = name
            };
            
            WalletGroups.Add(group);
            SaveWalletGroups();
            
            return group;
        }

        public void AddWalletsToGroup(string groupId, List<string> walletIds)
        {
            var group = WalletGroups.FirstOrDefault(g => g.Id == groupId);
            if (group != null)
            {
                foreach (var walletId in walletIds)
                {
                    if (!group.WalletIds.Contains(walletId))
                    {
                        group.WalletIds.Add(walletId);
                    }
                }
                
                SaveWalletGroups();
            }
        }

        public void RemoveWalletsFromGroup(string groupId, List<string> walletIds)
        {
            var group = WalletGroups.FirstOrDefault(g => g.Id == groupId);
            if (group != null)
            {
                foreach (var walletId in walletIds)
                {
                    group.WalletIds.Remove(walletId);
                }
                
                SaveWalletGroups();
            }
        }

        public void DeleteGroup(string groupId)
        {
            var group = WalletGroups.FirstOrDefault(g => g.Id == groupId);
            if (group != null)
            {
                WalletGroups.Remove(group);
                SaveWalletGroups();
            }
        }

        public List<Wallet> GetWalletsInGroup(string groupId)
        {
            var group = WalletGroups.FirstOrDefault(g => g.Id == groupId);
            if (group != null)
            {
                return Wallets.Where(w => group.WalletIds.Contains(w.Id)).ToList();
            }
            
            return new List<Wallet>();
        }

        // 尝试导入钱包（支持私钥或助记词）
        public bool TryImportWallet(string input, out Wallet wallet)
        {
            wallet = null;
            
            // 清除输入两端的空白字符
            input = input.Trim();
            
            try
            {
                // 尝试从输入中提取私钥（移除前缀、标签、空格等）
                string privateKey = ExtractPrivateKey(input);
                
                // 尝试从输入中提取助记词
                string mnemonic = ExtractMnemonic(input);
                
                // 如果同时存在助记词和私钥，验证它们是否指向同一地址
                if (!string.IsNullOrEmpty(privateKey) && !string.IsNullOrEmpty(mnemonic))
                {
                    // 尝试从助记词导入钱包
                    Wallet mnemonicWallet = ImportFromMnemonic(mnemonic, false);
                    
                    // 尝试从私钥导入钱包
                    Wallet privateKeyWallet = ImportFromPrivateKey(privateKey, false);
                    
                    // 如果两者指向同一地址，优先使用助记词导入
                    if (mnemonicWallet != null && privateKeyWallet != null && 
                        mnemonicWallet.Address.Equals(privateKeyWallet.Address, StringComparison.OrdinalIgnoreCase))
                    {
                        wallet = mnemonicWallet;
                        // 检查是否已存在相同地址的钱包
                        if (AddWalletIfNotExists(wallet))
                        {
                            // 导入成功后保存钱包
                            SaveWallets();
                            return true;
                        }
                        return false;
                    }
                }
                
                // 优先使用助记词导入
                if (!string.IsNullOrEmpty(mnemonic))
                {
                    wallet = ImportFromMnemonic(mnemonic);
                    if (wallet != null)
                    {
                        // 导入成功后保存钱包
                        SaveWallets();
                        return true;
                    }
                    return false;
                }
                
                // 如果没有有效的助记词，尝试使用私钥导入
                if (!string.IsNullOrEmpty(privateKey))
                {
                    wallet = ImportFromPrivateKey(privateKey);
                    if (wallet != null)
                    {
                        // 导入成功后保存钱包
                        SaveWallets();
                        return true;
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Import wallet error: {ex.Message}");
                return false;
            }
            
            return false;
        }
        
        // 提取可能的私钥
        public string ExtractPrivateKey(string input)
        {
            // 清除标签、前缀等
            input = input.Replace("私钥:", "").Replace("私钥：", "").Replace("私钥=", "")
                         .Replace("privateKey:", "").Replace("privateKey=", "")
                         .Replace("PrivateKey:", "").Replace("PrivateKey=", "")
                         .Trim();
            
            // 处理可能的0x前缀
            if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                input = input.Substring(2);
            }
            
            // 验证是否是有效的私钥格式（64个十六进制字符）
            if (input.Length == 64 && input.All(c => "0123456789abcdefABCDEF".Contains(c)))
            {
                return input;
            }
            
            return null;
        }
        
        // 提取可能的助记词
        public string ExtractMnemonic(string input)
        {
            // 清除标签、前缀等
            input = input.Replace("助记词:", "").Replace("助记词：", "").Replace("助记词=", "")
                         .Replace("mnemonic:", "").Replace("mnemonic=", "")
                         .Replace("Mnemonic:", "").Replace("Mnemonic=", "")
                         .Trim();
            
            // 检查是否包含至少12个单词，以空格分隔
            string[] words = input.Split(new[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            
            // 验证是否是有效的助记词格式（12、15、18、21或24个单词）
            if (words.Length == 12 || words.Length == 15 || words.Length == 18 || 
                words.Length == 21 || words.Length == 24)
            {
                // 验证每个单词是否都在助记词词汇表中
                try
                {
                    Wordlist wordlist = Wordlist.English;
                    if (words.All(word => wordlist.WordExists(word, out _)))
                    {
                        return string.Join(" ", words);
                    }
                }
                catch
                {
                    // 如果验证过程中发生错误，可能不是有效的助记词
                    return null;
                }
            }
            
            return null;
        }
        
        // 从私钥导入钱包
        public Wallet ImportFromPrivateKey(string privateKey, bool checkExists = true)
        {
            try
            {
                // 确保私钥格式正确
                if (!privateKey.StartsWith("0x"))
                {
                    privateKey = "0x" + privateKey;
                }
                
                var ethECKey = new Nethereum.Signer.EthECKey(privateKey);
                var address = ethECKey.GetPublicAddress();
                
                // 创建钱包对象
                var wallet = new Wallet
                {
                    Address = address,
                    PrivateKey = privateKey.StartsWith("0x") ? privateKey.Substring(2) : privateKey,
                    Mnemonic = "N/A", // 从私钥导入无法恢复助记词
                    Remark = "Imported from private key"
                };
                
                // 检查是否已存在相同地址的钱包
                if (checkExists)
                {
                    // 检查是否存在，存在则不添加，返回当前钱包对象
                    if (!AddWalletIfNotExists(wallet))
                    {
                        // 如果钱包已存在，静默返回，不显示警告
                        // 查找现有的钱包并返回
                        return Wallets.FirstOrDefault(w => w.Address.Equals(address, StringComparison.OrdinalIgnoreCase));
                    }
                    // 导入新钱包成功，在TryImportWallet中会调用SaveWallets
                }
                else
                {
                    // 如果不检查存在性，直接返回创建的钱包对象，不添加到Wallets集合
                }
                
                return wallet;
            }
            catch (Exception ex)
            {
                if (checkExists)
                {
                    MessageBox.Show($"导入私钥失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return null;
            }
        }
        
        // 从助记词导入钱包
        public Wallet ImportFromMnemonic(string mnemonicString, bool checkExists = true)
        {
            try
            {
                var mnemonic = new Mnemonic(mnemonicString);
                var hdRoot = mnemonic.DeriveExtKey();
                var extKey = hdRoot.Derive(new NBitcoin.KeyPath("m/44'/60'/0'/0/0"));
                var ethECKey = new Nethereum.Signer.EthECKey(extKey.PrivateKey.ToBytes(), true);
                
                var address = ethECKey.GetPublicAddress();
                var privateKey = ethECKey.GetPrivateKey();
                
                // 创建钱包对象
                var wallet = new Wallet
                {
                    Address = address,
                    PrivateKey = privateKey,
                    Mnemonic = mnemonicString,
                    Remark = "Imported from mnemonic"
                };
                
                // 检查是否已存在相同地址的钱包
                if (checkExists)
                {
                    // 使用AddWalletIfNotExists方法处理重复钱包
                    if (!AddWalletIfNotExists(wallet))
                    {
                        // 如果钱包已存在，静默返回，不显示警告
                        // 查找现有的钱包并返回
                        return Wallets.FirstOrDefault(w => w.Address.Equals(address, StringComparison.OrdinalIgnoreCase));
                    }
                    // 导入新钱包成功，在TryImportWallet中会调用SaveWallets
                }
                else
                {
                    // 如果不检查存在性，直接返回创建的钱包对象，不添加到Wallets集合
                }
                
                return wallet;
            }
            catch (Exception ex)
            {
                if (checkExists)
                {
                    MessageBox.Show($"导入助记词失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return null;
            }
        }
        
        // 添加钱包如果不存在
        public bool AddWalletIfNotExists(Wallet wallet)
        {
            // 检查是否已存在相同地址的钱包
            var existingWallet = Wallets.FirstOrDefault(w => w.Address.Equals(wallet.Address, StringComparison.OrdinalIgnoreCase));
            if (existingWallet == null)
            {
                // 钱包不存在，直接添加
                Wallets.Add(wallet);
                return true;
            }
            else
            {
                // 如果现有钱包没有助记词，但新钱包有，则更新现有钱包的助记词
                if (existingWallet.Mnemonic == "N/A" && wallet.Mnemonic != "N/A")
                {
                    existingWallet.Mnemonic = wallet.Mnemonic;
                    existingWallet.Remark = "Updated with mnemonic";
                    return true;
                }
                // 如果现有钱包和新钱包都有助记词但不同，则记录下来
                else if (existingWallet.Mnemonic != "N/A" && wallet.Mnemonic != "N/A" && 
                        !existingWallet.Mnemonic.Equals(wallet.Mnemonic, StringComparison.OrdinalIgnoreCase))
                {
                    // 这里可以选择记录或提醒用户，目前我们不执行操作
                    Debug.WriteLine($"钱包地址 {wallet.Address} 有两种不同的助记词");
                }
                // 如果现有钱包没有私钥（这种情况应该不存在），但新钱包有，则更新私钥
                if (string.IsNullOrEmpty(existingWallet.PrivateKey) && !string.IsNullOrEmpty(wallet.PrivateKey))
                {
                    existingWallet.PrivateKey = wallet.PrivateKey;
                    existingWallet.Remark = existingWallet.Remark + " (Updated with private key)";
                    return true;
                }
                // 其他情况都是钱包已存在，不做更改
                return false;
            }
        }

        // 添加获取所有钱包的方法
        public List<Wallet> GetWallets()
        {
            return Wallets;
        }
        
        // 添加获取钱包所属组的方法
        public string GetWalletGroup(string walletId)
        {
            var group = WalletGroups.FirstOrDefault(g => g.WalletIds.Contains(walletId));
            return group?.Name;
        }
        
        // 添加获取所有钱包组的方法
        public List<WalletGroup> GetGroups()
        {
            return WalletGroups;
        }
    }
} 