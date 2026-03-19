using aoi_common.Events;
using Cognex.VisionPro;
using Cognex.VisionPro.ImageFile;
using Cognex.VisionPro.ToolBlock;
using Cognex.VisionPro.Blob;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cognex.VisionPro.PMAlign;
using Serilog;

namespace aoi_common.Services
{

    public interface IVisionService
    {
        bool IsInitialized { get; }
        CogToolBlock toolBlock { get; }
        Task InitialAsync(string path);
        void SetBlobFilter(string blobToolName, string measureType, double min, double max);
        void ChangeImagePath(string imagePath);
        void RunTool();
    }

    public class VisionService : IVisionService
    {
        private bool _isInitialized;
        public bool IsInitialized => _isInitialized;
        private readonly ILogger _logger;
        private CogImageFileTool _imageFileTool;
        private IEventAggregator _eventAggregator;
        public CogToolBlock toolBlock { get; private set; }

        public VisionService(IEventAggregator eventAggregator, ILogger logger)
        {
            _logger = logger;
            _eventAggregator = eventAggregator;
            _imageFileTool = new CogImageFileTool();
        }

        public async Task InitialAsync(string path)
        {
            _isInitialized = false;
            _logger.Information("开始初始化...");
            await Task.Run(() =>
            {
                //string vppPath = "C:\\Users\\xuze\\Desktop\\testvpp.vpp";
                if (File.Exists(path))
                {
                    if (toolBlock != null)
                        toolBlock.Ran -= toolBlock_Ran;
                    Log.Warning("Vpp正在初始化，请等待");
                    toolBlock = (CogToolBlock)CogSerializer.LoadObjectFromFile(path);
                    _isInitialized =true;
             
                    toolBlock.Ran += toolBlock_Ran;
                    string imagePath = "C:\\Users\\xuze\\Desktop\\test\\14184680-贴胶后1贴条胶+面胶.bmp";
                    _imageFileTool.Operator.Open(imagePath, CogImageFileModeConstants.Read);
                    //SetBlobFilter("CogBlobTool1", "Area", 5100, 9100);

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
            //if (toolBlock.Tools.Count > 0)
            //{
            //    displayRecord = toolBlock.Tools[0].CreateLastRunRecord();
            //    if (displayRecord.SubRecords.Count > 0)
            //    {
            //        displayRecord = displayRecord.SubRecords.Count > 1 ? displayRecord.SubRecords[2] : displayRecord.SubRecords[0];
            //    }
            //}
            //else
            {
                displayRecord = toolBlock.CreateLastRunRecord().SubRecords[0];
            }
            _eventAggregator.GetEvent<VisionResultEvent>().Publish(displayRecord);
        }

        public void ChangeImagePath(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath)) return;

            try
            {
                _imageFileTool.Operator.Close();
                _imageFileTool.Operator.Open(imagePath, CogImageFileModeConstants.Read);
                // RunTool();                 // 运行一次，以便界面刷新
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "检测过程发生崩溃");
                System.Diagnostics.Debug.WriteLine($"切换图片失败: {ex.Message}");
            }
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

            //int num = toolBlock.Tools["CogBlobTool1"].DataBindings.Count;

            //CogBlobTool tool = toolBlock.Tools["CogBlobTool1"] as CogBlobTool ;
            //var items = tool.RunParams.RunTimeMeasures;
            //var db = toolBlock.Tools["CogBlobTool1"].DataBindings[1].DestinationPath; //"RunParams.RunTimeMeasures.Item[0].FilterRangeHigh"
            //var vvv = toolBlock.Tools.Contains(db);
            toolBlock.Run();
        }

        public void SetBlobFilter(string blobToolName, string measureType, double min, double max)
        {
            CogPMAlignTool pMAlignTool = toolBlock.Tools[blobToolName] as CogPMAlignTool;
            //pMAlignTool.RunParams.ApproximateNumberToFind
            var blobTool = toolBlock.Tools[blobToolName] as CogBlobTool;
            //blobTool.RunParams.RegionMode
            //pMAlignTool.RunParams.EdgeThreshold;
            //pMAlignTool.RunParams.
            if (blobTool == null) return;
            var measures = blobTool.RunParams.RunTimeMeasures;

            for (int i = 0; i < measures.Count; i++)
            {
                // 匹配 Measure 属性，例如 "Area"
                if (measures[i].Measure.ToString().Contains(measureType))
                {
                    measures[i].FilterRangeLow = min;
                    measures[i].FilterRangeHigh = max;
                    measures[i].Mode = CogBlobMeasureModeConstants.Filter; // 确保开启了过滤模式
                    break;
                }
            }
        }
    }
}
