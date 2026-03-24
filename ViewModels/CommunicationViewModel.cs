using aoi_common.Models;
using aoi_common.Services;
using DryIoc;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using Serilog;
using System;
using System.Linq;
using System.Collections.ObjectModel;

namespace aoi_common.ViewModels
{
    public class CommunicationMessage
    {
        public string Sender { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
        public string DisplayText => $"[{Timestamp:HH:mm:ss}] {Sender}: {Content}";
    }

    public class CommunicationViewModel : BindableBase, IDialogAware
    {
        private readonly IParametersConfigService _configService;
        private readonly ICommunicationService _service;
        private readonly ILogger _logger;

        public ObservableCollection<string> Logs { get; } = new ObservableCollection<string>();

        public ObservableCollection<CommunicationMessage> SentMessages { get; } = new ObservableCollection<CommunicationMessage>();

        private string _receivedMessagesText;
        public string ReceivedMessagesText
        {
            get => _receivedMessagesText;
            set => SetProperty(ref _receivedMessagesText, value);
        }

        public string Title => "通讯配置";

        private string _ip = "127.0.0.1";
        public string Ip { get => _ip; set => SetProperty(ref _ip, value); }
        private int _port = 5000;
        public int Port { get => _port; set => SetProperty(ref _port, value); }
        private CommProtocol _selectedProtocol = CommProtocol.TCP;
        public CommProtocol SelectedProtocol
        {
            get => _selectedProtocol;
            set => SetProperty(ref _selectedProtocol, value);
        }
        private CommRole _selectedRole = CommRole.Server;
        public CommRole SelectedRole
        {
            get => _selectedRole;
            set => SetProperty(ref _selectedRole, value);
        }

        private bool _isConnected;
        public bool IsConnected { get => _isConnected; set => SetProperty(ref _isConnected, value); }

        private string _statusMessage = "未连接";
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        private string _messageInput;
        public string MessageInput { get => _messageInput; set => SetProperty(ref _messageInput, value); }

        public DelegateCommand SendCommand { get; }
        public DelegateCommand ClearCommand { get; }
        public DelegateCommand ConnectCommand { get; }
        public DelegateCommand DisconnectCommand { get; }
        public DelegateCommand SaveConfigCommand { get; }

        public event Action<IDialogResult> RequestClose;

        public CommunicationViewModel(ICommunicationService service, ILogger logger, IParametersConfigService configService)
        {
            _logger = logger;
            _service = service;
            _configService = configService;
            LoadConfigToUI();
            ConnectCommand = new DelegateCommand(
             () =>
             {
                 _logger.Debug("用户点击连接按钮");
                 _service.Start(SelectedProtocol, SelectedRole, Ip, Port);
             },
             () => !IsConnected);
            DisconnectCommand = new DelegateCommand(
                () =>
                {
                    _logger.Debug("断开连接按钮");
                    _service.Stop();
                },
                () => IsConnected);
            SendCommand = new DelegateCommand(
                async () =>
                {
                    await _service.SendAsync(MessageInput);
                    SentMessages.Add(new CommunicationMessage
                    {
                        Sender = "本地",
                        Content = MessageInput,
                        Timestamp = DateTime.Now
                    });

                    MessageInput = string.Empty;
                },
                () => IsConnected && !string.IsNullOrWhiteSpace(MessageInput))
                .ObservesProperty(() => MessageInput);
            ClearCommand = new DelegateCommand(() =>
            {
                Logs.Clear();
                SentMessages.Clear();
                //ReceivedMessages.Clear();
                ReceivedMessagesText = string.Empty;
                _logger.Information("日志已清空");
            });
            SaveConfigCommand = new DelegateCommand(() =>
            {
                try
                {
                    _configService.UpdateParam("Communication", "Protocol", SelectedProtocol.ToString());
                    _configService.UpdateParam("Communication", "Role", SelectedRole.ToString());
                    _configService.UpdateParam("Communication", "IP", Ip);
                    _configService.UpdateParam("Communication", "Port", Port.ToString(), ParamOutputType.INT);
                    if (_configService.SaveConfig())
                    {
                        _logger.Information("通讯配置已保存: {Protocol} {Role} {IP}:{Port}",
                        SelectedProtocol, SelectedRole, Ip, Port);
                        StatusMessage = "配置已保存";
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "保存配置时发生错误");
                    StatusMessage = "保存失败";
                }
            });

            _service.ConnectionStatusChanged += isConnected =>
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    IsConnected = isConnected;
                    UpdateStatusMessage();
                    UpdateCommandsCanExecute();
                });
            };

            _service.LogMessage += m => App.Current.Dispatcher.Invoke(() =>
            {
                Logs.Insert(0, $"{DateTime.Now:HH:mm:ss} {m}");
            });

            _service.MessageReceived += (s, m) => HandleMessage(s, m);
            bool currentState = _service.IsActive;
            IsConnected = currentState;
            UpdateStatusMessage();
            UpdateCommandsCanExecute();
            _logger.Information("ViewModel 初始化，当前连接状态: {IsConnected}", currentState);
            _logger.Debug("通讯配置界面已打开");
           
        }

        private void LoadConfigToUI()
        {
            Ip = _configService.GetString("Communication", "IP", "127.0.0.1");
            Port = _configService.GetInt("Communication", "Port", 5000);

            string protocolStr = _configService.GetString("Communication", "Protocol", "TCP");
            if (Enum.TryParse<CommProtocol>(protocolStr, out var protocol))
            {
                SelectedProtocol = protocol;
            }
            string roleStr = _configService.GetString("Communication", "Role", "Server");
            if (Enum.TryParse<CommRole>(roleStr, out var role))
            {
                SelectedRole = role; 
            }
        }

        private void HandleMessage(string source, string message)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                var newMessage = new CommunicationMessage
                {
                    Sender = source,
                    Content = message,
                    Timestamp = DateTime.Now
                };

                //ReceivedMessages.Add(newMessage);
                string line = newMessage.DisplayText + Environment.NewLine;
                ReceivedMessagesText += line;

                Logs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] 来自 [{source}]: {message}");
                _logger.Information("【接收】{Source}: {Message}", source, message);
            });
        }
        
        private void UpdateCommandsCanExecute()
        {
            ConnectCommand.RaiseCanExecuteChanged();
            DisconnectCommand.RaiseCanExecuteChanged();
            SendCommand.RaiseCanExecuteChanged();
        }

        private void UpdateStatusMessage()
        {
            StatusMessage = IsConnected ? "已连接" : "未连接";
        }
        
        public bool CanCloseDialog() => true;

        public void OnDialogClosed() { } /*=> _service.Stop();*/

        public void OnDialogOpened(IDialogParameters parameters) { }
    }
}