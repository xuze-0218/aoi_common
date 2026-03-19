using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace aoi_common.Models
{
    public class DetectCode
    {
        public int CodeLength { get; private set; }
        public int FuncCode { get; private set; }
        public int TrigType { get; private set; }
        public int TrigCount { get; private set; }
        public int NameLength { get; private set; }
        public string ImageName { get; private set; } = string.Empty;
        public int CalibOrDetect { get; private set; }
        public int ProductType1 { get; private set; }
        public int CellType1 { get; private set; }
        public int ProductType2 { get; private set; }
        public int CellType2 { get; private set; }
        public int BackUp { get; private set; }

        public bool IsValid { get; private set; }
        public string ErrorMsg { get; private set; } = string.Empty;

        public DetectCode(string plcData)
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
