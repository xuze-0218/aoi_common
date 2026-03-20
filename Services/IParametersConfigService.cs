using aoi_common.Models;
using DryIoc;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace aoi_common.Services
{
    public interface IParametersConfigService
    {
        ObservableCollection<ParametersConfig> ConfigParams { get; }
        void LoadConfig();
        bool SaveConfig(IEnumerable<ParametersConfig> configs);
        int GetInt(string moduleName, string paramName, int defaultValue = 0);
        float GetFloat(string moduleName, string paramName, float defaultValue = 0f);
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

        public float GetFloat(string moduleName, string paramName, float defaultValue = 0f)
        {
            var p = FindParam(moduleName, paramName);
            if (p != null && float.TryParse(p.InitValue, out float result)) return result;
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

        private ParametersConfig FindParam(string moduleName, string paramName)
        {
            // 如果 moduleName 为空，则在全局搜索 paramName；否则按模块匹配
            return ConfigParams.FirstOrDefault(p =>
                (string.IsNullOrEmpty(moduleName) || p.ModuleName == moduleName) &&
                p.Name == paramName);
        }
    }
}
