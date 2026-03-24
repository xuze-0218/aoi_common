using Cognex.VisionPro;
using Cognex.VisionPro.ToolBlock;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace aoi_common.Models
{
    public class ToolBlockResultModel
    {
        public bool IsSuccess { get; set; }

        public string ErrorMessage { get; set; }

        public ICogRecord DisplayRecord { get; set; }

        public CogToolBlock ToolBlock { get; set; }
     
        public Dictionary<string, object> Outputs { get; set; } = new Dictionary<string, object>();

        public DateTime TimeConsuming { get; set; }
    }
}
