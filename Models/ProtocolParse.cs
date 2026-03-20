using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace aoi_common.Models
{
    /// <summary>
    /// 报文解析
    /// </summary>
    public class ProtocolParse
    {
        public int CodeLength { get; private set; }                     //报文长度
        public int FuncCode { get; private set; }                       //功能码
        public int TrigType { get; private set; }                       //检测类型
        public int TrigCount { get; private set; }                      //检测数量
        public int NameLength { get; private set; }                     //二维码名称长度
        public string ImageName { get; private set; } = string.Empty;   //二维码名称
        public int CalibOrDetect { get; private set; }                  //标定或检测（0-标定 1-检测）
        public int ProductType1 { get; private set; }                   //产品类型1
        public int CellType1 { get; private set; }                      //电芯类型1
        public int ProductType2 { get; private set; }                   //产品类型2
        public int CellType2 { get; private set; }                      //电芯类型2
        public int BackUp { get; private set; }                         //备用

        public bool IsValid { get; private set; }
        public string ErrorMsg { get; private set; } = string.Empty;

        public ProtocolParse(string plcData)
        {
            if (string.IsNullOrEmpty(plcData) || plcData.Length < 178)
            {
                IsValid = false;
                ErrorMsg = $"报文长度不足 178 位 (当前: {plcData?.Length ?? 0})";
                return;
            }

            try
            {
                CodeLength = SafeParse(plcData, 1, 4);
                FuncCode = SafeParse(plcData, 5, 4);
                TrigType = SafeParse(plcData, 17, 2);
                TrigCount = SafeParse(plcData, 19, 2);
                NameLength = SafeParse(plcData, 21, 4);
                ImageName = (NameLength == 0) ? string.Empty : plcData.Substring(25, NameLength).Trim();
                CalibOrDetect = SafeParse(plcData, 153, 4);
                ProductType1 = SafeParse(plcData, 157, 4);
                CellType1 = SafeParse(plcData, 161, 4);
                ProductType2 = SafeParse(plcData, 165, 4);
                CellType2 = SafeParse(plcData, 169, 4);
                BackUp = SafeParse(plcData, 173, 4);
                IsValid = true;
            }
            catch (Exception ex)
            {
                IsValid = false;
                ErrorMsg = $"数据格式转换异常: {ex.Message}";
            }
        }

        private int SafeParse(string data, int start, int length)
        {
            string sub = data.Substring(start, length).Trim();
            return int.TryParse(sub, out int result) ? result : 0;
        }
    }
}
