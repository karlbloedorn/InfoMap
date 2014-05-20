using SharpGL;
using SharpGL.VertexBuffers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfoMap.DataSources
{
  public abstract class Renderable
  {
    public float[] vertexArray;
    public float[] colorArray;
    public VertexBufferArray vertexBufferArray;
    
    public bool isSetup = false;
    public bool didFail = false;

    public string category = "";
    public string description = "";

    public void Setup(OpenGL gl)
    {
      this.vertexBufferArray = new VertexBufferArray();
      this.vertexBufferArray.Create(gl);
      this.vertexBufferArray.Bind(gl);

      var vertexDataBuffer = new VertexBuffer();
      vertexDataBuffer.Create(gl);
      vertexDataBuffer.Bind(gl);
      vertexDataBuffer.SetData(gl, 0, this.vertexArray, false, 3);
      var colourDataBuffer = new VertexBuffer();

      colourDataBuffer.Create(gl);
      colourDataBuffer.Bind(gl);
      colourDataBuffer.SetData(gl, 1, this.colorArray, false, 4);
      this.vertexBufferArray.Unbind(gl);

      Debug.WriteLine("used " + (this.vertexArray.Length * 3 + this.colorArray.Length * 4) / 1000000 + " MB");
      this.vertexArray = null;
      this.colorArray = null;
      this.isSetup = true;
    }

    public void Teardown(OpenGL gl)
    {
      if (this.didFail)
      {
        return;
      }
      if (this.isSetup)
      {
        this.vertexBufferArray.Delete(gl);
        this.isSetup = false;
      }
    }

    public virtual async Task<bool> Load()
    {
      return true;
    }
    public abstract void Draw(OpenGL gl);
  }
}
