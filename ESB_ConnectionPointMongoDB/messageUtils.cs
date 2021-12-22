using ESB_ConnectionPoints.PluginsInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESB_ConnectionPointMongoDB
{
    public static class messageUtils
    {
        public static string getPropeties(Dictionary<string, IMessagePropertyData> properties , string nameProperties)
        {
            if (properties.ContainsKey(nameProperties))
              return properties[nameProperties].ToString();
            return "0";
        }
    }
}
