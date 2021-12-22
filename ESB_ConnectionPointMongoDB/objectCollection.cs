using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ESB_ConnectionPoints.PluginsInterfaces;

namespace ESB_ConnectionPointMongoDB
{
   public class ObjectCollection
    {
        public System.Guid Id { get; set; }
        public string Name { get; set; }
        public string ClassId { get; set; }
        public string Type { get; set; }
        public System.Guid CorrelationId { get; set; }
        public byte[] Body { get; set; }
        public DateTime Date { get; set; }
        public int PackageNumber { get; set; }
    }
}
