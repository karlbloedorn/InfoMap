using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfoMap.Models
{
  public class NexradStation
  {
    public string idn { get; set; }
    public string id { get; set; }
    public string name { get; set; }
    public string st { get; set; }
    public string co { get; set; }
    public string lat { get; set; }
    public string lon { get; set; }
    public string elev { get; set; }
    public bool enabled { get; set; }
  }

  public class NexradStationTable
  {
    public string name { get; set; }
    public string category { get; set; }
    public List<NexradStation> station { get; set; }
  }

  public class NexradStationFile
  {
    public NexradStationTable stationtable { get; set; }
  }
}
