using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace aoi_common.Models
{
    // 字段来源：固定文本、变量池映射、空白填充
    public enum FieldSource { Fixed, Variable, Padding }

    public class ProtocolField:BindableBase
    {
        private int _index;
        private string _name = "";
        private FieldSource _source = FieldSource.Variable;
        private int _length;
        private double _scale = 1.0;
        private string _fixedValue = "";

        // 1. 排序索引 (0, 1, 2...)
        public int Index { get => _index; set { _index = value; SetProperty(ref _index,value); } }

        // 2. 自定义名称 (对应变量池中的Key)
        public string Name { get => _name; set { _name = value; SetProperty(ref _name,value); } }

        // 3. 来源类型
        public FieldSource Source { get => _source; set { _source = value; SetProperty(ref _source,value); } }

        // 4. 占用长度
        public int Length { get => _length; set { _length = value; SetProperty(ref _length ,value); } }

        // 5. 倍率 (仅Variable生效)
        public double Scale { get => _scale; set { _scale = value; SetProperty( ref _scale,value); } }

        // 6. 固定值/默认值 (Fixed类型存文本, Variable类型存默认空值)
        public string FixedValue { get => _fixedValue; set { _fixedValue = value; SetProperty(ref _fixedValue ,value); } }

       
    }

    public class FullProtocolConfig
    {
        public string TemplateName { get; set; }
        public List<ProtocolField> InputFields { get; set; } = new List<ProtocolField>();
        public List<ProtocolField> OutputFields { get; set; } = new List<ProtocolField>();
    }
}
