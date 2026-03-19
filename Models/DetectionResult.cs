using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace aoi_common.Models
{
    public class DetectionResult
    {
        public int ItemCode { get; set; }
        public float Exposure { get; set; }
        public float Gain { get; set; }
        public string Message { get; set; } = string.Empty;     // 对应 outMessage
        public string PictureName { get; set; } = string.Empty; // 对应 outPictureName
        public string DetailedCode { get; set; } = string.Empty;// 对应 outCodeShow
        public bool IsSuccess { get; set; }
    }
}
