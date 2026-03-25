using aoi_common.Models;
using aoi_common.Services;
using Newtonsoft.Json;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace aoi_common.ViewModels
{
    public class ParamConfigViewModel : BindableBase, IDialogAware
    {

        private string _lastModuleName = "未分类模块";
        private ICollectionView _parametersView;
        private IParametersConfigService _configService;
        private bool _isInitialized = false;
        public ObservableCollection<ParametersConfig> Parameters => _configService.ConfigParams;


        public ICollectionView ParametersView
        {
            get => _parametersView;
            set => SetProperty(ref _parametersView, value);
        }
        private ParametersConfig _selectedParameter;
        public ParametersConfig SelectedParameter
        {
            get => _selectedParameter;
            set
            {
                if (SetProperty(ref _selectedParameter, value) && value != null)
                {
                    _lastModuleName = value.ModuleName;
                }
            }
        }
        public IEnumerable<ParamOutputType> DataTypeValues => Enum.GetValues(typeof(ParamOutputType)).Cast<ParamOutputType>();
        public string Title => "参数配置";

        public DelegateCommand AddCommand { get; }
        public DelegateCommand<ParametersConfig> DeleteCommand { get; }
        public DelegateCommand SaveCommand { get; }

        public event Action<IDialogResult> RequestClose;

        public ParamConfigViewModel(IParametersConfigService configService)
        {
            _configService = configService;
        
            AddCommand = new DelegateCommand(() =>
            {
                if (SelectedParameter != null)
                {
                    _lastModuleName = SelectedParameter.ModuleName;
                }

                var newParam = new ParametersConfig
                {
                    Name = "New_Param",
                    ModuleName = _lastModuleName
                };

                newParam.PropertyChanged += OnParameterPropertyChanged;

                Parameters.Add(newParam);
            });

            DeleteCommand = new DelegateCommand<ParametersConfig>(p => Parameters.Remove(p));
            SaveCommand = new DelegateCommand(() => _configService.SaveConfig(Parameters));
        }
        private void OnParameterPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ParametersConfig.ModuleName) && sender is ParametersConfig p)
            {
                _lastModuleName = p.ModuleName;
            }
        }

        public bool CanCloseDialog() => true;

        public void OnDialogClosed() { _isInitialized = false; }

        public void OnDialogOpened(IDialogParameters parameters) 
        {
            if (_isInitialized) return;
            _isInitialized = true;

            if (ParametersView != null)
            {
                ParametersView.GroupDescriptions.Clear();
                ParametersView.SortDescriptions.Clear();
            }
            ParametersView = CollectionViewSource.GetDefaultView(Parameters);
            ParametersView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ParametersConfig.ModuleName)));
            ParametersView.SortDescriptions.Add(new SortDescription(nameof(ParametersConfig.ModuleName), ListSortDirection.Ascending));
            ParametersView.SortDescriptions.Add(new SortDescription(nameof(ParametersConfig.Name), ListSortDirection.Ascending));
        }
    }
}