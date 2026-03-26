using aoi_common.Models;
using DryIoc;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;


namespace aoi_common.Services
{
    public interface IParametersConfigService
    {
        ObservableCollection<ParametersConfig> ConfigParams { get; }
        void LoadConfig();
        bool SaveConfig();
        bool SaveConfig(IEnumerable<ParametersConfig> configs);
        void UpdateParam(string moduleName, string paramName, string value, ParamOutputType type = ParamOutputType.STRING);
        int GetInt(string moduleName, string paramName, int defaultValue = 0);
        double GetDouble(string moduleName, string paramName, double defaultValue = 0.0);
        string GetString(string moduleName, string paramName, string defaultValue = "");
        bool GetBool(string moduleName, string paramName, bool defaultValue = false);
    }

    public class ParametersConfigService : IParametersConfigService
    {
        private readonly string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config/ConfigParas.json");
        public ObservableCollection<ParametersConfig> ConfigParams { get; private set; }


        public ParametersConfigService()
        {
            ConfigParams =new ObservableCollection<ParametersConfig>();
            LoadConfig();
        }
        public int GetInt(string moduleName, string paramName, int defaultValue = 0)
        {
            var p = FindParam(moduleName, paramName);
            if (p != null && int.TryParse(p.InitValue, out int result)) return result;
            return defaultValue;
        }

        public double GetDouble(string moduleName, string paramName, double defaultValue = 0.0)
        {
            var p = FindParam(moduleName, paramName);
            if (p != null && double.TryParse(p.InitValue, out double result)) return result;
            return defaultValue;
        }

        public string GetString(string moduleName, string paramName, string defaultValue = "")
        {
            var p = FindParam(moduleName, paramName);
            return p != null ? p.InitValue : defaultValue;
        }

        public bool GetBool(string moduleName, string paramName, bool defaultValue = false)
        {
            var p = FindParam(moduleName, paramName);
            if (p != null && bool.TryParse(p.InitValue, out bool result)) return result;
            if (p != null && p.InitValue == "1") return true;
            if (p != null && p.InitValue == "0") return false;
            return defaultValue;
        }

        public void UpdateParam(string moduleName, string paramName, string value, ParamOutputType type = ParamOutputType.STRING)
        {
            var p = FindParam(moduleName, paramName);
            if (p != null)
            {
                p.InitValue = value;
                p.OutputType = type; 
            }
            else
            {
                ConfigParams.Add(new ParametersConfig
                {
                    ModuleName = moduleName,
                    Name = paramName,
                    InitValue = value,
                    OutputType = type,
                });
            }
        }

        public void LoadConfig()
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    var json = File.ReadAllText(_configPath);
                    var list = JsonConvert.DeserializeObject<ObservableCollection<ParametersConfig>>(json);
                    ConfigParams.Clear();
                    if (list != null)
                    {
                        foreach (var p in list) ConfigParams.Add(p);
                        Log.Information("参数配置加载成功，共 {Count} 项", ConfigParams.Count);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "加载配置文件 ConfigParas.json 失败");
                }
            }
            else
            {
                Log.Warning("配置文件不存在: {Path}", _configPath);
            }
        }

        public bool SaveConfig(IEnumerable<ParametersConfig> configs)
        {
            try
            {
                var json = JsonConvert.SerializeObject(configs, Formatting.Indented);
                File.WriteAllText(_configPath, json);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "配置保存失败");
                return false;
            }
        }

        public bool SaveConfig()
        {
            return SaveConfig(this.ConfigParams);
        }

        private ParametersConfig FindParam(string moduleName, string paramName)
        {
            // 如果 moduleName 为空，则在全局搜索 paramName；否则按模块匹配
            return ConfigParams.FirstOrDefault(p =>
                (string.IsNullOrEmpty(moduleName) || p.ModuleName == moduleName) &&
                p.Name == paramName);
        }
    }
}
