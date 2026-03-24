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
        void RunToolOnline();
        void RunToolWithImageSource(IImageSource imageSource);
        void RunToolWithImage(ICogImage image);
    }

    public class VisionService : IVisionService
    {
        private bool _isInitialized;
        public bool IsInitialized => _isInitialized;
        private readonly ILogger _logger;
        private CogImageFileTool _imageFileTool;
        private IEventAggregator _eventAggregator;
        private ICameraConfigService _cameraService;
        public CogToolBlock toolBlock { get; private set; }

        public VisionService(IEventAggregator eventAggregator,ICameraConfigService cameraService, ILogger logger)
        {
            _logger = logger;
            _eventAggregator = eventAggregator;
            _cameraService = cameraService;
            //_acqFifo = cameraService.CurrentCogAcqFifoTool.Operator;
            //ICogAcqExposure exposure = _acqFifo.OwnedExposureParams;
            _imageFileTool = new CogImageFileTool();
        }

        public async Task InitialAsync(string path)
        {
            _isInitialized = false;
            _logger.Information("VisionService开始初始化,加载vpp文件");
            await Task.Run(() =>
            {
                if (string.IsNullOrEmpty(path))
                {
                    throw new ArgumentNullException(nameof(path), "ToolBlock文件路径不能为空");
                }
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException(string.Format("ToolBlock文件不存在: {0}", path));
                }
                try
                {
                    if (toolBlock != null)
                        toolBlock.Ran -= toolBlock_Ran;
                    _logger.Debug("正在加载vpp文件: {Path}", path);
                    toolBlock = (CogToolBlock)CogSerializer.LoadObjectFromFile(path);
                    _isInitialized = true;

                    toolBlock.Ran += toolBlock_Ran;
                    _logger.Information("ToolBlock加载成功");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "加载vpp文件失败");
                    _isInitialized = false;
                    throw;
                }
                //string imagePath = "C:\\Users\\xuze\\Desktop\\test\\14184680-贴胶后1贴条胶+面胶.bmp";
                //_imageFileTool.Operator.Open(imagePath, CogImageFileModeConstants.Read);
                ////SetBlobFilter("CogBlobTool1", "Area", 5100, 9100);
            });
        }

        private void toolBlock_Ran(object sender, EventArgs e)
        {
            UpdateDisplayRecord();
        }

        private void UpdateDisplayRecord()
        {
            if (toolBlock == null) return;

            //if (toolBlock.Tools.Count > 0)
            //{
            //    displayRecord = toolBlock.Tools[0].CreateLastRunRecord();
            //    if (displayRecord.SubRecords.Count > 0)
            //    {
            //        displayRecord = displayRecord.SubRecords.Count > 1 ? displayRecord.SubRecords[2] : displayRecord.SubRecords[0];
            //    }
            //}
            //else
            try
            {
                ICogRecord displayRecord = null;
                displayRecord = toolBlock.CreateLastRunRecord().SubRecords[0];
                _eventAggregator.GetEvent<ICogImageDisplayEvent>().Publish(displayRecord);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "更新显示记录失败");
            }

        }

        public void ChangeImagePath(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                _logger.Warning("图像文件路径无效或不存在: {Path}", imagePath);
                return;
            }

            try
            {
                _imageFileTool.Operator.Close();
                _imageFileTool.Operator.Open(imagePath, CogImageFileModeConstants.Read);
                _logger.Information("切换图像: {Path}", imagePath);
                // RunTool();                 // 运行一次，以便界面刷新
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "切换图像失败: {Path}", imagePath);
            }
        }

        public void RunToolOnline()
        {
            if (toolBlock == null)
            {
                _logger.Warning("ToolBlock 未初始化");
                return;
            }

            try
            {
                _logger.Information("【在线模式】运行");

                ICogImage currentImage = null;
                if (_cameraService != null && _cameraService.IsReady())
                {
                    _logger.Debug("【在线模式】从相机获取图像");
                    currentImage = _cameraService.CompleteCapture();

                    if (currentImage == null)
                    {
                        _logger.Warning("【在线模式】相机采集失败，尝试使用备用方案");
                        currentImage = TryGetFallbackImage();
                    }
                }
                else
                {
                    _logger.Warning("【在线模式】相机未就绪，使用备用方案");
                    currentImage = TryGetFallbackImage();
                }

                if (currentImage == null)
                {
                    _logger.Error("【在线模式】无法获取输入图像，运行失败");
                    return;
                }

                SetToolBlockInputImage(currentImage);
                toolBlock.Run();

                _logger.Information("【在线模式】完成");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "【在线模式】异常");
            }
            //int num = toolBlock.Tools["CogBlobTool1"].DataBindings.Count;
            //CogBlobTool tool = toolBlock.Tools["CogBlobTool1"] as CogBlobTool ;
            //var items = tool.RunParams.RunTimeMeasures;
            //var db = toolBlock.Tools["CogBlobTool1"].DataBindings[1].DestinationPath; //"RunParams.RunTimeMeasures.Item[0].FilterRangeHigh"
            //var vvv = toolBlock.Tools.Contains(db);
        }

        private ICogImage TryGetFallbackImage()
        {
            try
            {
                _logger.Debug("【备用方案】使用文件工具");
                _imageFileTool.Run();
                ICogImage image = _imageFileTool.OutputImage;

                if (image != null)
                {
                    _logger.Information("【备用方案】成功");
                    return image;
                }

                _logger.Error("【备用方案】失败");
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "【备用方案】异常");
                return null;
            }
        }

        private void SetToolBlockInputImage(ICogImage image)
        {
            if (toolBlock.Inputs.Contains("Image"))
            {
                toolBlock.Inputs["Image"].Value = image;
            }
            else if (toolBlock.Inputs.Contains("IntputImage"))
            {
                toolBlock.Inputs["IntputImage"].Value = image;
            }
        }

        public void RunToolWithImageSource(IImageSource imageSource)
        {
            if (toolBlock == null)
            {
                _logger.Error("ToolBlock未初始化，无法运行");
                throw new InvalidOperationException("ToolBlock未初始化");
            }
            if (imageSource == null)
            {
                _logger.Error("图像源为空");
                throw new ArgumentNullException(nameof(imageSource));
            }

            try
            {
                _logger.Information("开始使用图像源运行ToolBlock，共{Count}张图像", imageSource.TotalCount);
                imageSource.Reset();

                while (imageSource.HasNext())
                {
                    ICogImage currentImage = imageSource.GetNext();
                    string imageName = imageSource.GetCurrentImageName();

                    if (toolBlock.Inputs.Contains("IntputImage"))
                    {
                        toolBlock.Inputs["IntputImage"].Value = currentImage;
                    }

                    toolBlock.Run();
                    _logger.Debug("处理完成: {ImageName} [{Current}/{Total}]",
                        imageName, imageSource.CurrentIndex, imageSource.TotalCount);
                }

                _logger.Information("图像源处理完成");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "使用图像源运行ToolBlock失败");
                throw;
            }
        }

        public void RunToolWithImage(ICogImage image)
        {
            if (toolBlock == null)
            {
                _logger.Error("ToolBlock未初始化，无法运行");
                throw new InvalidOperationException("ToolBlock未初始化");
            }

            if (image == null)
            {
                _logger.Error("输入图像为空");
                throw new ArgumentNullException(nameof(image));
            }

            try
            {
                if (toolBlock.Inputs.Contains("IntputImage"))
                {
                    toolBlock.Inputs["IntputImage"].Value = image;
                }

                toolBlock.Run();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "运行ToolBlock失败");
                throw;
            }
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
