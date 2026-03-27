using aoi_common.Events;
using aoi_common.Models;
using Prism.Events;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace aoi_common.Services
{
    /// <summary>
    /// 检测会话服务接口
    /// </summary>
    public interface IDetectionSessionService
    {
        /// <summary>
        /// 开启在线检测
        /// </summary>
        /// <returns></returns>
        Task<DetectionResultModel> StartDetectionSessionAsync(string plcData);
        /// <summary>
        /// 离线检测
        /// </summary>
        /// <returns></returns>
        Task<DetectionResultModel> StartOfflineDetectionSessionAsync();

        /// <summary>
        /// 当前会话的状态
        /// </summary>
        DetectionSessionState CurrentState { get; }
    }

    /// <summary>
    /// 检测会话的类型
    /// </summary>
    public enum DetectionSessionType
    {
        Online,   // PLC在线模式
        Offline   // 离线本地测试模式
    }



    public class DetectionSessionService : IDetectionSessionService
    {
        private readonly IVisionService _visionService;
        //private readonly IMessageParsingService _messageParsingService;
        private readonly IProtocolEngineService _protocolEngine;
        private readonly ICommunicationService _communicationService;
        private readonly IParametersConfigService _config;
        private readonly IEventAggregator _eventAggregator;
        private readonly ILogger _logger;

        private FullProtocolConfig _protocolConfig;
        private string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config/ProtocolConfig.json");
        private DetectionResultModel _currentSessionResult;
        private string _currentPlcData;
        private TaskCompletionSource<DetectionResultModel> _sessionCompletionSource;
        private DetectionSessionType _currentSessionType = DetectionSessionType.Online;
        private DetectionSessionState _currentState = DetectionSessionState.Idle;
        public DetectionSessionState CurrentState => _currentState;

        public DetectionSessionService(
            IVisionService visionService,
            IProtocolEngineService protocolEngine,
            //IMessageParsingService messageParsingService,
            ICommunicationService communicationService,
            IParametersConfigService config,
            IEventAggregator eventAggregator,
            ILogger logger)
        {
            _visionService = visionService;
            //_messageParsingService = messageParsingService;
            _protocolEngine = protocolEngine;
            _communicationService = communicationService;
            _config = config;
            _eventAggregator = eventAggregator;
            _logger = logger;

            // 订阅ToolBlock完成事件
            _eventAggregator.GetEvent<ToolBlockCompletedEvent>().Subscribe(OnToolBlockCompleted, ThreadOption.BackgroundThread);
            LoadProtocolConfig();
            _logger.Information("检测会话服务已初始化");
        }

        private void LoadProtocolConfig()
        {
            try
            {
                _protocolConfig = ConfigStorage.Load(_configPath);
                _logger.Information("报文配置已加载");
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "加载报文配置失败，使用默认配置");
                _protocolConfig = new FullProtocolConfig();
            }
        }

        /// <summary>
        /// 接收toolblock运行完成返回的ToolBlockResultModel结果
        /// </summary>
        /// <param name="toolBlockResult"></param>
        private void OnToolBlockCompleted(ToolBlockResultModel toolBlockResult)
        {

            bool isValidOnlineSession = _currentState == DetectionSessionState.WaitingForImage &&
                                      _currentSessionType == DetectionSessionType.Online;
            bool isValidOfflineSession = _currentState == DetectionSessionState.WaitingForImageOffline &&
                                        _currentSessionType == DetectionSessionType.Offline;

            if (!isValidOnlineSession && !isValidOfflineSession)
            {
                _logger.Debug("ToolBlock完成但无活跃会话 (State={State}, Type={Type})，忽略此结果",
                    _currentState, _currentSessionType);
                return;
            }

            try
            {
                _currentState = DetectionSessionState.Processing;
                _logger.Information("ToolBlock运行完成，开始处理检测结果");

                if (!toolBlockResult.IsSuccess)
                {
                    _logger.Error("ToolBlock运行失败: {Error}", toolBlockResult.ErrorMessage);
                    _currentSessionResult.IsSuccess = false;
                    _currentSessionResult.Message = $"ToolBlock运行失败: {toolBlockResult.ErrorMessage}";
                    if (_currentSessionType == DetectionSessionType.Online)
                    {
                        SendResultToPlc(_currentSessionResult);
                    }
                    _currentState = DetectionSessionState.Failed;
                    _sessionCompletionSource?.TrySetResult(_currentSessionResult);
                    return;
                }
                //从ToolBlock提取检测数据
                ExtractVppOutputs(toolBlockResult);
                _logger.Debug("已提取ToolBlock输出数据 (共{Count}项)", _currentSessionResult.ToolBlockOutputs.Count);


                // ========== 拼装输出电文 ==========
                BuildResponseMessage(_currentSessionResult);
                
                _currentSessionResult.IsSuccess = true;
                _currentSessionResult.Message = "检测完成";
                _currentState = DetectionSessionState.Idle;


                //发送PLC结果
                if (_currentSessionType == DetectionSessionType.Online)
                {
                    SendResultToPlc(_currentSessionResult);
                    _logger.Debug("已发送PLC结果");
                }
                else
                {
                    _logger.Debug("离线模式，跳过发送PLC结果");
                }
                _currentState = DetectionSessionState.Completed;

                //信号会话完成
                _sessionCompletionSource?.TrySetResult(_currentSessionResult);
                _logger.Information("检测会话标记为完成");
            }
            catch (Exception ex)
            {
                _currentState = DetectionSessionState.Failed;
                _logger.Error(ex, "处理vpp结果异常");
                _sessionCompletionSource?.TrySetException(ex);
            }
        }


       

        public async Task<DetectionResultModel> StartDetectionSessionAsync(string plcData)
        {
            // 防止多个会话
            if (_currentState != DetectionSessionState.Idle)
            {
                _logger.Warning("已有检测会话在进行中 (State={State})", _currentState);
                return new DetectionResultModel
                {
                    IsSuccess = false,
                    Message = "设备忙，无法处理新请求",
                };
            }

            try
            {
                _currentSessionType = DetectionSessionType.Online;
                _currentState = DetectionSessionState.WaitingForImage;
                _currentPlcData = plcData;
                _sessionCompletionSource = new TaskCompletionSource<DetectionResultModel>();

                _logger.Information("========== 开始新的检测会话 ==========");

                //解析报文并提取参数

                var result = ParseAndValidatePlcData(plcData);
                if (!result.IsSuccess)
                {
                    _currentState = DetectionSessionState.Idle;
                    return result;
                }
                _currentSessionResult = result;
                _logger.Debug("电文解析完成 - ImageName={ImageName}", result.PictureName);

                _currentState = DetectionSessionState.WaitingForImage;
                _visionService.AcquireImage();
                _logger.Debug("已触发图像采集");

                // 等待ToolBlock完成
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))///timeout设置5s
                {
                    try
                    {
                        await _sessionCompletionSource.Task.ConfigureAwait(false);
                        _logger.Information("检测会话已完成");
                    }
                    catch (OperationCanceledException)
                    {
                        _currentState = DetectionSessionState.Failed;
                        _currentSessionResult.IsSuccess = false;
                        _currentSessionResult.Message = "检测超时(5s)";
                        _logger.Error("检测会话超时");
                    }
                }
                _logger.Information("========== 检测会话结束 - Result={Result} ==========",
                    _currentSessionResult.IsSuccess ? "成功" : "失败");

                return _currentSessionResult;
            }
            catch (Exception ex)
            {
                _currentState = DetectionSessionState.Idle;
                _logger.Error(ex, "检测会话异常");
                return new DetectionResultModel
                {
                    IsSuccess = false,
                    Message = $"检测异常: {ex.Message}",
                    DetailedCode = ex.ToString()
                };
            }
        }

        public async Task<DetectionResultModel> StartOfflineDetectionSessionAsync()
        {
            if (_currentState != DetectionSessionState.Idle)
            {
                _logger.Warning("已有检测会话在进行中 (State={State})，拒绝离线检测请求", _currentState);
                return new DetectionResultModel
                {
                    IsSuccess = false,
                    Message = "设备忙，无法处理离线检测",
                };
            }

            try
            {
                _currentSessionType = DetectionSessionType.Offline;
                _currentState = DetectionSessionState.WaitingForImageOffline;
                _sessionCompletionSource = new TaskCompletionSource<DetectionResultModel>();

                _logger.Information("========== 开始离线检测会话 ==========");


                _currentSessionResult = new DetectionResultModel
                {
                    Message = "离线测试",
                    IsSuccess = false,
                    //Exposure = _config.GetInt("相机参数", "默认曝光"),
                    //Gain = _config.GetInt("相机参数", "默认增益"),
                    ToolBlockOutputs = new Dictionary<string, string>()
                };

                _logger.Debug("离线会话初始化完成");

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    try
                    {
                        await _sessionCompletionSource.Task;
                        _logger.Information("离线检测会话已完成");
                    }
                    catch (OperationCanceledException)
                    {
                        _currentState = DetectionSessionState.Failed;
                        _currentSessionResult.IsSuccess = false;
                        _currentSessionResult.Message = "离线检测超时）";
                        _logger.Error("离线检测会话超时");
                    }
                }

                _logger.Information("========== 离线检测会话结束 - Result={Result} ==========",
                    _currentSessionResult.IsSuccess ? "成功" : "失败");

                return _currentSessionResult;
            }
            catch (Exception ex)
            {
                _currentState = DetectionSessionState.Failed;
                _logger.Error(ex, "离线检测会话异常");
                return new DetectionResultModel
                {
                    IsSuccess = false,
                    Message = $"离线检测异常: {ex.Message}",
                };
            }
            finally
            {
                _currentState = DetectionSessionState.Idle;
            }
        }


        /// <summary>
        /// 从ToolBlock中提取检测数据
        /// </summary>
        private void ExtractVppOutputs(ToolBlockResultModel toolBlockResult)
        {
            try
            {
                if (toolBlockResult?.ToolBlock?.Outputs == null)
                {
                    _logger.Warning("ToolBlock输出为空");
                    return;
                }

                _currentSessionResult.ToolBlockOutputs = new Dictionary<string, string>();
                var outputs = toolBlockResult.ToolBlock.Outputs;

                //foreach (var key in outputs.Keys)
                //{
                //    try
                //    {
                //        var value = outputs[key]?.Value;
                //        _currentSessionResult.ToolBlockOutputs[key] = value;
                //        _logger.Debug("  输出参数: {Key} = {Value}", key, value ?? "null");
                //    }
                //    catch (Exception ex)
                //    {
                //        _logger.Warning(ex, "  提取输出参数失败: {Key}", key);
                //    }
                //}
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "提取ToolBlock结果异常");
            }
        }


        /// <summary>
        /// 拼装输出电文
        /// </summary>
        private void BuildResponseMessage(DetectionResultModel result)
        {
            try
            {
                // 设置检测结果（硬编码: 对应输出电文的Result字段
                _protocolEngine.SetVariable("Result", result.ResultCode);

                if (_protocolConfig.OutputFields != null && _protocolConfig.OutputFields.Count > 0)
                {
                    string responseMessage = _protocolEngine.BuildOutput(_protocolConfig.OutputFields);
                    result.SendPlcMessage = responseMessage;
                    _logger.Information("输出电文已生成，长度: {Length}", responseMessage.Length);
                }
                else
                {
                    _logger.Warning("未配置输出字段");
                    result.SendPlcMessage = "";
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "拼装输出电文异常");
                result.IsSuccess = false;
                result.Message = $"拼装电文异常: {ex.Message}";
            }
        }

        /// <summary>
        /// 发送结果给PLC
        /// </summary>
        private void SendResultToPlc(DetectionResultModel result)
        {
            try
            {
                if (!string.IsNullOrEmpty(result.SendPlcMessage))
                {
                    _communicationService.SendAsync(result.SendPlcMessage);
                    _logger.Information("已发送PLC回复报文 (长度: {Length})", result.SendPlcMessage.Length);
                }
                else
                {
                    _logger.Warning("PLC回复报文为空");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "发送PLC消息失败");
            }
        }


        private DetectionResultModel ParseAndValidatePlcData(string rawPlcData)
        {
            var result = new DetectionResultModel { IsSuccess = false };
            var logs = new List<string> { $"接收数据长度: {rawPlcData?.Length ?? 0}" };

            try
            {
                _protocolEngine.ClearVariables();
                _protocolEngine.ParseInput(rawPlcData, _protocolConfig.InputFields);


                string imageName = _protocolEngine.GetVariable("ImageName") ?? "";
                string productTypeStr = _protocolEngine.GetVariable("ProductType1") ?? "0";
                logs.Add($"ImageName: {imageName}");
                logs.Add($"ProductType: {productTypeStr}");
                result.PictureName = imageName;
                result.IsSuccess = true;
                result.Message = "电文解析成功";
                _logger.Debug("电文解析完成");
            }
            catch (Exception ex)
            {
                logs.Add($"解析异常: {ex.Message}");
                result.Message = $"电文解析异常: {ex.Message}";
                _logger.Error(ex, "电文解析失败");
            }

            result.DetailedCode = string.Join(Environment.NewLine, logs);
            return result;
        }

        //double allowDeviation = _config.GetDouble("参数设置", "%允许面积偏差%");
        //_config.GetInt("全局变量","earOrNot");

    }
}
