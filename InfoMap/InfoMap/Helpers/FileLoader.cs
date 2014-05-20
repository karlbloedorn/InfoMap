using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfoMap.Helpers
{
  public static class FileLoader
  {
    public static string LoadTextFile(string textFileName)
    {
      using (StreamReader sr = new StreamReader(textFileName))
      {
        return sr.ReadToEnd();
      }
    }
  }
}
