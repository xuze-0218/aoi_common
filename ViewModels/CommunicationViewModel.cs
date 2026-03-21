using aoi_common.Models;
using aoi_common.Services;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using Serilog;
using System;
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
        private readonly ICommunicationService _service;
        private readonly ILogger _logger;

        public ObservableCollection<string> Logs { get; } = new ObservableCollection<string>();

        public ObservableCollection<CommunicationMessage> SentMessages { get; } = new ObservableCollection<CommunicationMessage>();
        public ObservableCollection<CommunicationMessage> ReceivedMessages { get; } = new ObservableCollection<CommunicationMessage>();

        public string Title => "通讯配置";

        private string _ip = "127.0.0.1";
        public string Ip { get => _ip; set => SetProperty(ref _ip, value); }

        private int _port = 5000;
        public int Port { get => _port; set => SetProperty(ref _port, value); }

        public CommProtocol SelectedProtocol { get; set; } = CommProtocol.TCP;
        public CommRole SelectedRole { get; set; } = CommRole.Server;

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

        public CommunicationViewModel(ICommunicationService service, ILogger logger)
        {
            _logger = logger;
            _service = service;

            ConnectCommand = new DelegateCommand(
             () =>
             {
                 _logger.Debug("用户点击连接按钮");
                 _service.Start(SelectedProtocol, SelectedRole, Ip, Port);
                 //IsConnected = _service.IsActive;
                 //UpdateStatusMessage();
                 //UpdateCommandsCanExecute();
             },
             () => !IsConnected);
            DisconnectCommand = new DelegateCommand(
                () =>
                {
                    _logger.Debug("断开连接按钮");
                    _service.Stop();
                    //IsConnected = _service.IsActive;
                    //UpdateStatusMessage();
                    //UpdateCommandsCanExecute();
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
                ReceivedMessages.Clear();
                _logger.Information("日志已清空");
            });
            SaveConfigCommand = new DelegateCommand(() =>
            {
                _logger.Information("通讯配置已保存: {Protocol} {Role} {IP}:{Port}",
                    SelectedProtocol, SelectedRole, Ip, Port);
                StatusMessage = "配置已保存";
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


            // 订阅日志消息事件
            _service.LogMessage += m => App.Current.Dispatcher.Invoke(() =>
            {
                Logs.Insert(0, $"{DateTime.Now:HH:mm:ss} {m}");
            });

            // 订阅接收消息事件
            _service.MessageReceived += (s, m) => HandleMessage(s, m);

           
          

            bool currentState = _service.IsActive;
            IsConnected = currentState;
            UpdateStatusMessage();
            UpdateCommandsCanExecute();
            _logger.Information("ViewModel 初始化，当前连接状态: {IsConnected}", currentState);

           
            _logger.Information("通讯配置界面已打开");
        }

        private void HandleMessage(string source, string message)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                // 添加到接收消息列表
                ReceivedMessages.Add(new CommunicationMessage
                {
                    Sender = source,
                    Content = message,
                    Timestamp = DateTime.Now
                });

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
            StatusMessage = IsConnected ? "✓ 已连接" : "✗ 未连接";
        }

        public bool CanCloseDialog() => true;
        public void OnDialogClosed() => _service.Stop();
        public void OnDialogOpened(IDialogParameters parameters) { }
    }
}