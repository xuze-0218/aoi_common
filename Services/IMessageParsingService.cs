using aoi_common.Models;
using Serilog;
using System;
using System.Collections.Generic;

namespace aoi_common.Services
{
    /// <summary>
    /// 负责报文解析和参数提取
    /// </summary>
    public interface IMessageParsingService
    {
        DetectionResultModel ParsePlcData(string rawPlcData);

    }

    public class MessageParsingService : IMessageParsingService
    {
        //private readonly IVisionService _visionService;
        private readonly IParametersConfigService _config;
        private readonly ILogger _logger;
        public MessageParsingService(IParametersConfigService config, /*IVisionService visionService*/ILogger logger)
        {
            _config = config;
            _logger = logger;
            //_visionService = visionService;
        }

        public DetectionResultModel ParsePlcData(string rawPlcData)
        {
            var result = new DetectionResultModel { IsSuccess = false };
            var logs = new List<string> { $"接收数据长度:{rawPlcData?.Length ?? 0}" };

            //if (_config.GetInt("全局变量", "ModuStatus") == 0)
            //{
            //    SetError(result, logs, 99, "手动触发测试！");
            //    return result;
            //}
            //验证报文格式
            var dc = new ProtocolParse(rawPlcData);
            if (!dc.IsValid)
            {
                SetError(result, logs, 98, dc.ErrorMsg);
                return result;
            }

            result.PictureName = dc.ImageName;
            PopulateLogDetails(logs, dc);

            if (dc.CalibOrDetect == 1 && dc.FuncCode == 2003)
            {
                ProcessCalibration(result, logs);
            }
            else if (dc.CalibOrDetect == 2 && dc.FuncCode == 2003)
            {
                ProcessDetection(result, logs, dc);

            }
            else
            {
                SetError(result, logs, 97, $"PLC报文异常，功能码：{dc.FuncCode}！");
            }

            result.DetailedCode = string.Join(Environment.NewLine, logs);
            return result;
        }

        private void ProcessCalibration(DetectionResultModel res, List<string> logs)
        {
            res.ItemCode = 100;
            res.Message = "成功进入标定！";
            res.Exposure = _config.GetInt("相机参数", "标定曝光");
            res.Gain = _config.GetInt("相机参数", "标定增益");
            res.IsSuccess = true;
            logs.Add(res.Message);
        }

        private void ProcessDetection(DetectionResultModel res, List<string> logs, ProtocolParse dc)
        {
            logs.Add("成功进入检测!");

            int finalType = JudgeItemCode(dc.ProductType1, dc.ProductType2);
            res.ItemCode = finalType;//临时赋值
            res.Exposure = _config.GetInt("相机参数", "默认曝光");
            res.Gain = _config.GetInt("相机参数", "默认增益");

            //switch (finalType)
            //{
            //    case 0: res.ItemCode = 93; res.Message = "该产品不应该贴胶！"; break;
            //    case 1: res.ItemCode = 1; res.Message = "进入复杂大面（白膜黑胶）检测流程！"; res.IsSuccess = true; break;
            //    case 2: res.ItemCode = 2; res.Message = "进入复杂大面（透明膜）检测流程！"; res.IsSuccess = true; break;
            //    case 3:
            //    case 4:
            //        res.ItemCode = finalType;
            //        res.Message = $"进入复杂双胶条（{(finalType == 3 ? "白膜黑胶" : "透明膜")}）检测流程！";
            //        res.Exposure = _config.GetInt("相机参数", "胶条曝光");
            //        res.Gain = _config.GetInt("相机参数", "胶条增益");
            //        res.IsSuccess = true;
            //        break;
            //    case 5: res.ItemCode = 5; res.Message = "进入简易大面（白膜黑胶）检测流程！"; res.IsSuccess = true; break;
            //    case 6: res.ItemCode = 6; res.Message = "进入简易大面（透明膜）检测流程！"; res.IsSuccess = true; break;
            //    case 7:
            //    case 8:
            //        res.ItemCode = finalType;
            //        res.Message = $"进入复杂三胶条（{(finalType == 7 ? "白膜黑胶" : "透明膜")}）检测流程！";
            //        res.Exposure = _config.GetInt("相机参数", "胶条曝光");
            //        res.Gain = _config.GetInt("相机参数", "胶条增益");
            //        res.IsSuccess = true;

            //        break;
            //    case 9: res.ItemCode = 9; res.Message = "进入组合胶检测流程！"; res.IsSuccess = true; _visionService.AcquireImage(); break;
            //    case 10: res.ItemCode = 10; res.Message = "进入备用胶2检测流程！"; res.IsSuccess = true; break;
            //    default: res.ItemCode = 94; res.Message = $"PLC产品类型异常：{finalType}！"; break;
            //}
            //if (IsIgnored(finalType))
            //{
            //    res.ItemCode = 95;
            //    res.Message = "该产品视觉屏蔽检测结果！";
            //}
            string detectionModeName = GetDetectionModeName(finalType);
            res.Message = $"进入检测模式: {detectionModeName}";
            logs.Add(res.Message);

            _logger.Information("进入检测模式 - ItemCode={ItemCode}, 模式={Mode}, 曝光={Exposure}, 增益={Gain}",
                finalType, detectionModeName, res.Exposure, res.Gain);
            logs.Add(res.Message);


        }

