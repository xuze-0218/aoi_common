using aoi_common.Models;
using Serilog;
using System;
using System.Collections.Generic;

namespace aoi_common.Services
{
    public interface IDetectionLogicService
    {
        DetectionResult ProcessPlcData(string rawPlcData);
    }

    public class DetectionLogicService : IDetectionLogicService
    {
        private readonly IParametersConfigService _config;
        public DetectionLogicService(IParametersConfigService config)
        {
            _config = config;
        }

        public DetectionResult ProcessPlcData(string rawPlcData)
        {
            var result = new DetectionResult { IsSuccess = false };
            var logs = new List<string> { $"接收数据长度:{rawPlcData?.Length ?? 0}" };

            //if (_config.GetInt("全局变量", "ModuStatus") == 0)
            //{
            //    SetError(result, logs, 99, "手动触发测试！");
            //    return result;
            //}

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

        private void ProcessCalibration(DetectionResult res, List<string> logs)
        {
            res.ItemCode = 100;
            res.Message = "成功进入标定！";
            res.Exposure = _config.GetInt("相机参数", "标定曝光");
            res.Gain = _config.GetInt("相机参数", "标定增益");
            res.IsSuccess = true;
            logs.Add(res.Message);
        }

        private void ProcessDetection(DetectionResult res, List<string> logs, ProtocolParse dc)
        {
            logs.Add("成功进入检测！");

            // 产品类型转换逻辑
            int pType = dc.ProductType1;
            bool bigTrans = _config.GetBool("全局变量", "bigTrans");
            bool doubleTrans = _config.GetBool("全局变量", "doubleTrans");

            if (pType == 1 || pType == 2) pType = !bigTrans ? 1 : 2;
            else if (pType == 3 || pType == 4) pType = !doubleTrans ? 3 : 4;

            bool plcOrNot = _config.GetBool("全局变量", "plcOrNot");
            int finalType = (plcOrNot && pType != 0) ? _config.GetInt("全局变量", "产品类型") : pType;

            res.Exposure = _config.GetInt("相机参数", "默认曝光");
            res.Gain = _config.GetInt("相机参数", "默认增益");

            switch (finalType)
            {
                case 0: res.ItemCode = 93; res.Message = "该产品不应该贴胶！"; break;
                case 1: res.ItemCode = 1; res.Message = "进入复杂大面（白膜黑胶）检测流程！"; res.IsSuccess = true; break;
                case 2: res.ItemCode = 2; res.Message = "进入复杂大面（透明膜）检测流程！"; res.IsSuccess = true; break;
                case 3:
                case 4:
                    res.ItemCode = finalType;
                    res.Message = $"进入复杂双胶条（{(finalType == 3 ? "白膜黑胶" : "透明膜")}）检测流程！";
                    res.Exposure = _config.GetInt("相机参数", "胶条曝光");
                    res.Gain = _config.GetInt("相机参数", "胶条增益");
                    res.IsSuccess = true;
                    break;
                case 5: res.ItemCode = 5; res.Message = "进入简易大面（白膜黑胶）检测流程！"; res.IsSuccess = true; break;
                case 6: res.ItemCode = 6; res.Message = "进入简易大面（透明膜）检测流程！"; res.IsSuccess = true; break;
                case 7:
                case 8:
                    res.ItemCode = finalType;
                    res.Message = $"进入复杂三胶条（{(finalType == 7 ? "白膜黑胶" : "透明膜")}）检测流程！";
                    res.Exposure = _config.GetInt("相机参数", "胶条曝光");
                    res.Gain = _config.GetInt("相机参数", "胶条增益");
                    res.IsSuccess = true;
                    break;
                case 9: res.ItemCode = 9; res.Message = "进入组合胶检测流程！"; res.IsSuccess = true; break;
                case 10: res.ItemCode = 10; res.Message = "进入备用胶2检测流程！"; res.IsSuccess = true; break;
                default: res.ItemCode = 94; res.Message = $"PLC产品类型异常：{finalType}！"; break;
            }
            if (IsIgnored(finalType))
            {
                res.ItemCode = 95;
                res.Message = "该产品视觉屏蔽检测结果！";
            }

            logs.Add(res.Message);


        }

        private bool IsIgnored(int type)
        {
            if (_config.GetBool("全局变量", "ignore")) return true;
            string ignoreOne = _config.GetString("全局变量", "ignoreOne");

            return (ignoreOne == "大面胶1" && (type == 1 || type == 2)) ||
                   (ignoreOne == "双条胶1" && (type == 3 || type == 4)) ||
                   (ignoreOne == "大面胶2" && (type == 7 || type == 8));
        }

        private void SetError(DetectionResult res, List<string> logs, int code, string msg)
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
