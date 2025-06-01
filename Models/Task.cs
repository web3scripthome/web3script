using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace web3script.Models
{
    public class Task
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Info { get; set; }
        public string ProjectName { get; set; }
        public string GroupName { get; set; }
        public TaskStatus Status { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int Progress { get; set; }
        public List<TaskExecutionRecord> ExecutionRecords { get; set; }
        public string ProxyPoolId { get; set; }
        public decimal Amount { get; set; }
        public bool UseProxy { get; set; }
        public ScheduleSettings ScheduleSettings { get; set; }
        public int LastProcessedIndex { get; set; }
        public int ThreadCount { get; set; } = 5;
        
        public Task()
        {
            Id = Guid.NewGuid().ToString();
            CreateTime = DateTime.Now;
            Status = TaskStatus.Pending;
            ExecutionRecords = new List<TaskExecutionRecord>();
            Progress = 0;
            LastProcessedIndex = 0;
            ThreadCount = 5;
        }
    }

    public class TaskExecutionRecord
    {
        public string Id { get; set; }
        public string WalletAddress { get; set; }
        public DateTime OperationTime { get; set; }
        public string Status { get; set; }
        public bool? Success { get; set; }
        public string ErrorMessage { get; set; }
        public string TransactionHash { get; set; }
        public string TaskName { get; set; }
        public string ProjectName { get; set; }
        
        public TaskExecutionRecord()
        {
            Id = Guid.NewGuid().ToString();
            OperationTime = DateTime.Now;
        }
    }

    public class ScheduleSettings
    {
        public bool IsScheduled { get; set; }
        public DateTime? ScheduledTime { get; set; }
        public bool IsRecurring { get; set; }
        public RecurrenceType RecurrenceType { get; set; }
        public int RecurrenceInterval { get; set; }
    }

    public enum RecurrenceType
    {
        Hourly,
        Daily,
        Weekly,
        Monthly
    }

    public enum TaskStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        Cancelled,
        Paused
    }
} 