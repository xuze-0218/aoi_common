using aoi_common.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace aoi_common.Common
{
    public class UiLogSink : ILogEventSink
    {
        public static ObservableCollection<LogEventModel> LogCollection = new ObservableCollection<LogEventModel>();
        private const int MaxLogCount = 100;
        public void Emit(LogEvent logEvent)
        {
            var message = logEvent.RenderMessage();
            var level = logEvent.Level.ToString();

            string color;
            switch (logEvent.Level)
            {
                case LogEventLevel.Fatal:
                case LogEventLevel.Error:
                    color = "Red";
                    break;
                case LogEventLevel.Warning:
                    color = "Yellow";
                    break;
                case LogEventLevel.Debug:
                    color = "Gray";
                    break;
                default:
                    color = "Green";
                    break;
            }

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (LogCollection.Count >= MaxLogCount) LogCollection.RemoveAt(LogCollection.Count - 1);

                LogCollection.Insert(0, new LogEventModel
                {
                    Timestamp = logEvent.Timestamp.ToString("HH:mm:ss.fff"),
                    Level = level,
                    Message = $"[{message}] {logEvent.RenderMessage()}",
                    Color = color
                });
            }));
        }
    }
}
