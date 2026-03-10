using aoi_common.Events;
using Cognex.VisionPro;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace aoi_common.ViewModels
{
    public class RunViewModel:BindableBase
    {
        public Action<ICogRecord> OnRecordUpdated;

        public RunViewModel(IEventAggregator eventAggregator)
        {
            eventAggregator.GetEvent<VisionResultEvent>().Subscribe(record => 
            {
                OnRecordUpdated?.Invoke(record);
            },ThreadOption.UIThread);
        }
    }
}
