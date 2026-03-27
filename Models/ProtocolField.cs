using Prism.Mvvm;
using System.Collections.Generic;
namespace aoi_common.Models
{
    public enum FieldSource
    {
        Fixed, //固定文本
        Variable, //变量池映射
        Padding  //空白填充
    }
    public enum LengthType
    {
        Fixed,      // 固定长度
        Dynamic     // 动态长度（从其他字段获取）
    }

    public class ProtocolField : BindableBase
    {
        private int _index;
        private string _name = "";
        private FieldSource _source = FieldSource.Variable;
        private int _length;
        private double _scale = 1.0;
        private string _fixedValue = "";
        private int _startIndex;  // 仅用于输入侧
        private string _description = "";
        private string _preview = "";
        private LengthType _lengthType = LengthType.Fixed;
        private string _lengthSourceField = "";  // 长度来源字段名（如 "CodeLength"）
        private int _lengthOffset = 0;           // 长度偏移
        // ====== 通用字段 ======
        /// <summary>
        /// 排序索引（输出侧用于确定电文中的位置）
        /// </summary>
        public int Index
        {
            get => _index;
            set => SetProperty(ref _index, value);
        }

        /// <summary>
        /// 字段名称（对应变量池中的 Key）
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>
        /// 字段来源类型
        /// </summary>
        public FieldSource Source
        {
            get => _source;
            set => SetProperty(ref _source, value);
        }


        public int Length
        {
            get => _length;
            set => SetProperty(ref _length, value);
        }

        /// <summary>
        /// 缩放因子（仅Variable生效,用于处理浮点数）
        /// 例如：1.24 * 1000 = 1240
        /// </summary>
        public double Scale
        {
            get => _scale;
            set => SetProperty(ref _scale, value);
        }

        /// <summary>
        /// 固定值或默认值
        /// - 当 Source=Fixed：存储固定内容
        /// - 当 Source=Variable：存储变量不存在时的默认值
        /// </summary>
        public string FixedValue
        {
            get => _fixedValue;
            set => SetProperty(ref _fixedValue, value);
        }

        // ====== 仅输入侧 ======
        /// <summary>
        /// 原始电文中的起始位置（从 0 开始）
        /// 仅用于接收解析
        /// </summary>
        public int StartIndex
        {
            get => _startIndex;
            set => SetProperty(ref _startIndex, value);
        }


        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        /// <summary>
        /// 预览：根据当前变量池内容显示预期的字段值
        /// </summary>
        public string Preview
        {
            get => _preview;
            set => SetProperty(ref _preview, value);
        }

        public LengthType LengthType
        {
            get => _lengthType;
            set => SetProperty(ref _lengthType, value);
        }

        // 长度来源字段名（如 "CodeLength"）
        public string LengthSourceField
        {
            get => _lengthSourceField;
            set => SetProperty(ref _lengthSourceField, value);
        }

        // 长度偏移
        public int LengthOffset
        {
            get => _lengthOffset;
            set => SetProperty(ref _lengthOffset, value);
        }

        /// <summary>
        /// 如果是动态长度，从变量池查询；否则返回 Fixed 长度
        /// </summary>
        /// <returns></returns>
        public int GetActualLength(Dictionary<string, string> variablePool)
        {
            if (LengthType == LengthType.Dynamic && !string.IsNullOrEmpty(LengthSourceField))
            {
                if (variablePool.TryGetValue(LengthSourceField, out string lengthStr) &&
                    int.TryParse(lengthStr, out int dynamicLength))
                {
                    return dynamicLength + LengthOffset;
                }
                return LengthOffset;
            }
            return Length;
        }
    }

    public class FullProtocolConfig
    {
        public string TemplateName { get; set; } = "DefaultTemplate";
        public string Description { get; set; } = "";
        public List<ProtocolField> InputFields { get; set; } = new List<ProtocolField>();
        public List<ProtocolField> OutputFields { get; set; } = new List<ProtocolField>();
    }
}
