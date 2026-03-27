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
                    _currentSessionResult.ItemCode = 99;
                    if (_currentSessionType == DetectionSessionType.Online)
                    {
                        SendResultToPlc(_currentSessionResult);
                    }
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
                //bool result = DetectComplexFilm();
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
                _currentSessionType = DetectionSessionType.Online;
                _currentState = DetectionSessionState.WaitingForImage;
                _currentPlcData = plcData;
                _sessionCompletionSource = new TaskCompletionSource<DetectionResultModel>();

                _logger.Information("========== 开始新的检测会话 ==========");
                _logger.Debug("原始PLC报文长度: {Length}", plcData?.Length ?? 0);

                //解析报文并提取参数
                var parseResult = ParseAndValidatePlcData(plcData);
                if (!parseResult.IsSuccess)
                {
                    _currentState = DetectionSessionState.Idle;
                    return parseResult;
                }
                _currentSessionResult = parseResult;

                _logger.Debug(" PLC报文已解析 - ItemCode={ItemCode}, Message={Message}",
                    _currentSessionResult.ItemCode, _currentSessionResult.Message);

                if (!_currentSessionResult.IsSuccess)
                {
                    _currentState = DetectionSessionState.Failed;
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

        public async Task<DetectionResultModel> StartOfflineDetectionSessionAsync()
        {
            if (_currentState != DetectionSessionState.Idle)
            {
                _logger.Warning("已有检测会话在进行中 (State={State})，拒绝离线检测请求", _currentState);
                return new DetectionResultModel
                {
                    IsSuccess = false,
                    Message = "设备忙，无法处理离线检测",
                    ItemCode = 99
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
                    ItemCode = 1,  // 默认为单个胶块检测
                    Message = "离线测试",
                    IsSuccess = false,
                    Exposure = _config.GetInt("相机参数", "默认曝光"),
                    Gain = _config.GetInt("相机参数", "默认增益"),
                    ToolBlockOutputs = new Dictionary<string, object>()
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
                        _currentSessionResult.ItemCode = 99;
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
        /// PLC回复报文
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


        private DetectionResultModel ParseAndValidatePlcData(string rawPlcData)
        {
            var result = new DetectionResultModel { IsSuccess = false };
            var logs = new List<string> { $"接收数据长度: {rawPlcData?.Length ?? 0}" };

            try
            {
                _protocolEngine.ClearVariables();

                if (_protocolConfig.InputFields == null || _protocolConfig.InputFields.Count == 0)
                {
                    logs.Add("未加载报文配置");
                }

                _protocolEngine.ParseInput(rawPlcData, _protocolConfig.InputFields);

                // 业务逻辑判断：标定 vs 检测
                string calibOrDetectStr = _protocolEngine.GetVariable("CalibOrDetect");
                string funcCodeStr = _protocolEngine.GetVariable("FuncCode");

                if (!int.TryParse(calibOrDetectStr, out int calibOrDetect))
                    calibOrDetect = 0;
                if (!int.TryParse(funcCodeStr, out int funcCode))
                    funcCode = 0;

                logs.Add($"功能码: {funcCode}, 标定/检测: {calibOrDetect}");

                // 功能码 2003 是标定，3003 是检测
                if (funcCode == 2003)
                {
                    //result.DetectionMode = DetectionMode.Calibration;
                    logs.Add("检测模式: 标定");
                }
                else if (funcCode == 3003)
                {
                    //result.DetectionMode = DetectionMode.Detection;
                    logs.Add("检测模式: 检测");

                    string productType1Str = _protocolEngine.GetVariable("ProductType1");
                    string productType2Str = _protocolEngine.GetVariable("ProductType2");

                    if (int.TryParse(productType1Str, out int productType1))
                    {
                        result.ItemCode = productType1;
                        logs.Add($"产品类型1: {productType1}");
                    }
                }
                else
                {
                    logs.Add($"❌ 功能码异常: {funcCode}");
                    result.Message = $"功能码异常: {funcCode}";
                    result.DetailedCode = string.Join(Environment.NewLine, logs);
                    return result;
                }

                // ✅ 获取图像名称
                string imageName = _protocolEngine.GetVariable("ImageName");
                result.PictureName = imageName;
                logs.Add($"图像名称: {imageName}");

                result.IsSuccess = true;
                result.Message = "电文验证成功";
            }
            catch (Exception ex)
            {
                logs.Add($"❌ 解析异常: {ex.Message}");
                result.Message = $"电文解析异常: {ex.Message}";
                _logger.Error(ex, "电文解析失败");
            }

            result.DetailedCode = string.Join(Environment.NewLine, logs);
            return result;
        }


        //private bool DetectComplexFilm()
        //{
        //    if (_config.GetInt("输入参数", "cellOrNot") == 0)
        //    {
        //        _currentSessionResult.Message = "电芯未检测到！";
        //        return false;
        //    }
        //    float area = 1000f;//获取的vpp的outputs结果
        //    double inRadio = _config.GetDouble("输入参数", "inRadio");//0.059
        //    double filmArea = 11111 * Math.Pow(inRadio, 2);// 面胶膜颜色,从vpp获取
        //    double cellRealArea = area* Math.Pow(inRadio, 2);

        //    double allowDeviation = _config.GetDouble("参数设置", "%允许面积偏差%");
        //    double cellBlueArea = _config.GetDouble("参数设置", "%电芯蓝膜面积%");
        //    double singleTapeArea = _config.GetDouble("参数设置", "%单个胶条面积%");
        //    double singleBlockArea = _config.GetDouble("参数设置", "%单个胶块面积%");
        //    double tearAreaThreshold = _config.GetDouble("面积阈值", "%胶撕膜面积%");

        //    // 标准完整面积
        //    double standardFullArea = cellBlueArea - 2 * singleTapeArea - singleBlockArea;

        //    // 面积偏差判断
        //    if (Math.Abs(cellRealArea - standardFullArea) < allowDeviation)
        //    {
        //        string sFilmArea = $"{filmArea:f3}mm²";

        //        // 撕膜面积检查
        //        if (filmArea < tearAreaThreshold)
        //        {
        //            // 小耳朵检测
        //            if (_config.GetInt("全局变量","earOrNot") == 0)
        //            {
        //                string direct = _config.GetString("全局变量", "directBig");
        //                if (direct is "上" or "下" or "左" or "右")
        //                {
        //                    bool earCheckFailed = direct is "上" or "下"
        //                        ? !DetectEar($"耳朵{direct}.左1") || !DetectEar($"耳朵{direct}.右1") || !DetectEar($"耳朵{direct}.左2") || !DetectEar($"耳朵{direct}.右2")
        //                        : !DetectEar($"耳朵{direct}.上1") || !DetectEar($"耳朵{direct}.下1") || !DetectEar($"耳朵{direct}.上2") || !DetectEar($"耳朵{direct}.下2");

        //                    if (earCheckFailed) return false;
        //                }
        //            }

        //            // 距离 / 面积检测
        //            if (GetGlobalInt("disOrArea") == 0)
        //            {
        //                if (!GetGroupLine("单色胶")) return false;

        //                // 距离计算
        //                DistanceLL1(upLeftLineCell, upLeftLineTape, inRadio, out upLeftDist);
        //                DistanceLL1(upRightLineCell, upRightLineTape, inRadio, out upRightDist);
        //                DistanceLL1(downRightLineCell, downRightLineTape, inRadio, out downRightDist);
        //                DistanceLL1(downLeftLineCell, downLeftLineTape, inRadio, out downLeftDist);
        //                DistanceLL1(leftUpLineCell, leftUpLineTape, inRadio, out leftUpDist);
        //                DistanceLL1(rightUpLineCell, rightUpLineTape, inRadio, out rightUpDist);
        //                DistanceLL1(rightDownLineCell, rightDownLineTape, inRadio, out rightDownDist);
        //                DistanceLL1(leftDownLineCell, leftDownLineTape, inRadio, out leftDownDist);
        //                DistanceLL1(leftLineCell, leftLineTape, inRadio, out leftDist);
        //                DistanceLL1(rightLineCell, rightLineTape, inRadio, out rightDist);

        //                // 角度计算
        //                LineAngle1(upLeftLineCell, upLeftLineTape, out upLeftAngle);
        //                LineAngle1(upRightLineCell, upRightLineTape, out upRightAngle);
        //                LineAngle1(downRightLineCell, downRightLineTape, out downRightAngle);
        //                LineAngle1(downLeftLineCell, downLeftLineTape, out downLeftAngle);
        //                LineAngle1(leftUpLineCell, leftUpLineTape, out leftUpAngle);
        //                LineAngle1(rightUpLineCell, rightUpLineTape, out rightUpAngle);
        //                LineAngle1(rightDownLineCell, rightDownLineTape, out rightDownAngle);
        //                LineAngle1(leftDownLineCell, leftDownLineTape, out leftDownAngle);

        //                if (!DetectComplexDistAndAngle()) return false;
        //            }
        //            else
        //            {
        //                if (!GetGroupArea("单色胶")) return false;
        //                if (!DetectDoubleArea()) return false;
        //            }
        //        }
        //        else
        //        {
        //            outMsg = $"组合胶上撕膜有残留！面积：{filmArea:f3}mm²";
        //            return false;
        //        }
        //    }
        //    // 只检测到大面胶
        //    else if (Math.Abs(cellRealArea - (cellBlueArea - singleBlockArea)) < allowDeviation)
        //    {
        //        outMsg = $"组合胶只检测到大面胶！电芯露出面积：{cellRealArea:f3}mm²";
        //        return false;
        //    }
        //    // 只检测到双条胶
        //    else if (Math.Abs(cellRealArea - (cellBlueArea - 2 * singleTapeArea)) < allowDeviation)
        //    {
        //        outMsg = $"组合胶只检测到双条胶！电芯露出面积：{cellRealArea:f3}mm²";
        //        return false;
        //    }
        //    // 只检测到一条胶
        //    else if (Math.Abs(cellRealArea - (cellBlueArea - singleTapeArea - singleBlockArea)) < allowDeviation)
        //    {
        //        outMsg = $"组合胶只检测到一条胶！电芯露出面积：{cellRealArea:f3}mm²";
        //        return false;
        //    }
        //    // 未检测到组合胶
        //    else
        //    {
        //        outMsg = $"组合胶未检测到！电芯露出面积：{cellRealArea:f3}mm²";
        //        return false;
        //    }

        //    // 所有检测通过
        //    return true;
        //}
    }
}
