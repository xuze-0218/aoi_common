using aoi_common.Events;
using Cognex.VisionPro;
using Cognex.VisionPro.ImageFile;
using Cognex.VisionPro.ToolBlock;
using Cognex.VisionPro.Blob;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Cognex.VisionPro.PMAlign;
using Serilog;

namespace aoi_common.Services
{

    /// <summary>
    /// ToolBlock服务
    /// </summary>
    public interface IVisionService
    {
        bool IsInitialized { get; }
        CogToolBlock toolBlock { get; }

        Task InitialAsync(string path);
        void SetBlobFilter(string blobToolName, string measureType, double min, double max);
        void ChangeImagePath(string imagePath);
        /// <summary>
        /// 相机触发采集图片后默认触发回调处理函数
        /// </summary>
        void AcquireImage();
        void RunToolWithImageSource(IImageSource imageSource);
        void RunToolWithImage(ICogImage image);
    }

    public class VisionService : IVisionService, IDisposable
    {
        private bool _isInitialized;
        public bool IsInitialized => _isInitialized;
        private readonly ILogger _logger;
        private CogImageFileTool _imageFileTool;
        private IEventAggregator _eventAggregator;
        private ICameraConfigService _cameraService;
        public CogToolBlock toolBlock { get; private set; }

        public VisionService(IEventAggregator eventAggregator, ICameraConfigService cameraService, ILogger logger)
        {
            _logger = logger;
            _eventAggregator = eventAggregator;
            _cameraService = cameraService;
            //_acqFifo = cameraService.CurrentCogAcqFifoTool.Operator;
            //ICogAcqExposure exposure = _acqFifo.OwnedExposureParams;
            _imageFileTool = new CogImageFileTool();
            if (_cameraService != null)
            {
                _cameraService.OnImageCaptured += HandleImageCaptured;
            }
        }


        private void HandleImageCaptured(ICogImage image)
        {
            if (image == null)
            {
                _logger.Warning("接收到空图像");
                return;
            }

            try
            {
                _logger.Debug("接收采集完成事件，开始检测");

                if (toolBlock == null)
                {
                    _logger.Error("ToolBlock未初始化");
                    return;
                }

                SetToolBlockInputImage(image);
                toolBlock.Run();
                _logger.Information("图像检测完成");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "图像检测异常");
            }
        }

        public async Task InitialAsync(string path)
        {
            _isInitialized = false;
            _logger.Debug("VisionService开始初始化,加载vpp文件");
            await Task.Run(() =>
            {
                if (string.IsNullOrEmpty(path))
                {
                    throw new ArgumentNullException(nameof(path), "vpp文件路径不能为空");
                }
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException(string.Format("vpp文件不存在: {0}", path));
                }
                try
                {
                    if (toolBlock != null)
                        toolBlock.Ran -= toolBlock_Ran;
                    _logger.Debug("正在加载vpp文件: {Path}", path);
                    toolBlock = (CogToolBlock)CogSerializer.LoadObjectFromFile(path);
                    _isInitialized = true;

                    toolBlock.Ran += toolBlock_Ran;
                    _logger.Information("vpp加载成功");
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

        /// <summary>
        /// toolblock运行结束
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolBlock_Ran(object sender, EventArgs e)
        {
            PublishToolBlockCompletedEvent();
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
                _logger.Debug("切换图像: {Path}", imagePath);
                // RunTool();                 // 运行一次,界面刷新
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "切换图像失败: {Path}", imagePath);
            }
        }

        public void AcquireImage()
        {
            if (toolBlock == null)
            {
                _logger.Warning("vpp未初始化");
                return;
            }
            try
            {
                _logger.Information("触发拍照");
                if (_cameraService != null && _cameraService.IsReady())
                {
                    _logger.Debug("启动相机采集");
                    _cameraService.StartCapture();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "在线模式异常");
            }
            //int num = toolBlock.Tools["CogBlobTool1"].DataBindings.Count;
            //CogBlobTool tool = toolBlock.Tools["CogBlobTool1"] as CogBlobTool ;
            //var items = tool.RunParams.RunTimeMeasures;
            //var db = toolBlock.Tools["CogBlobTool1"].DataBindings[1].DestinationPath; //"RunParams.RunTimeMeasures.Item[0].FilterRangeHigh"
            //var vvv = toolBlock.Tools.Contains(db);
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
                _logger.Error("vpp未加载，无法运行");
                throw new InvalidOperationException("vpp未加载");
            }
            if (imageSource == null)
            {
                _logger.Error("图像源为空");
                throw new ArgumentNullException(nameof(imageSource));
            }

            try
            {
                _logger.Information("本地测试共{Count}张图像", imageSource.TotalCount);
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
                _logger.Error(ex, "本地测试失败");
                throw;
            }
        }

        public void RunToolWithImage(ICogImage image)
        {
            if (toolBlock == null)
            {
                _logger.Error("vpp未初始化，无法运行");
                throw new InvalidOperationException("vpp未初始化");
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
                _logger.Error(ex, "运行程序失败");
                throw;
            }
        }

        private void PublishToolBlockCompletedEvent()
        {
            try
            {
                if (toolBlock == null)
                {
                    _logger.Error("ToolBlock为空，无法发布完成事件");
                    return;
                }

                var result = new Models.ToolBlockResultModel
                {
                    IsSuccess = true,
                    ToolBlock = toolBlock,
                    Outputs = ExtractToolBlockOutputs(),
                    TimeConsuming = DateTime.Now,
                    DisplayRecord = toolBlock.CreateLastRunRecord(),
                    ErrorMessage = null
                };
                _eventAggregator.GetEvent<ToolBlockCompletedEvent>().Publish(result);
                _logger.Debug("已发布ToolBlockCompletedEvent");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "发布ToolBlock完成事件失败");
            }
        }

        private Dictionary<string, object> ExtractToolBlockOutputs()
        {
            var outputs = new Dictionary<string, object>();
            //try
            //{
            //    if (toolBlock?.Outputs == null) return outputs;

            //    foreach (var key in toolBlock.Outputs)
            //    {
            //        try
            //        {
            //            outputs[key] = toolBlock.Outputs[key]?.Value;
            //        }
            //        catch { }
            //    }
            //}
            //catch (Exception ex)
            //{
            //    _logger.Warning(ex, "提取ToolBlock输出异常");
            //}
            return outputs;
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
                    measures[i].Mode = CogBlobMeasureModeConstants.Filter; // 过滤模式
                    break;
                }
            }
        }


        public void Dispose()
        {
            try
            {
                if (toolBlock != null)
                {
                    toolBlock.Ran -= toolBlock_Ran; 
                    toolBlock.Dispose();
                    toolBlock = null;
                }
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "释放ToolBlock异常");
            }

            try
            {
                if (_cameraService != null)
                {
                    _cameraService.OnImageCaptured -= HandleImageCaptured;
                }
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "释放相机服务异常");
            }

            try
            {
                _imageFileTool?.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "释放ImageFileTool异常");
            }
        }

        ~VisionService()
        {
            Dispose();
        }
    }
}
