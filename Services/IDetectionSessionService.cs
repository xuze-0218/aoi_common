using aoi_common.Events;
using aoi_common.Models;
using Prism.Events;
using Serilog;
using System;
using System.Collections.Generic;
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
        /// 开启检测
        /// </summary>
        /// <returns></returns>
        Task<DetectionResultModel> StartDetectionSessionAsync(string plcData);

        /// <summary>
        /// 当前会话的状态
        /// </summary>
        DetectionSessionState CurrentState { get; }
    }

    public class DetectionSessionService : IDetectionSessionService
    {
        private readonly IVisionService _visionService;
        private readonly IMessageParsingService _messageParsingService;
        private readonly ICommunicationService _communicationService;
        private readonly IParametersConfigService _config;
        private readonly IEventAggregator _eventAggregator;
        private readonly ILogger _logger;

        private DetectionResultModel _currentSessionResult;
        private string _currentPlcData;
        private TaskCompletionSource<DetectionResultModel> _sessionCompletionSource;
        private ProtocolParse _currentProtocolParse;
        private DetectionSessionState _currentState = DetectionSessionState.Idle;
        public DetectionSessionState CurrentState => _currentState;

        public DetectionSessionService(
            IVisionService visionService,
            IMessageParsingService messageParsingService,
            ICommunicationService communicationService,
            IParametersConfigService config,
            IEventAggregator eventAggregator,
            ILogger logger)
        {
            _visionService = visionService;
            _messageParsingService = messageParsingService;
            _communicationService = communicationService;
            _config = config;
            _eventAggregator = eventAggregator;
            _logger = logger;

            // 订阅ToolBlock完成事件
            _eventAggregator.GetEvent<ToolBlockCompletedEvent>().Subscribe(OnToolBlockCompleted, ThreadOption.BackgroundThread);
            _logger.Information("检测会话服务已初始化");
        }

        /// <summary>
        /// 接收toolblock运行完成返回的ToolBlockResultModel结果
        /// </summary>
        /// <param name="toolBlockResult"></param>
        private void OnToolBlockCompleted(ToolBlockResultModel toolBlockResult)
        {
            //只处理有活跃会话的情况
            if (_currentState != DetectionSessionState.WaitingForImage)
            {
                _logger.Debug(" ToolBlock完成但无活跃会话 (State={State})，忽略此结果", _currentState);
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
                    _currentSessionResult.ItemCode = 99;
                    SendResultToPlc(_currentSessionResult);
                    _currentState = DetectionSessionState.Failed;
                    _sessionCompletionSource?.TrySetResult(_currentSessionResult);
                    return;
                }
                //从ToolBlock提取检测数据
                ExtractToolBlockResults(toolBlockResult);
                _logger.Debug("已提取ToolBlock输出数据 (共{Count}项)", _currentSessionResult.ToolBlockOutputs.Count);

                //执行检测逻辑
                bool detectionPassed = ExecuteDetectionLogic(toolBlockResult);
                _logger.Information("检测逻辑执行完成 - Result={Result}",
                    detectionPassed ? "合格" : "不合格");

                // 更新检测结果
                _currentSessionResult.IsSuccess = detectionPassed;
                _currentSessionResult.Message = detectionPassed
                    ? $"ItemCode {_currentSessionResult.ItemCode} 检测合格"
                    : $"ItemCode {_currentSessionResult.ItemCode} 检测不合格";

                //构建PLC回复报文
                _currentSessionResult.SendPlcMessage = BuildPlcResponseMessage(
                    _currentSessionResult,
                    toolBlockResult);
                _logger.Debug("已构建PLC回复报文");

                //发送PLC结果
                SendResultToPlc(_currentSessionResult);
                _logger.Debug("已发送PLC结果");

                _currentState = DetectionSessionState.Completed;

                //信号会话完成
                _sessionCompletionSource?.TrySetResult(_currentSessionResult);
                _logger.Information("检测会话标记为完成");
            }
            catch (Exception ex)
            {
                _currentState = DetectionSessionState.Failed;
                _logger.Error(ex, "处理ToolBlock结果时异常");
                _sessionCompletionSource?.TrySetException(ex);
            }
        }


        /// <summary>
        /// 从ToolBlock中提取检测数据
        /// </summary>
        private void ExtractToolBlockResults(ToolBlockResultModel toolBlockResult)
        {
            try
            {
                if (toolBlockResult?.ToolBlock?.Outputs == null)
                {
                    _logger.Warning("ToolBlock输出为空");
                    return;
                }

                _currentSessionResult.ToolBlockOutputs = new Dictionary<string, object>();
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

        private bool ExecuteDetectionLogic(ToolBlockResultModel toolBlockResult)
        {
            try
            {
                int itemCode = _currentSessionResult.ItemCode;

                // 根据ItemCode调用不同的检测方法
                //bool result = itemCode switch
                //{
                //    0 => DetectNoTape(),                            // 该产品不应该贴胶
                //    1 => DetectSingleSolidTapeComplexFilm(),        // 复杂大面实心胶块（白膜黑胶）
                //    2 => DetectSingleSolidTapeComplex(),            // 复杂大面实心胶块（透明膜）
                //    3 => DetectDoubleTapeComplexFilm(),             // 复杂双胶条（白膜黑胶）
                //    4 => DetectDoubleTapeComplex(),                 // 复杂双胶条（透明胶）
                //    5 => DetectSingleSolidTapeSimpleFilm(),         // 简易大面实心胶块（白膜黑胶）
                //    6 => DetectSingleSolidTapeSimple(),             // 简易大面实心胶块（透明膜）
                //    7 => DetectComplexThreeTapeFilm(),              // 复杂三胶条（白膜黑胶）
                //    8 => DetectComplexThreeTape(),                  // 复杂三胶条（透明胶）
                //    9 => DetectComplexFilm(),                       // 组合胶
                //    10 => DetectReservedTape2(),                    // 备用胶2
                //    93 => DetectNoTape(),                           // 该产品不应该贴胶
                //    95 => true,                                     // 视觉屏蔽检测结果 - 直接返回true
                //    _ => false
                //};

                //return result;
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "执行检测逻辑异常");
                return false;
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
                    ItemCode = 99
                };
            }

            try
            {
                _currentState = DetectionSessionState.WaitingForImage;
                _currentPlcData = plcData;
                _sessionCompletionSource = new TaskCompletionSource<DetectionResultModel>();

                _logger.Information("========== 开始新的检测会话 ==========");
                _logger.Debug("原始PLC报文长度: {Length}", plcData?.Length ?? 0);

                //解析报文并提取参数
                _currentSessionResult = _messageParsingService.ParsePlcData(plcData);
                _currentProtocolParse = new ProtocolParse(plcData);

                _logger.Debug(" PLC报文已解析 - ItemCode={ItemCode}, Message={Message}",
                    _currentSessionResult.ItemCode, _currentSessionResult.Message);

                if (!_currentProtocolParse.IsValid)
                {
                    _currentState = DetectionSessionState.Failed;
                    _logger.Error(" 报文解析失败: {Error}", _currentProtocolParse.ErrorMsg);
                    _currentSessionResult.IsSuccess = false;
                    return _currentSessionResult;
                }
                int finalType = _currentSessionResult.ItemCode;
                ApplyDetectionModeSpecificSettings(finalType);//此时的ItemCode实际值为finalType

                if (IsIgnored(finalType))
                {
                    _currentSessionResult.ItemCode = 95;
                    _currentSessionResult.Message = "该产品视觉屏蔽检测结果！";
                    _logger.Information("该产品被屏蔽检测");
                    _currentState = DetectionSessionState.Completed;
                    return _currentSessionResult;
                }
                //根据报文类型进行相应操作
                switch (_currentSessionResult.ItemCode)
                {
                    case 0:     // 该产品不应该贴胶
                    case 93:    // 同上
                    case 95:    // 屏蔽
                                // 这些模式不需要ToolBlock运行，直接完成
                        _currentState = DetectionSessionState.Completed;
                        return _currentSessionResult;

                    case 100:   // 标定模式
                        _currentState = DetectionSessionState.Completed;
                        return _currentSessionResult;

                    default:    // 其他检测模式（1-10）
                                // 需要采集图像并运行ToolBlock
                        _currentState = DetectionSessionState.WaitingForImage;
                        _visionService.AcquireImage();
                        _logger.Debug("已触发图像采集");

                        // 等待ToolBlock完成
                        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))///timeout设置5s
                        {
                            try
                            {
                                await _sessionCompletionSource.Task;
                                _logger.Information("检测会话已完成");
                            }
                            catch (OperationCanceledException)
                            {
                                _currentState = DetectionSessionState.Failed;
                                _currentSessionResult.IsSuccess = false;
                                _currentSessionResult.Message = "检测超时";
                                _currentSessionResult.ItemCode = 99;
                                _logger.Error("检测会话超时");
                            }
                        }
                        break;
                }
                _logger.Information("========== 检测会话结束 - Result={Result} ==========",
                    _currentSessionResult.IsSuccess ? "成功" : "失败");

                return _currentSessionResult;
            }
            catch (Exception ex)
            {
                _currentState = DetectionSessionState.Failed;
                _logger.Error(ex, "检测会话异常");
                return new DetectionResultModel
                {
                    IsSuccess = false,
                    Message = $"检测异常: {ex.Message}",
                    ItemCode = 99
                };
            }
            finally
            {
                _currentState = DetectionSessionState.Idle;
            }
        }

        private void ApplyDetectionModeSpecificSettings(int finalType)
        {
            try
            {
                _logger.Debug("应用检测模式设置 - ItemCode={ItemCode}", finalType);

                switch (finalType)
                {
                    case 0:
                        _currentSessionResult.ItemCode = 93;
                        _currentSessionResult.Message = "该产品不应该贴胶！";
                        break;

                    case 1:
                        _currentSessionResult.ItemCode = 1;
                        _currentSessionResult.Message = "进入复杂大面（白膜黑胶）检测流程！";
                        _currentSessionResult.IsSuccess = true;
                        break;

                    case 2:
                        _currentSessionResult.ItemCode = 2;
                        _currentSessionResult.Message = "进入复杂大面（透明膜）检测流程！";
                        _currentSessionResult.IsSuccess = true;
                        break;

                    case 3:
                    case 4:
                        _currentSessionResult.ItemCode = finalType;
                        _currentSessionResult.Message = $"进入复杂双胶条（{(finalType == 3 ? "白膜黑胶" : "透明膜")}）检测流程！";
                        _currentSessionResult.Exposure = _config.GetInt("相机参数", "胶条曝光");
                        _currentSessionResult.Gain = _config.GetInt("相机参数", "胶条增益");
                        _currentSessionResult.IsSuccess = true;
                        break;

                    case 5:
                        _currentSessionResult.ItemCode = 5;
                        _currentSessionResult.Message = "进入简易大面（白膜黑胶）检测流程！";
                        _currentSessionResult.IsSuccess = true;
                        break;

                    case 6:
                        _currentSessionResult.ItemCode = 6;
                        _currentSessionResult.Message = "进入简易大面（透明膜）检测流程！";
                        _currentSessionResult.IsSuccess = true;
                        break;

                    case 7:
                    case 8:
                        _currentSessionResult.ItemCode = finalType;
                        _currentSessionResult.Message = $"进入复杂三胶条（{(finalType == 7 ? "白膜黑胶" : "透明膜")}）检测流程！";
                        _currentSessionResult.Exposure = _config.GetInt("相机参数", "胶条曝光");
                        _currentSessionResult.Gain = _config.GetInt("相机参数", "胶条增益");
                        _currentSessionResult.IsSuccess = true;
                        break;

                    case 9:
                        _currentSessionResult.ItemCode = 9;
                        _currentSessionResult.Message = "进入组合胶检测流程！";
                        _currentSessionResult.IsSuccess = true;
                        break;

                    case 10:
                        _currentSessionResult.ItemCode = 10;
                        _currentSessionResult.Message = "进入备用胶2检测流程！";
                        _currentSessionResult.IsSuccess = true;
                        break;

                    default:
                        _currentSessionResult.ItemCode = 94;
                        _currentSessionResult.Message = $"PLC产品类型异常：{finalType}！";
                        break;
                }

                _logger.Debug("检测模式设置完成 - Message={Message}", _currentSessionResult.Message);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "应用检测模式设置异常");
            }
        }

        private bool IsIgnored(int type)
        {
            if (_config.GetBool("全局变量", "ignore")) return true;
            string ignoreOne = _config.GetString("全局变量", "ignoreOne");

            return (ignoreOne == "大面胶1" && (type == 1 || type == 2)) ||
                   (ignoreOne == "双条胶1" && (type == 3 || type == 4)) ||
                   (ignoreOne == "大面胶2" && (type == 7 || type == 8));
        }

        /// <summary>
        /// 构建PLC回复报文
        /// </summary>
        /// <returns></returns>
        private string BuildPlcResponseMessage(DetectionResultModel result, ToolBlockResultModel toolBlockResult)
        {
            try
            {
                var sb = new StringBuilder();

                // 报文头
                sb.Append("$12963003");

                // 检测结果 (01=OK, 02=NG)
                sb.Append(result.IsSuccess ? "01" : "02");

             
                // 例如：长度、宽度、角度等参数
                // sb.Append(Convert.ToInt32(Math.Round(length * 1000)).ToString("D8"));
                // sb.Append(Convert.ToInt32(Math.Round(width * 1000)).ToString("D8"));
                // sb.Append(Convert.ToInt32(Math.Round(angle * 1000)).ToString("D8"));

                // 假数据（24位）
                sb.Append(new string('0', 24));

                // 预留数据区（600位）
                for (int i = 0; i < 24; i++)
                {
                    sb.Append("00");
                    sb.Append(new string('0', 48));
                }

                // 报文尾
                sb.Append("#");

                _logger.Debug("构建的PLC回复报文长度: {Length}", sb.Length);
                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "构建PLC回复报文异常");
                return string.Empty;
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
      
    }
}
