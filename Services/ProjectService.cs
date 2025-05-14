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
                    Investors = new List<string> { "L1", "Blockchain Capital", "Tech Innovations" },
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
