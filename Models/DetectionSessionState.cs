using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace aoi_common.Models
{
    public enum DetectionSessionState
    {
        /// <summary>
        /// 空闲状态，可以接收新的检测请求
        /// </summary>
        Idle,
        /// <summary>
        /// 等待图像采集完成
        /// </summary>
        WaitingForImage,
        /// <summary>
        /// 正在处理检测结果
        /// </summary>
        Processing,
        /// <summary>
        /// 检测完成
        /// </summary>
        Completed,
        /// <summary>
        /// 检测失败
        /// </summary>
        Failed
    }
}
