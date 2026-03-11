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
        CogToolBlock toolBlock { get; }
        Task InitialAsync(string path);
        void RunTool();
    }

    public class VisionService : IVisionService
    {

        private CogImageFileTool _imageFileTool;
        private IEventAggregator _eventAggregator;
        public CogToolBlock toolBlock { get; private set; }

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
                    if (toolBlock!=null)
                        toolBlock.Ran -= toolBlock_Ran;                    
                    toolBlock = (CogToolBlock)CogSerializer.LoadObjectFromFile(vppPath);
                    toolBlock.Ran += toolBlock_Ran;
                    string imagePath = "D:\\锂电项目\\Vm转Vp\\coins.idb";
                    _imageFileTool.Operator.Open(imagePath, CogImageFileModeConstants.Read);

                }
            });
        }

        private void toolBlock_Ran(object sender, EventArgs e)
        {
            UpdateDisplayRecord();
        }

        private void UpdateDisplayRecord()
        {
            if (toolBlock == null) return;
            ICogRecord displayRecord = null;
            if (toolBlock.Tools.Count > 0)
            {
                displayRecord = toolBlock.Tools[0].CreateLastRunRecord();
                if (displayRecord.SubRecords.Count > 0)
                {
                    displayRecord = displayRecord.SubRecords.Count > 1 ? displayRecord.SubRecords[1] : displayRecord.SubRecords[0];
                }
            }
            else
            {
                displayRecord = toolBlock.CreateLastRunRecord();
            }
            _eventAggregator.GetEvent<VisionResultEvent>().Publish(displayRecord);
        }

        public void RunTool()
        {
            if (toolBlock == null) return;
            _imageFileTool.Run();
            ICogImage currentImage = _imageFileTool.OutputImage;
            if (toolBlock.Inputs.Contains("Image"))
            {
                toolBlock.Inputs["Image"].Value = currentImage;
            }
            toolBlock.Run();
        }
    }
}
