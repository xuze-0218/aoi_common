using aoi_common.Models;
using aoi_common.Services;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

namespace aoi_common.ViewModels
{
    public class ProtocolConfigViewModel : BindableBase, IDialogAware
    {
        private readonly IProtocolEngineService _protocolEngine;
        private readonly ICommunicationService _communicationService;
        private readonly ILogger _logger;

        private string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config/ProtocolConfig.json");
        private ObservableCollection<ProtocolField> _inputFields;
        private ObservableCollection<ProtocolField> _outputFields;
        private int _totalLength;
        private string _testRawData = "";
        private string _parseResult = "";
        private string _generatedMessage = "";
        private string _previewMessage = "";
        private string _statusMessage = "就绪";
        private string _templateName = "DefaultTemplate";
        private bool _isListeningPlc = false;
        private string _listenButtonText = "开始监听 PLC";

        public event Action<IDialogResult> RequestClose;

        public ObservableCollection<ProtocolField> InputFields
        {
            get => _inputFields;
            set => SetProperty(ref _inputFields, value);
        }

        public ObservableCollection<ProtocolField> OutputFields
        {
            get => _outputFields;
            set => SetProperty(ref _outputFields, value);
        }

        public int TotalLength
        {
            get => _totalLength;
            set => SetProperty(ref _totalLength, value);
        }

        public string TestRawData
        {
            get => _testRawData;
            set => SetProperty(ref _testRawData, value);
        }

        public string ParseResult
        {
            get => _parseResult;
            set => SetProperty(ref _parseResult, value);
        }

        public string GeneratedMessage
        {
            get => _generatedMessage;
            set => SetProperty(ref _generatedMessage, value);
        }

        public string PreviewMessage
        {
            get => _previewMessage;
            set => SetProperty(ref _previewMessage, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string TemplateName
        {
            get => _templateName;
            set => SetProperty(ref _templateName, value);
        }

        public bool IsListeningPlc
        {
            get => _isListeningPlc;
            set => SetProperty(ref _isListeningPlc, value);
        }

        public string ListenButtonText
        {
            get => _listenButtonText;
            set => SetProperty(ref _listenButtonText, value);
        }

        // 命令
        public DelegateCommand AddInputFieldCommand { get; private set; }
        public DelegateCommand AddOutputFieldCommand { get; private set; }
        public DelegateCommand SortCommand { get; private set; }
        public DelegateCommand ParseTestCommand { get; private set; }
        public DelegateCommand PreviewOutputCommand { get; private set; }
        public DelegateCommand SaveConfigCommand { get; private set; }
        public DelegateCommand LoadConfigCommand { get; private set; }
        public DelegateCommand ToggleListenCommand { get; private set; }

        public string Title => "报文配置调试";


        public ProtocolConfigViewModel(
            IProtocolEngineService protocolEngine,
            ICommunicationService communicationService,
            ILogger logger)
        {
            _protocolEngine = protocolEngine;
            _communicationService = communicationService;
            _logger = logger;

            InitializeCollections();
            InitializeCommands();
            SubscribeToCommunicationEvents();
            LoadConfig();

            _logger?.Information("ProtocolConfigViewModel 已初始化");
        }

        private void InitializeCollections()
        {
            InputFields = new ObservableCollection<ProtocolField>();
            OutputFields = new ObservableCollection<ProtocolField>();

            InputFields.CollectionChanged += (s, e) => OnFieldsChanged();
            OutputFields.CollectionChanged += (s, e) => OnFieldsChanged();
        }

        private void InitializeCommands()
        {
            AddInputFieldCommand = new DelegateCommand(() =>
            {
                var newField = new ProtocolField
                {
                    Name = $"Input_{InputFields.Count}",
                    StartIndex = 0,
                    Length = 4,
                    Description = "新字段"
                };
                InputFields.Add(newField);
                StatusMessage = "已添加输入字段";
            });

            AddOutputFieldCommand = new DelegateCommand(() =>
            {
                var newField = new ProtocolField
                {
                    Index = OutputFields.Count,
                    Name = $"Output_{OutputFields.Count}",
                    Source = FieldSource.Variable,
                    Length = 4,
                    Scale = 1.0,
                    FixedValue = "0"
                };
                OutputFields.Add(newField);
                UpdateTotalLength();
                UpdateOutputPreview();
                StatusMessage = "已添加输出字段";
            });

            SortCommand = new DelegateCommand(() =>
            {
                var sorted = new ObservableCollection<ProtocolField>(
                    OutputFields.OrderBy(f => f.Index).ToList());
                OutputFields.Clear();
                foreach (var field in sorted)
                    OutputFields.Add(field);
                UpdateOutputPreview();
                StatusMessage = "输出字段已按索引重排";
                _logger?.Information("输出字段已重新排序");
            });

            ParseTestCommand = new DelegateCommand(() => ExecuteParseTest());
            PreviewOutputCommand = new DelegateCommand(() => ExecutePreviewOutput());
            SaveConfigCommand = new DelegateCommand(() => ExecuteSaveConfig());
            LoadConfigCommand = new DelegateCommand(() => LoadConfig());
            ToggleListenCommand = new DelegateCommand(() => ToggleListen());
        }

        /// <summary>
        /// 订阅通讯服务事件
        /// </summary>
        private void SubscribeToCommunicationEvents()
        {
            _communicationService.MessageReceived += (sender, message) =>
            {
                if (IsListeningPlc)
                {
                    App.Current?.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            _logger?.Debug("从 {Sender} 接收到消息: {Message}", sender, message);
                            TestRawData = message;
                            ExecuteParseTest();

                            PreviewMessage = "已自动解析来自 PLC 的消息";
                            _logger?.Information("自动解析 PLC 消息成功");
                        }
                        catch (Exception ex)
                        {
                            PreviewMessage = $"自动解析失败: {ex.Message}";
                            _logger?.Error(ex, "自动解析 PLC 消息失败");
                        }
                    });
                }
            };

