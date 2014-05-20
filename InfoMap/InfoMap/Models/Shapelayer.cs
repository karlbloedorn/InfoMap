using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfoMap.Models
{
  public class Shapelayer
  {
      public string file { get; set; }
      public float r { get; set; }
      public float g { get; set; }
      public float b { get; set; }
      public string category { get; set; }
      public string description { get; set; }
      public bool enabled { get; set; }
  }
}
