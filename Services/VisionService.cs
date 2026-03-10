using aoi_common.Events;
using Cognex.VisionPro;
using Cognex.VisionPro.ImageFile;
using Cognex.VisionPro.ToolBlock;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace aoi_common.Services
{

    public interface IVisionService
    {
        Task InitialAsync(string path);
        void RunTool();
    }

    internal class VisionService : IVisionService
    {
        private CogToolBlock _toolBlock;
        private CogImageFileTool _imageFileTool;
        private IEventAggregator _eventAggregator;

        public VisionService(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _imageFileTool = new CogImageFileTool();
        }
        public async Task InitialAsync(string path)
        {
            await Task.Run(() =>
            {
                string vppPath = "D:\\锂电项目\\Vm转Vp\\TB.vpp";
                if (File.Exists(vppPath))
                {
                    _toolBlock = (CogToolBlock)CogSerializer.LoadObjectFromFile(vppPath);
                    string imagePath = "D:\\锂电项目\\Vm转Vp\\coins.idb";
                    _imageFileTool.Operator.Open(imagePath, CogImageFileModeConstants.Read);

                }
            });
        }

        public void RunTool()
        {
            if (_toolBlock == null) return;
            _imageFileTool.Run();
            ICogImage currentImage = _imageFileTool.OutputImage;
            if (_toolBlock.Inputs.Contains("Image"))
            {
                _toolBlock.Inputs["Image"].Value = currentImage;
            }

            _toolBlock.Run();
            ICogRecord displayRecord = null;
            if (_toolBlock.Tools.Count > 0)
            {
                displayRecord = _toolBlock.Tools[0].CreateLastRunRecord();
                if (displayRecord.SubRecords.Count>0)
                {
                    displayRecord = displayRecord.SubRecords[1];
                }

            }
            else
            {
                displayRecord = _toolBlock.CreateLastRunRecord();
            }

            _eventAggregator.GetEvent<VisionResultEvent>().Publish(displayRecord);

        }
    }
}