            _logger?.Debug("已订阅通讯服务事件");
        }

        /// <summary>
        ///切换监听 PLC
        /// </summary>
        private void ToggleListen()
        {
            IsListeningPlc = !IsListeningPlc;

            if (IsListeningPlc)
            {
                ListenButtonText = "停止监听 PLC";
                StatusMessage = "正在监听 PLC 消息...";
                PreviewMessage = "已开始监听，等待 PLC 发送电文";
                _logger?.Information("已开始监听 PLC");
            }
            else
            {
                ListenButtonText = "开始监听 PLC";
                StatusMessage = "已停止监听";
                PreviewMessage = "已停止监听";
                _logger?.Information("已停止监听 PLC");
            }
        }

        /// <summary>
        /// 执行接收电文解析测试
        /// </summary>
        private void ExecuteParseTest()
        {
            try
            {
                if (string.IsNullOrEmpty(TestRawData))
                {
                    PreviewMessage = "请输入原始电文";
                    ParseResult = "";
                    return;
                }

                _protocolEngine.ClearVariables();
                var inputList = InputFields.ToList();
                _protocolEngine.ParseInput(TestRawData, inputList);

                // 构建解析结果显示
                var result = new System.Text.StringBuilder();
                result.AppendLine("解析成功:");
                result.AppendLine();
                foreach (var field in inputList)
                {
                    string value = _protocolEngine.GetVariable(field.Name);
                    result.AppendLine($"{field.Name}:");
                    result.AppendLine($"  值: {value}");
                    if (!string.IsNullOrEmpty(field.Description))
                        result.AppendLine($"  说明: {field.Description}");
                    result.AppendLine();
                }

                ParseResult = result.ToString();
                if (!IsListeningPlc) 
                    PreviewMessage = "电文解析完成";
                StatusMessage = "解析测试成功";
                UpdateOutputPreview();
                _logger?.Information("电文解析测试完成");
            }
            catch (Exception ex)
            {
                PreviewMessage = $"解析异常: {ex.Message}";
                ParseResult = $"错误: {ex.Message}";
                StatusMessage = "解析失败";
                _logger?.Error(ex, "电文解析测试失败");
            }
        }

        /// <summary>
        /// 执行输出电文预览
        /// </summary>
        private void ExecutePreviewOutput()
        {
            try
            {
                UpdateOutputPreview();
                PreviewMessage = "电文生成成功";
                StatusMessage = "输出预览完成";
            }
            catch (Exception ex)
            {
                PreviewMessage = $"生成异常: {ex.Message}";
                GeneratedMessage = $"错误: {ex.Message}";
                StatusMessage = "生成失败";
                _logger?.Error(ex, "输出预览失败");
            }
        }

