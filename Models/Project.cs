using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace web3script.Models
{
    public class Project
    {
        // 基本信息
        public string Id { get; set; }
        public string Name { get; set; }
        public string Tags { get; set; }
        public string Ecosystem { get; set; }
        public string StartTime { get; set; }
        public string ProjectType { get; set; }
        public List<string> Investors { get; set; }
        
        // 显示属性
        public string LogoText { get; set; }
        public string LogoBg { get; set; }
        public string Description { get; set; }
        
        // 链接
        public string Website { get; set; }
        public string LinkedInUrl { get; set; }
        public string BlogUrl { get; set; }
        
        // 配置信息
        public string ThreadCount { get; set; }
        
        // 执行账户组
        public string ExecuteGroup { get; set; }
        
        // 运行设置
        public List<ExecutionItem> ExecutionItems { get; set; }
        public decimal Amount { get; internal set; }
        public string ExecuteGroupName { get; internal set; }
        public string ExecuteGroupId { get; internal set; }
        public bool UseProxy { get; internal set; }
        public string? ProxyPool { get; internal set; }
        public ScheduleSettings ScheduleSettings { get; internal set; }
        public string? Status { get; internal set; }

        public Project()
        {
            Id = Guid.NewGuid().ToString();
            Investors = new List<string>();
            ExecutionItems = new List<ExecutionItem>();
        }
    }
    
    public class ExecutionItem
    {
        public string Name { get; set; }
        public bool IsSelected { get; set; }
    }
} 