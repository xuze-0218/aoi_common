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

namespace aoi_common.ViewModels
{
    public class CommunicationViewModel:BindableBase, IDialogAware
    {
        private readonly ICommunicationService _service;
        private readonly ILogger _logger;
        public ObservableCollection<string> Logs { get; } = new ObservableCollection<string>();

        private string _ip = "127.0.0.1";
        public string Ip { get => _ip; set => SetProperty(ref _ip, value); }

        private int _port = 2000;

        public event Action<IDialogResult> RequestClose;

        public int Port { get => _port; set => SetProperty(ref _port, value); }

        public CommProtocol SelectedProtocol { get; set; } = CommProtocol.TCP;
        public CommRole SelectedRole { get; set; } = CommRole.Server;

        public DelegateCommand StartCommand { get; }
        public DelegateCommand StopCommand { get; }

        public string Title =>"通讯配置";

        public CommunicationViewModel(ICommunicationService service)
        {
            _service = service;
            _service.LogMessage += m => App.Current.Dispatcher.Invoke(() => Logs.Insert(0, $"{DateTime.Now:HH:mm:ss} {m}"));
            _service.MessageReceived += (s, m) => HandleMessage(s, m);

            StartCommand = new DelegateCommand(() => _service.Start(SelectedProtocol, SelectedRole, Ip, Port));
            StopCommand = new DelegateCommand(() => _service.Stop());
        }

        private void HandleMessage(string source, string msg)
        {
            App.Current.Dispatcher.Invoke(() => Logs.Insert(0, $"收到来自 [{source}]: {msg}"));
        }

        public bool CanCloseDialog()=>true;


        public void OnDialogClosed()
        {
           
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
        }
    }
}
