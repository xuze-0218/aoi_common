using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace aoi_common.Models
{
    public class DetectionResultModel
    {
        public int ItemCode { get; set; }
        public int Exposure { get; set; }
        public int Gain { get; set; }
        public string Message { get; set; } = string.Empty;     // 对应 outMessage
        public string PictureName { get; set; } = string.Empty; // 对应 outPictureName
        public string DetailedCode { get; set; } = string.Empty;// 对应 outCodeShow
        public bool IsSuccess { get; set; }
        public string SendPlcMessage { get; set; } = string.Empty;
        public Dictionary<string, object> ToolBlockOutputs { get; set; } = new Dictionary<string, object>();
    }
}
