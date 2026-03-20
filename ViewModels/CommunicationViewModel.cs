using aoi_common.Models;
using aoi_common.Services;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace aoi_common.ViewModels
{
    public class CommunicationViewModel : BindableBase, IDialogAware
    {
        private readonly ICommunicationService _service;
        private readonly ILogger _logger;
        public ObservableCollection<string> Logs { get; } = new ObservableCollection<string>();
        public string Title => "通讯配置";
        private string _ip = "127.0.0.1";
        public string Ip { get => _ip; set => SetProperty(ref _ip, value); }
        private int _port = 2000;
        public int Port { get => _port; set => SetProperty(ref _port, value); }
        private bool _isConnected;
        public bool IsConnected { get => _isConnected; set => SetProperty(ref _isConnected, value); }
        private string _messageInput;
        public string MessageInput
        {
            get => _messageInput;
            set => SetProperty(ref _messageInput, value);
        }
        public event Action<IDialogResult> RequestClose;
        public CommProtocol SelectedProtocol { get; set; } = CommProtocol.TCP;
        public CommRole SelectedRole { get; set; } = CommRole.Server;

        public DelegateCommand StartCommand { get; }
        public DelegateCommand StopCommand { get; }
        public DelegateCommand SendCommand { get; }
        public DelegateCommand ClearCommand { get; }

        public CommunicationViewModel(ICommunicationService service, ILogger logger)
        {
            _logger = logger;
            _service = service;
            IsConnected=_service.IsActive;
            _service.LogMessage += m => App.Current.Dispatcher.Invoke(() => Logs.Insert(0, $"{DateTime.Now:HH:mm:ss} {m}"));
            _service.MessageReceived += (s, m) => HandleMessage(s, m);

            StartCommand = new DelegateCommand(
                () =>
                {
                    _service.Start(SelectedProtocol, SelectedRole, Ip, Port);
                    IsConnected = _service.IsActive;
                    UpdateCommandsCanExecute();
                },
                () => !IsConnected);

            StopCommand = new DelegateCommand(
                () =>
                {
                    _service.Stop();
                    IsConnected = _service.IsActive;
                    UpdateCommandsCanExecute();
                },
                () => IsConnected);

            SendCommand = new DelegateCommand(
                async () =>
                {
                    await _service.SendAsync(MessageInput);
                    MessageInput = string.Empty;
                },
                () => IsConnected && !string.IsNullOrWhiteSpace(MessageInput))
                .ObservesProperty(() => MessageInput);

            ClearCommand = new DelegateCommand(() =>
            {
                Logs.Clear();
                _logger.Information("日志已清空");
            });
            this.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(IsConnected))
                {
                    UpdateCommandsCanExecute();
                }
            };

            _logger.Information("通讯配置界面已打开");
        }

        private void HandleMessage(string source, string message)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                Logs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] 来自 [{source}]: {message}");
                _logger.Information("【接收】{Source}: {Message}", source, message);
            });
        }
        private void UpdateCommandsCanExecute()
        {
            StartCommand.RaiseCanExecuteChanged();
            StopCommand.RaiseCanExecuteChanged();
            SendCommand.RaiseCanExecuteChanged();
        }
        public bool CanCloseDialog() => true;


        public void OnDialogClosed() => _service.Stop();


        public void OnDialogOpened(IDialogParameters parameters) { }
    }
}
