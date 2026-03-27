using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace aoi_common.Models
{
    public class DetectionResultModel
    {

      
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 结果代码 (01=成功, 02=失败)
        /// </summary>
        public string ResultCode => IsSuccess ? "01" : "02";

        /// <summary>
        /// 错误/信息消息 (调试用)
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 详细日志 (调试用)
        /// </summary>
        public string DetailedCode { get; set; } = string.Empty;

        public string PictureName { get; set; } = string.Empty;

        /// <summary>
        /// VPP输出数据 
        /// Key: 变量名, Value: 变量值
        /// 例如："dist1" → "1240"
        public Dictionary<string, string> ToolBlockOutputs { get; set; } = new Dictionary<string, string>();

        public string SendPlcMessage { get; set; } = string.Empty;       
    }
}
