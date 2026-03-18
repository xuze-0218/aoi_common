using aoi_common.Models;
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
        private readonly string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config/ConfigParas.json");

        // 原始数据集合
        public ObservableCollection<ConfigParam> Parameters { get; set; } = new ObservableCollection<ConfigParam>();
        private string _lastModuleName = "未分类模块";
        private ICollectionView _parametersView;
      
        /// <summary>
        /// 用于前端 DataGrid 绑定的视图，支持分组和过滤
        /// </summary>
        public ICollectionView ParametersView
        {
            get => _parametersView;
            set => SetProperty(ref _parametersView, value);
        }
        private ConfigParam _selectedParameter;
        public ConfigParam SelectedParameter
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
        public DelegateCommand<ConfigParam> DeleteCommand { get; }
        public DelegateCommand SaveCommand { get; }

        public event Action<IDialogResult> RequestClose;

        public ParamConfigViewModel()
        {
            LoadConfig();

            ParametersView = CollectionViewSource.GetDefaultView(Parameters);
            ParametersView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ConfigParam.ModuleName)));
            ParametersView.SortDescriptions.Add(new SortDescription(nameof(ConfigParam.ModuleName), ListSortDirection.Ascending));
            ParametersView.SortDescriptions.Add(new SortDescription(nameof(ConfigParam.Name), ListSortDirection.Ascending));

            AddCommand = new DelegateCommand(() =>
            {
                if (SelectedParameter != null)
                {
                    _lastModuleName = SelectedParameter.ModuleName;
                }

                var newParam = new ConfigParam
                {
                    Name = "New_Param",
                    ModuleName = _lastModuleName
                };

                newParam.PropertyChanged += OnParameterPropertyChanged;

                Parameters.Add(newParam);
            });

            DeleteCommand = new DelegateCommand<ConfigParam>(p => Parameters.Remove(p));
            SaveCommand = new DelegateCommand(SaveConfig);
        }
        private void OnParameterPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ConfigParam.ModuleName) && sender is ConfigParam p)
            {
                _lastModuleName = p.ModuleName;
            }
        }
        private void SaveConfig()
        {
            try
            {
                var json = JsonConvert.SerializeObject(Parameters, Formatting.Indented);
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}");
            }
        }

        private void LoadConfig()
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    var json = File.ReadAllText(_configPath);
                    var list = JsonConvert.DeserializeObject<List<ConfigParam>>(json);
                    Parameters.Clear();
                    if (list != null)
                    {
                        list.ForEach(p => {
                            p.PropertyChanged += OnParameterPropertyChanged;
                            Parameters.Add(p);
                        });
                        if (list.Count > 0) _lastModuleName = list.Last().ModuleName;
                    }
                }
                catch (Exception)
                {
                }
            }
            else
            {
                string configDirectory = Path.GetDirectoryName(_configPath);
                if (!string.IsNullOrEmpty(configDirectory) && !Directory.Exists(configDirectory))
                {
                    Directory.CreateDirectory(configDirectory);
                }
            }
        }

        public bool CanCloseDialog() => true;

        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters parameters) { }
    }
}