        //private bool IsIgnored(int type)
        //{
        //    if (_config.GetBool("全局变量", "ignore")) return true;
        //    string ignoreOne = _config.GetString("全局变量", "ignoreOne");

        //    return (ignoreOne == "大面胶1" && (type == 1 || type == 2)) ||
        //           (ignoreOne == "双条胶1" && (type == 3 || type == 4)) ||
        //           (ignoreOne == "大面胶2" && (type == 7 || type == 8));
        //}

        public int JudgeItemCode(int productType1, int productType2)
        {
            try
            {
                int pType = productType1;
                bool bigTrans = _config.GetBool("全局变量", "bigTrans");
                bool doubleTrans = _config.GetBool("全局变量", "doubleTrans");

                if (pType == 1 || pType == 2)
                    pType = !bigTrans ? 1 : 2;
                else if (pType == 3 || pType == 4)
                    pType = !doubleTrans ? 3 : 4;

                bool plcOrNot = _config.GetBool("全局变量", "plcOrNot");
                int finalType = (plcOrNot && pType != 0) ? _config.GetInt("全局变量", "产品类型") : pType;
                _logger.Debug("产品类型转换: {Original} -> {Final}", productType1, finalType);
                return finalType;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "确定最终ItemCode异常");
                return 0;
            }
        }

        private string GetDetectionModeName(int itemCode)
        {
            switch (itemCode)
            {
                case 0:
                    return "该产品不应该贴胶";
                case 1:
                    return "复杂大面实心胶块（白膜黑胶）";
                case 2:
                    return "复杂大面实心胶块（透明膜）";
                case 3:
                    return "复杂双胶条（白膜黑胶）";
                case 4:
                    return "复杂双胶条（透明胶）";
                case 5:
                    return "简易大面实心胶块（白膜黑胶）";
                case 6:
                    return "简易大面实心胶块（透明膜）";
                case 7:
                    return "复杂三胶条（白膜黑胶）";
                case 8:
                    return "复杂三胶条（透明胶）";
                case 9:
                    return "组合胶";
                case 10:
                    return "备用胶2";
                case 93:
                    return "该产品不应该贴胶";
                case 95:
                    return "视觉屏蔽检测结果";
                case 100:
                    return "标定模式";
                default:
                    return "未知模式";
            }
        }

        private void SetError(DetectionResultModel res, List<string> logs, int code, string msg)
        {
            res.ItemCode = code;
            res.Message = msg;
            logs.Add(msg);
            Log.Warning("检测逻辑被拦截: {Msg}", msg);
        }

        private void PopulateLogDetails(List<string> logs, ProtocolParse dc)
        {
            logs.Add($"功能码:{dc.FuncCode}");
            logs.Add($"检测类型:{dc.TrigType}");
            logs.Add($"检测数量:{dc.TrigCount}");
            logs.Add($"图片名称长度:{dc.NameLength}");
            logs.Add($"图片名称:{dc.ImageName}");
            logs.Add($"标定或检测:{dc.CalibOrDetect}");
            logs.Add($"产品类型1:{dc.ProductType1}");
            logs.Add($"电芯类型1:{dc.CellType1}");
            logs.Add($"产品类型2:{dc.ProductType2}");
            logs.Add($"电芯类型2:{dc.CellType2}");
            logs.Add($"备用:{dc.BackUp}");
        }
    }
}
