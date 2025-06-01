using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Newtonsoft.Json;

namespace web3script.Data
{
    public static class ExecutionStore
    {
        
        private static Dictionary<string, ObservableCollection<ExecutionRecord>> _taskRecords = new Dictionary<string, ObservableCollection<ExecutionRecord>>();
        private static readonly string _recordsFilePath = "execution_details.json";

         
        public static ObservableCollection<ExecutionRecord> GetRecords(string taskId)
        {
            if (!_taskRecords.ContainsKey(taskId))
            {
                _taskRecords[taskId] = new ObservableCollection<ExecutionRecord>();
            }
            return _taskRecords[taskId];
        }

        
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
        
       
        public static void SaveRecords()
        {
            try
            {
               
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
        public string TaskId { get; set; }  
        public string WalletAddress { get; set; }
        public DateTime OperationTime { get; set; }
        public string Status { get; set; }
        public bool? Success { get; set; }
        public string ErrorMessage { get; set; }
        public string TransactionHash { get; set; }
       
        public string TaskName { get; set; }
        
       
        public string ProjectName { get; set; }
        
         
        public int WalletIndex { get; set; }
        
     
        public string ActionName { get; set; }
    }
}
