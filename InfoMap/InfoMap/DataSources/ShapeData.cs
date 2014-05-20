using DotSpatial.Data;
using SharpGL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfoMap.DataSources
{
  class ShapeData : Renderable
  {
    private IFeatureSet fs;
    private int lineCount = 0;
    private float r;
    private float g;
    private float b;
    private string filename;

    public ShapeData(string filename, float r, float g, float b, string category, string description)
    {
      this.filename = filename;
      this.category = category;
      this.description = description;
      this.r = r;
      this.g = g;
      this.b = b;
    }

    public override async Task<bool> Load()
    {
      lineCount = 0;
      fs = FeatureSet.Open(filename);
      var shapes = fs.ShapeIndices.ToList();
      foreach (var shape in shapes)
      {
        foreach (var part in shape.Parts)
        {
          lineCount += (part.NumVertices - 1);
        }
      }
      this.vertexArray = new float[lineCount * 2 * 3]; //2 points in each line  3 dim:xyz
      this.colorArray = new float[lineCount * 2 * 4];

      int  lineIndex = 0;

      foreach (var shape in shapes)
      {
        foreach (var part in shape.Parts)
        {
          for (int i = 2; i < part.NumVertices; i++)
          {
            int baseIndex = (2 * part.StartIndex) + i * 2;

            int baseVertex = 3 * 2 * lineIndex;
            this.vertexArray[baseVertex + 0] = ConvertLongitude(fs.Vertex[baseIndex - 2]);  //long1
            this.vertexArray[baseVertex + 1] = ConvertLatitude(fs.Vertex[baseIndex - 3]); //lat1
            this.vertexArray[baseVertex + 2] = 0.0f;
            this.vertexArray[baseVertex + 3] = ConvertLongitude(fs.Vertex[baseIndex - 0]); //long2
            this.vertexArray[baseVertex + 4] = ConvertLatitude(fs.Vertex[baseIndex - 1]); //lat2
            this.vertexArray[baseVertex + 5] = 0.0f;

            int baseColor = 4 * 2 * lineIndex;
            float alpha = 1.0f;
            this.colorArray[baseColor + 0] = r;
            this.colorArray[baseColor + 1] = g;
            this.colorArray[baseColor + 2] = b;
            this.colorArray[baseColor + 3] = alpha;
            this.colorArray[baseColor + 4] = r;
            this.colorArray[baseColor + 5] = g;
            this.colorArray[baseColor + 6] = b;
            this.colorArray[baseColor + 7] = alpha;
            lineIndex++;
          }
        }
      }
      //this.isLoaded = true;
      return true;
    }

    private float ConvertLatitude(double latitude)
    {
      const int mapWidth = 360;
      const int mapHeight = 180;

      // convert from degrees to radians
      var latRad = latitude * Math.PI / 180;

      // get y value
      var mercN =Math.Log(Math.Tan((Math.PI / 4) + (latRad / 2)));
      var y = (mapHeight / 2) - (mapWidth * mercN / (2 * Math.PI));
      return (float)y;
    }

    private float ConvertLongitude(double longitude)
    {
      const int mapWidth = 360;
      const int mapHeight = 180;

      // get x value
      var x = (longitude + 180) * (mapWidth / 360.0);
      return (float)x;
    }


    public override void Draw(OpenGL gl)
    {
      this.vertexBufferArray.Bind(gl);
      gl.DrawArrays(OpenGL.GL_LINES, 0, this.lineCount*2);
      this.vertexBufferArray.Unbind(gl);
    }
  }
}
