using web3script.Models;
using web3script.ucontrols;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;
using TaskStatus = web3script.Models.TaskStatus;

namespace web3script.ViewModels
{
    public class TaskViewModel : INotifyPropertyChanged
    {
        private string _id;
        private string _name;
        private string _projectName;
        private string _groupName;
        private TaskStatus _status;
        private string _statusText;
        private DateTime _createTime;
        private DateTime? _startTime;
        private DateTime? _endTime;
        private int _progress;
        private decimal _amount;
        private bool _useProxy;
        private string _proxyPoolId;
        private bool _isSelected;
        private bool _isScheduled;
        private DateTime? _scheduledTime;
        private bool _isRecurring;
        private RecurrenceType _recurrenceType;
        private int _recurrenceInterval;

        public string Id
        {
            get => _id;
            set
            {
                if (_id != value)
                {
                    _id = value;
                    OnPropertyChanged(nameof(Id));
                }
            }
        }

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

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

        public string GroupName
        {
            get => _groupName;
            set
            {
                if (_groupName != value)
                {
                    _groupName = value;
                    OnPropertyChanged(nameof(GroupName));
                }
            }
        }

        public TaskStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(IsRunning));
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public DateTime CreateTime
        {
            get => _createTime;
            set
            {
                if (_createTime != value)
                {
                    _createTime = value;
                    OnPropertyChanged(nameof(CreateTime));
                }
            }
        }

        public DateTime? StartTime
        {
            get => _startTime;
            set
            {
                if (_startTime != value)
                {
                    _startTime = value;
                    OnPropertyChanged(nameof(StartTime));
                }
            }
        }

        public DateTime? EndTime
        {
            get => _endTime;
            set
            {
                if (_endTime != value)
                {
                    _endTime = value;
                    OnPropertyChanged(nameof(EndTime));
                }
            }
        }

        public int Progress
        {
            get => _progress;
            set
            {
                if (_progress != value)
                {
                    _progress = value;
                    OnPropertyChanged(nameof(Progress));
                }
            }
        }

        public decimal Amount
        {
            get => _amount;
            set
            {
                if (_amount != value)
                {
                    _amount = value;
                    OnPropertyChanged(nameof(Amount));
                }
            }
        }

        public bool UseProxy
        {
            get => _useProxy;
            set
            {
                if (_useProxy != value)
                {
                    _useProxy = value;
                    OnPropertyChanged(nameof(UseProxy));
                }
            }
        }

        public string ProxyPoolId
        {
            get => _proxyPoolId;
            set
            {
                if (_proxyPoolId != value)
                {
                    _proxyPoolId = value;
                    OnPropertyChanged(nameof(ProxyPoolId));
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public bool IsScheduled
        {
            get => _isScheduled;
            set
            {
                if (_isScheduled != value)
                {
                    _isScheduled = value;
                    OnPropertyChanged(nameof(IsScheduled));
                }
            }
        }

        public DateTime? ScheduledTime
        {
            get => _scheduledTime;
            set
            {
                if (_scheduledTime != value)
                {
                    _scheduledTime = value;
                    OnPropertyChanged(nameof(ScheduledTime));
                }
            }
        }

        public bool IsRecurring
        {
            get => _isRecurring;
            set
            {
                if (_isRecurring != value)
                {
                    _isRecurring = value;
                    OnPropertyChanged(nameof(IsRecurring));
                }
            }
        }

        public RecurrenceType RecurrenceType
        {
            get => _recurrenceType;
            set
            {
                if (_recurrenceType != value)
                {
                    _recurrenceType = value;
                    OnPropertyChanged(nameof(RecurrenceType));
                }
            }
        }

        public int RecurrenceInterval
        {
            get => _recurrenceInterval;
            set
            {
                if (_recurrenceInterval != value)
                {
                    _recurrenceInterval = value;
                    OnPropertyChanged(nameof(RecurrenceInterval));
                }
            }
        }

        public bool IsRunning => Status == TaskStatus.Running;

        public List<TaskExecutionRecord> ExecutionRecords { get; set; } = new List<TaskExecutionRecord>();

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // 从模型创建ViewModel
        public static TaskViewModel FromModel(Models.Task task)
        {
            return new TaskViewModel
            {
                Id = task.Id,
                Name = task.Name,
                ProjectName = task.ProjectName,
                GroupName = task.GroupName,
                Status = task.Status,
                StatusText = GetStatusText(task.Status),
                CreateTime = task.CreateTime,
                StartTime = task.StartTime,
                EndTime = task.EndTime,
                Progress = task.Progress,
                Amount = task.Amount,
                UseProxy = task.UseProxy,
                ProxyPoolId = task.ProxyPoolId,
                IsSelected = false,
                ExecutionRecords = task.ExecutionRecords,
                IsScheduled = task.ScheduleSettings?.IsScheduled ?? false,
                ScheduledTime = task.ScheduleSettings?.ScheduledTime,
                IsRecurring = task.ScheduleSettings?.IsRecurring ?? false,
                RecurrenceType = task.ScheduleSettings?.RecurrenceType ?? RecurrenceType.Daily,
                RecurrenceInterval = task.ScheduleSettings?.RecurrenceInterval ?? 1
            };
        }

        // 转换为模型
        public Models.Task ToModel()
        {
            var task = new Models.Task
            {
                Id = Id,
                Name = Name,
                ProjectName = ProjectName,
                GroupName = GroupName,
                Status = Status,
                CreateTime = CreateTime,
                StartTime = StartTime,
                EndTime = EndTime,
                Progress = Progress,
                Amount = Amount,
                UseProxy = UseProxy,
                ProxyPoolId = ProxyPoolId,
                ExecutionRecords = ExecutionRecords
            };

            if (IsScheduled)
            {
                task.ScheduleSettings = new ScheduleSettings
                {
                    IsScheduled = IsScheduled,
                    ScheduledTime = ScheduledTime,
                    IsRecurring = IsRecurring,
                    RecurrenceType = RecurrenceType,
                    RecurrenceInterval = RecurrenceInterval
                };
            }

            return task;
        }

        private static string GetStatusText(TaskStatus status)
        {
            switch (status)
            {
                case TaskStatus.Pending:
                    return "等待执行";
                case TaskStatus.Running:
                    return "正在运行";
                case TaskStatus.Completed:
                    return "已完成";
                case TaskStatus.Failed:
                    return "失败";
                case TaskStatus.Cancelled:
                    return "已取消";
                case TaskStatus.Paused:
                    return "已暂停";
                default:
                    return "未知";
            }
        }
    }
} 