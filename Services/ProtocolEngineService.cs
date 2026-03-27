using aoi_common.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace aoi_common.Services
{

    public interface IProtocolEngineService
    {
        Dictionary<string, string> VariablePool { get; }
        /// <summary>
        /// 解析输入电文：根据配置的 Offset 和 Length 截取并存入变量池
        /// </summary>
        /// <param name="rawData"></param>
        /// <param name="inputConfig"></param>
        void ParseInput(string rawData, List<ProtocolField> inputConfig);
        /// <summary>
        /// 构建输出电文：按照配置的 Index 从变量池取值（或固定值/空白），处理缩放和长度对齐，最终拼接成完整字符串
        /// </summary>
        /// <param name="outputConfig"></param>
        /// <returns></returns>
        string BuildOutput(List<ProtocolField> outputConfig);
        void SetVariable(string name, object value);
        void ClearVariables();
        string GetVariable(string name);
    }

    public class ProtocolEngineService: IProtocolEngineService
    {
        private readonly ILogger _logger;

        public Dictionary<string, string> VariablePool { get; private set; } = new Dictionary<string, string>();

        public ProtocolEngineService(ILogger logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// 解析接收的原始报文：按配置截取字符串并存入变量池
        /// </summary>
        public void ParseInput(string rawData, List<ProtocolField> inputConfig)
        {
            if (string.IsNullOrEmpty(rawData))
            {
                _logger?.Warning("接收数据为空");
                return;
            }

            try
            {
                var sortedFields = inputConfig.OrderBy(f => f.StartIndex).ToList();
                foreach (var field in inputConfig)
                {
                    int start = field.StartIndex;
                    int length = field.GetActualLength(VariablePool);

                    // 边界检查
                    if (start < 0 || length <= 0)
                    {
                        _logger?.Warning("字段 {FieldName} 配置无效 (Start={Start}, Length={Length})",
                            field.Name, start, length);
                        continue;
                    }

                    if (rawData.Length < start + length)
                    {
                        _logger?.Warning("字段 {FieldName} 超出数据范围 (需要 {Required}, 实际 {Actual})",
                            field.Name, start + length, rawData.Length);
                        continue;
                    }

                    string value = rawData.Substring(start, length).Trim();
                    VariablePool[field.Name] = value;

                    _logger?.Debug("解析字段: {FieldName} = '{Value}'", field.Name, value);
                }
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "解析输入数据异常");
            }
        }

        /// <summary>
        /// 拼装输出报文：按 Index 顺序从上往下依次拼接字符串
        /// </summary>
        public string BuildOutput(List<ProtocolField> outputConfig)
        {
            if (outputConfig == null || outputConfig.Count == 0)
            {
                _logger?.Warning("输出配置为空");
                return string.Empty;
            }

            try
            {
                var sb = new StringBuilder();
                //严格按 Index 从小到大排序
                var sortedFields = outputConfig.OrderBy(f => f.Index).ToList();

                foreach (var field in sortedFields)
                {
                    string rawContent = GetFieldContent(field);
                    int actualLength = field.GetActualLength(VariablePool);
                    // 强制长度对齐：不足补'0'，超出截断
                    string alignedContent = AlignContent(rawContent, actualLength);
                    sb.Append(alignedContent);

                    _logger?.Debug("字段 {FieldName} → '{Content}' (长度: {Length})",
                        field.Name, alignedContent, field.Length);
                }

                string result = sb.ToString();
                _logger?.Information("生成输出报文，总长度: {Length}", result.Length);
                return result;
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "构建输出报文异常");
                return string.Empty;
            }
        }

        /// <summary>
        /// 获取字段内容
        /// </summary>
        private string GetFieldContent(ProtocolField field)
        {
            switch (field.Source)
            {
                case FieldSource.Fixed:  // 固定值直接返回
                    return field.FixedValue ?? "";

                case FieldSource.Variable:// 从变量池取值
                    if (VariablePool.TryGetValue(field.Name, out string val))
                    {
                        //处理缩放：float 1.24 * 1000 → "1240"
                        if (field.Scale != 1.0 && double.TryParse(val, out double d))
                        {
                            return Math.Round(d * field.Scale).ToString();
                        }
                        return val;
                    }
                    // 变量不存在，使用默认值
                    return field.FixedValue ?? "";
                case FieldSource.Padding:  // 填充模式返回空，长度对齐会补齐
                    return "";

                default:
                    return "";
            }
        }

        /// <summary>
        /// 内容长度对齐
        /// </summary>
        private string AlignContent(string content, int length)
        {
            if (content.Length > length)
                return content.Substring(0, length);  // 超长截断
            else
                return content.PadLeft(length, '0');  // 不足补'0'
        }

        public void SetVariable(string name, object value)
        {
            VariablePool[name] = value?.ToString() ?? "";
            _logger?.Debug("设置变量: {Name} = '{Value}'", name, value);
        }

        public string GetVariable(string name)
        {
            return VariablePool.TryGetValue(name, out var value) ? value : "";
        }

        public void ClearVariables()
        {
            VariablePool.Clear();
            _logger?.Debug("变量池已清空");
        }
    }
}