        /// <summary>
        /// 更新输出预览
        /// </summary>
        private void UpdateOutputPreview()
        {
            try
            {
                var outputList = OutputFields.OrderBy(f => f.Index).ToList();             
                var sb = new StringBuilder();

                foreach (var field in outputList)
                {
                    string fieldContent = "";

                    if (field.Source == FieldSource.Fixed)
                    {
                        fieldContent = field.FixedValue ?? "";
                    }
                    else if (field.Source == FieldSource.Variable)
                    {
                        fieldContent = _protocolEngine.GetVariable(field.Name); 

                        if (!string.IsNullOrEmpty(fieldContent) && field.Scale != 1.0 &&
                            double.TryParse(fieldContent, out double d))
                        {
                            fieldContent = Math.Round(d * field.Scale).ToString();
                        }
                    }
                    else if (field.Source == FieldSource.Padding)
                    {
                        fieldContent = "";
                    }

                    int actualLength = field.GetActualLength(_protocolEngine.VariablePool);
                    string alignedContent = fieldContent.Length > actualLength
                        ? fieldContent.Substring(0, actualLength)
                        : fieldContent.PadLeft(actualLength, '0');

                    field.Preview = alignedContent;
                    sb.Append(alignedContent);

                    _logger?.Debug("预览 {FieldName}: {Preview}", field.Name, field.Preview);
                }

                GeneratedMessage = sb.ToString();
                _logger?.Debug("输出预览已更新，长度: {Length}", GeneratedMessage.Length);
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "更新输出预览异常");
            }
        }

        /// <summary>
        /// 保存配置到文件
        /// </summary>
        private void ExecuteSaveConfig()
        {
            try
            {
                var config = new FullProtocolConfig
                {
                    TemplateName = TemplateName,
                    InputFields = InputFields.ToList(),
                    OutputFields = OutputFields.ToList()
                };

                string dirPath = Path.GetDirectoryName(_configPath);
                if (!Directory.Exists(dirPath))
                    Directory.CreateDirectory(dirPath);

                ConfigStorage.Save(_configPath, config);
                PreviewMessage = "配置已保存";
                StatusMessage = $"已保存到: {_configPath}";
                _logger?.Information("配置已保存");
            }
            catch (Exception ex)
            {
                PreviewMessage = $"保存失败: {ex.Message}";
                StatusMessage = "保存失败";
                _logger?.Error(ex, "保存配置失败");
            }
        }

        /// <summary>
        /// 从文件加载配置
        /// </summary>
        private void LoadConfig()
        {
            try
            {
                var config = ConfigStorage.Load(_configPath);
                InputFields.Clear();
                OutputFields.Clear();

                TemplateName = config.TemplateName;

                foreach (var field in config.InputFields)
                    InputFields.Add(field);

                foreach (var field in config.OutputFields)
                    OutputFields.Add(field);

                UpdateTotalLength();
                PreviewMessage = "配置已加载";
                StatusMessage = "配置已加载";
                _logger?.Information("配置已从文件加载");
            }
            catch (Exception ex)
            {
                PreviewMessage = $"加载失败，使用空白配置";
                StatusMessage = "配置加载失败，已重置";
                _logger?.Warning(ex, "加载配置失败");
            }
        }

        private void OnFieldsChanged()
        {
            UpdateTotalLength();
            SyncFieldValues(InputFields);
            SyncFieldValues(OutputFields);
            UpdateOutputPreview();
        }

        private void SyncFieldValues(ObservableCollection<ProtocolField> fields)
        {
            try
            {
                foreach (var field in fields)
                {
                    if (field.Source == FieldSource.Fixed)
                    {
                        // Fixed 类型：用 Name 的值赋给 FixedValue
                        if (!string.IsNullOrEmpty(field.Name))
                        {
                            field.FixedValue = field.Name;
                            _logger?.Debug("同步 Fixed 字段: Name={Name} → FixedValue={Value}",
                                field.Name, field.FixedValue);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "同步字段值异常");
            }
        }

        private void UpdateTotalLength()
        {
            TotalLength = OutputFields.Sum(f => f.Length);
        }

        public bool CanCloseDialog() => true;

        public void OnDialogClosed()
        {
            if (IsListeningPlc)
            {
                IsListeningPlc = false;
                _logger?.Debug("对话框关闭，已停止监听 PLC");
            }
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            _logger?.Debug("ProtocolConfigView 对话框已打开");
        }
    }
}
