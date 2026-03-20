using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace aoi_common.Models
{
    public enum ParamOutputType
    {
        INT,
        FLOAT,
        BOOL,
        STRING
    }
    public class ParametersConfig : BindableBase
    {
        private string _name;
        public string Name { get => _name; set => SetProperty(ref _name, value); }

        private string _expression;
        public string Expression { get => _expression; set => SetProperty(ref _expression, value); }

        private string _note;
        public string Note { get => _note; set => SetProperty(ref _note, value); }

        private string _initValue;
        public string InitValue { get => _initValue; set => SetProperty(ref _initValue, value); }

        private ParamOutputType _outputType = ParamOutputType.FLOAT;
        public ParamOutputType OutputType { get => _outputType; set => SetProperty(ref _outputType, value); }

        private string _moduleName;

        public string ModuleName
        {
            get { return _moduleName; }
            set { _moduleName = value; }
        }
    }
}
