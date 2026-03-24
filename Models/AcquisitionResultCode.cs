using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace aoi_common.Models
{
    public enum AcquisitionResultCode
    {
        /// <summary>采集成功</summary>
        Success = 0,

        /// <summary>相机未初始化</summary>
        NotInitialized = 1,

        /// <summary>采集超时</summary>
        Timeout = 2,

        /// <summary>采集失败</summary>
        Failed = 3,

        /// <summary>异常</summary>
        Exception = 4
    }
}
