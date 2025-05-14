using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Newtonsoft.Json;

namespace web3script.Data
{
    public static class ExecutionStore
    {
        // 使用字典来存储不同任务的执行记录，以TaskId为键
        private static Dictionary<string, ObservableCollection<ExecutionRecord>> _taskRecords = new Dictionary<string, ObservableCollection<ExecutionRecord>>();
        private static readonly string _recordsFilePath = "execution_details.json";

        // 获取指定任务的执行记录集合
        public static ObservableCollection<ExecutionRecord> GetRecords(string taskId)
        {
            if (!_taskRecords.ContainsKey(taskId))
            {
                _taskRecords[taskId] = new ObservableCollection<ExecutionRecord>();
            }
            return _taskRecords[taskId];
        }

        // 清除指定任务的执行记录
        public static void ClearRecords(string taskId)
        {
            if (_taskRecords.ContainsKey(taskId))
            {
                _taskRecords[taskId].Clear();
            }
            else
            {
                _taskRecords[taskId] = new ObservableCollection<ExecutionRecord>();
            }
        }
        
        // 保存所有任务的执行记录到文件
        public static void SaveRecords()
        {
            try
            {
                // 将ObservableCollection转换为普通List以便于序列化
                var recordsToSave = new Dictionary<string, List<ExecutionRecord>>();
                
                foreach (var taskEntry in _taskRecords)
                {
                    recordsToSave[taskEntry.Key] = new List<ExecutionRecord>(taskEntry.Value);
                }
                
                string json = JsonConvert.SerializeObject(recordsToSave, Formatting.Indented);
                File.WriteAllText(_recordsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"保存执行记录详情失败: {ex.Message}", "错误", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
        
        // 从文件加载所有任务的执行记录
        public static void LoadRecords()
        {
            try
            {
                if (File.Exists(_recordsFilePath))
                {
                    string json = File.ReadAllText(_recordsFilePath);
                    var loadedRecords = JsonConvert.DeserializeObject<Dictionary<string, List<ExecutionRecord>>>(json);
                    
                    if (loadedRecords != null)
                    {
                        _taskRecords.Clear();
                        
                        foreach (var taskEntry in loadedRecords)
                        {
                            var observableRecords = new ObservableCollection<ExecutionRecord>();
                            foreach (var record in taskEntry.Value)
                            {
                                observableRecords.Add(record);
                            }
                            _taskRecords[taskEntry.Key] = observableRecords;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"加载执行记录详情失败: {ex.Message}", "错误", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    public class ExecutionRecord
    {
        public string TaskId { get; set; } // 用于区分哪个任务的记录
        public string WalletAddress { get; set; }
        public DateTime OperationTime { get; set; }
        public string Status { get; set; }
        public bool? Success { get; set; }
        public string ErrorMessage { get; set; }
        public string TransactionHash { get; set; }
        
        // 添加任务名称
        public string TaskName { get; set; }
        
        // 添加项目名称
        public string ProjectName { get; set; }
        
        // 添加钱包位置信息，用于继续执行时的索引判断
        public int WalletIndex { get; set; }
        
        // 添加操作信息，记录具体的执行项
        public string ActionName { get; set; }
    }
}

