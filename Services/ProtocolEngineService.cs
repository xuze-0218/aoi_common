using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace aoi_common.Services
{
    public class ProtocolEngineService
    {
        // 全局变量池：所有数据以 String 存储
        public Dictionary<string, string> VariablePool { get; private set; } = new Dictionary<string, string>();

        // 解析接收电文：按照配置的 Offset 和 Length 截取并存入池子
        public void ParseInput(string rawData, List<Models.ProtocolField> config)
        {
            foreach (var field in config)
            {
                // 注意：解析时根据 Index 或逻辑起始位截取
                // 这里假设解析配置里存了 StartIndex（可以通过 FixedValue 临时借用或扩展字段）
                if (int.TryParse(field.FixedValue, out int start) && rawData.Length >= start + field.Length)
                {
                    VariablePool[field.Name] = rawData.Substring(start, field.Length).Trim();
                }
            }
        }

        // 视觉结果写入变量池
        public void SetVariable(string name, object value)
        {
            VariablePool[name] = value?.ToString() ?? "";
        }

        // 核心功能：拼装发送电文 (按索引排序)
        public string BuildOutput(List<Models.ProtocolField> config)
        {
            StringBuilder sb = new StringBuilder();

            // 关键：严格按 Index 从小到大排序
            var sortedFields = config.OrderBy(f => f.Index).ToList();

            foreach (var field in sortedFields)
            {
                string rawContent = "";

                switch (field.Source)
                {
                    case Models.FieldSource.Fixed:
                        rawContent = field.FixedValue;
                        break;
                    case Models.FieldSource.Padding:
                        rawContent = ""; // 后面补齐长度
                        break;
                    case Models.FieldSource.Variable:
                        if (VariablePool.TryGetValue(field.Name, out string val))
                        {
                            // 处理缩放 (String -> Double -> Multiply -> String)
                            if (field.Scale != 1.0 && double.TryParse(val, out double d))
                                rawContent = Math.Round(d * field.Scale).ToString();
                            else
                                rawContent = val;
                        }
                        else
                        {
                            rawContent = field.FixedValue; // 变量池没有则取默认值
                        }
                        break;
                }

                // 强制长度对齐：不足补'0'，超出截断
                if (rawContent.Length > field.Length)
                    sb.Append(rawContent.Substring(0, field.Length));
                else
                    sb.Append(rawContent.PadLeft(field.Length, '0'));
            }

            return sb.ToString();
        }
    }
}
