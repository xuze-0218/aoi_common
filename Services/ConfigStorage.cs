using aoi_common.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace aoi_common.Services
{
    public class ConfigStorage
    {
        public static void Save(string path, FullProtocolConfig config)
        {
            string json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        public static FullProtocolConfig Load(string path)
        {
            if (!File.Exists(path)) return new FullProtocolConfig();
            return JsonConvert.DeserializeObject<FullProtocolConfig>(File.ReadAllText(path));
        }
    }
}
