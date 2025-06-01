using web3script.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace web3script.Services
{
    public class ProjectService
    {
        private readonly string _dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "projects.json");
        
        public List<Project> GetProjects()
        {
            return LoadProjects();
        }
        
        public List<Project> LoadProjects()
        {
            //try
            //{
            //    if (File.Exists(_dataPath))
            //    {
            //        string json = File.ReadAllText(_dataPath);
            //        return JsonConvert.DeserializeObject<List<Project>>(json) ?? new List<Project>();
            //    }
            //}
            //catch (Exception ex)
            //{
            //    Debug.WriteLine($"Error loading projects: {ex.Message}");
            //}
            
            // 如果文件不存在或出错，返回默认项目
            return GetDefaultProjects();
        }
        
        private List<Project> GetDefaultProjects()
        {
            var projects = new List<Project>
            {
                new Project
                {
                    Name = "Monad",
                    Tags = "L1",
                    Ecosystem = "web3",
                    StartTime = "2022-01",
                    ProjectType = "L1",
                    Description = "Monad 是一个专注于高性能和兼容 EVM 的新一代区块链项目，其目标是提升区块链智能合约平台的吞吐量和效率，同时保持对现有以太坊生态的兼容性。L1",
                    LogoText = "Mo",
                    LogoBg = "#4B5563",
                    Website = "https://testnet.monad.xyz/",
                    LinkedInUrl = "https://linkedin.com/company/monad-ai",
                    BlogUrl = "https://monad.example.com/blog",
                    ThreadCount = "1",
                    Investors = new List<string> { "AI Ventures", "Blockchain Capital", "Tech Innovations" },
                    ExecutionItems = new List<ExecutionItem>
                    {

                        new ExecutionItem { Name = "自动质押magma(gMON)", IsSelected = false },
                        new ExecutionItem { Name = "自动质押aPriori(aprMON)", IsSelected = false },
                        new ExecutionItem { Name = "自动Mintmagiceden(NFT)", IsSelected = false },
                        new ExecutionItem { Name = "自动创建域名(nad.domains)", IsSelected = false },
                        new ExecutionItem { Name = "自动创建Meme(nad.fun)", IsSelected = false },
                        new ExecutionItem { Name = "自动买卖本帐号创建Meme(nad.fun)", IsSelected = false },
                        new ExecutionItem { Name = "自动买卖最新内盘Meme(nad.fun)", IsSelected = false },
                        new ExecutionItem { Name = "自动买卖市值前[1-10]Meme(nad.fun)", IsSelected = false },
                        new ExecutionItem { Name = "自动卖出所有持仓Meme(nad.fun)", IsSelected = false },
                    }
                    },
                //new Project
                //{
                //    Name = "CoreSky",
                //    Tags = "web3",
                //    Ecosystem = "web3",
                //    StartTime = "2022-01",
                //    ProjectType = "web3",
                //    Description = "CoreSky TGE即将到来",
                //    LogoText = "Co",
                //    LogoBg = "#4B5563",
                //    Website = "https://testnet.monad.xyz/",
                //    LinkedInUrl = "https://linkedin.com/company/monad-ai",
                //    BlogUrl = "https://monad.example.com/blog",
                //    ThreadCount = "1",
                //    Investors = new List<string> { "AI Ventures", "Blockchain Capital", "Tech Innovations" },
                //    ExecutionItems = new List<ExecutionItem>
                //    {

                //        new ExecutionItem { Name = "自动创建新帐号(CoreSky)", IsSelected = false },
                //        new ExecutionItem { Name = "每日签到并投票(CoreSky)", IsSelected = false },
                //    }
                //    },
                    new Project
                {
                    Name = "PharosNetwork",
                    Tags = "web3",
                    Ecosystem = "web3",
                    StartTime = "2022-01",
                    ProjectType = "web3",
                    Description = "PharosNetwork  ",
                    LogoText = "Ph",
                    LogoBg = "#4B5563",
                    Website = "https://testnet.monad.xyz/",
                    LinkedInUrl = "https://linkedin.com/company/monad-ai",
                    BlogUrl = "https://monad.example.com/blog",
                    ThreadCount = "1",
                    Investors = new List<string> { "AI Ventures", "Blockchain Capital", "Tech Innovations" },
                    ExecutionItems = new List<ExecutionItem>
                    {

                       // new ExecutionItem { Name = "创建主帐号(PharosNetwork)", IsSelected = false }, 
                        new ExecutionItem { Name = "绑定主帐号(PharosNetwork)", IsSelected = false }, 
                        new ExecutionItem { Name = "每日签到(PharosNetwork)", IsSelected = false },
                        new ExecutionItem { Name = "交互任务SWAP(Phrs换wPhrs)(PharosNetwork)", IsSelected = false },
                        new ExecutionItem { Name = "交互任务SWAP(wPhrs换USDC)(PharosNetwork)", IsSelected = false },
                        new ExecutionItem { Name = "交互任务SWAP(wPhrs换USDT)(PharosNetwork)", IsSelected = false },
                        new ExecutionItem { Name = "交互任务SWAP(USDC换wPhrs)(PharosNetwork)", IsSelected = false },//交互任务SWAP(wPhrs换Phrs)(PharosNetwork)
                        new ExecutionItem { Name = "交互任务SWAP(USDT换wPhrs)(PharosNetwork)", IsSelected = false }, 
                        new ExecutionItem { Name = "交互任务SWAP(wPhrs换Phrs)(PharosNetwork)", IsSelected = false },
                        new ExecutionItem { Name = "交互任务Addliquidity(wPhrs/usdc)[添加流动性0.0001wPhrs/usdc](PharosNetwork)", IsSelected = false },
                        new ExecutionItem { Name = "交互任务Addliquidity(usdc/usdt) [添加流动性1usdc/usdt](PharosNetwork)", IsSelected = false },
                        new ExecutionItem { Name = "特殊福利[使用本脚本每日可领0.01Phrs](PharosNetwork)", IsSelected = false },
                    }
                    },
                //new Project
                //{
                //    Name = "bluwhale",
                //    Tags = "bluwhale",
                //    Ecosystem = "web3",
                //    StartTime = "2021-06",
                //    ProjectType = "L1",
                //    Description = "bluwhale",
                //    LogoText = "BL",
                //    LogoBg = "#6366F1",
                //    Website = "https://somnia.example.com",
                //    LinkedInUrl = "https://linkedin.com/company/somnia-vr",
                //    BlogUrl = "https://somnia.example.com/blog",
                //    ThreadCount = "8",
                //    Investors = new List<string> { "Dream Ventures", "Reality Fund", "bluwhale" },
                //    ExecutionItems = new List<ExecutionItem>
                //    {
                //        new ExecutionItem { Name = "为主帐号创建推广小号(bluwhale)", IsSelected = false },
                //        new ExecutionItem { Name = "每日签到(bluwhale)", IsSelected = false },
                //    }
                //}, 
                };



            return projects;
        }
        
        public void SaveProjects(List<Project> projects)
        {
            try
            {
                string directoryPath = Path.GetDirectoryName(_dataPath);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
                
                string json = JsonConvert.SerializeObject(projects, Formatting.Indented);
                File.WriteAllText(_dataPath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving projects: {ex.Message}");
            }
        }
    }
} 