using GlmNet;
using InfoMap.DataSources;
using InfoMap.Helpers;
using InfoMap.Models;
using Newtonsoft.Json;
using SharpGL;
using SharpGL.Enumerations;
using SharpGL.Shaders;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;

namespace InfoMap
{
  public partial class Map : Form
  {
    private List<Renderable> all_renderables = new List<Renderable>();
    private BlockingCollection<Renderable> load_renderables = new BlockingCollection<Renderable>();
    private BlockingCollection<Renderable> ready_renderables = new BlockingCollection<Renderable>();
    private BlockingCollection<Renderable> unload_renderables = new BlockingCollection<Renderable>();
    private List<Renderable> active_renderables = new List<Renderable>();


    public async void loaderThreadFunc()
    {
      while (true)
      {
       var renderable = load_renderables.Take();
       try
       {
         await renderable.Load();
         this.ready_renderables.Add(renderable);
       }
       catch (Exception e)
       {
         renderable.didFail = true;
         Debug.WriteLine("failed loading : " + renderable.description);
       }
      }
    }

    private DateTime lastTime = DateTime.UtcNow;
    private DateTime startTime = DateTime.UtcNow;
    private ShaderProgram shaderProgram;
    private mat4 projectionMatrix;
    private mat4 modelMatrix;

    private float scale = 5.0f;
    private int frameCount = 0;
    private float offsetX = 0.0f;
    private float offsetY = 0.0f;

    Thread loadThread;
    Thread loadThread2;
    Thread loadThread3;

    public Map()
    {
      InitializeComponent();
    }
    private async void MapLoad(object sender, EventArgs e)
    {
      var shapelayerText = FileLoader.LoadTextFile("config/shapelayers.json");
      var shapelayers = JsonConvert.DeserializeObject<List<Shapelayer>>(shapelayerText);
      var stationText = FileLoader.LoadTextFile("config/stations.json");
      var stations = JsonConvert.DeserializeObject<NexradStationFile>(stationText);

      var us_stations = stations.stationtable.station.Where(x => x.co == "US");
      var states = us_stations.GroupBy(x => x.st).OrderBy(x => x.Key);
      var list = new ListView();
      list.Columns.Add(new ColumnHeader() { Width = list.Width - 10 });
      list.HeaderStyle = ColumnHeaderStyle.None; 
      list.Alignment = ListViewAlignment.Left;
      list.AutoArrange = false;
      list.CheckBoxes = true;
      list.Margin = new Padding(30);
      list.Height = 500;
      list.Width = 250;
      list.ShowGroups = true;
      list.View = View.Details;
   
      foreach (var shapelayer in shapelayers)
      {
        if (list.Groups[shapelayer.category] == null)
        {
          list.Groups.Add(shapelayer.category, shapelayer.category);
        }
        var cur_renderable = new ShapeData(shapelayer.file, shapelayer.r, shapelayer.g, shapelayer.b, shapelayer.category, shapelayer.description);
        all_renderables.Add(cur_renderable);
        var item = list.Items.Add(shapelayer.description);
        item.Group = list.Groups[shapelayer.category];
        item.Checked = shapelayer.enabled;
        if (item.Checked)
        {
          load_renderables.Add(cur_renderable);
        }
      }

      for (int i = 0; i < states.Count(); i++)
      {
        var grp = list.Groups.Add(states.ElementAt(i).Key, states.ElementAt(i).Key);   
      }
      foreach(var station in us_stations){
        var cur_renderable = new RadarScan("K" + station.id, station.st);
        all_renderables.Add(cur_renderable);
        var item = list.Items.Add("K" + station.id + " " + station.name);
        item.Group = list.Groups[station.st];
        item.Checked = station.enabled;
        if (item.Checked)
        {
          load_renderables.Add(cur_renderable);
        }
      }

      list.AutoResizeColumn(0, ColumnHeaderAutoResizeStyle.ColumnContent);
      panel1.Controls.Add(list);
      list.ItemChecked += list_ItemChecked;


      loadThread = new Thread(new ThreadStart(loaderThreadFunc));
      loadThread.Start();
      loadThread2 = new Thread(new ThreadStart(loaderThreadFunc));
      loadThread2.Start();
      loadThread3 = new Thread(new ThreadStart(loaderThreadFunc));
      loadThread3.Start();

      overlayControl.OpenGLDraw += overlayControl_Draw;
      overlayControl.Resized += overlayControl_Resized;

      var gl = overlayControl.OpenGL;
      gl.Enable(OpenGL.GL_BLEND);
      gl.BlendFunc(BlendingSourceFactor.SourceAlpha, BlendingDestinationFactor.OneMinusSourceAlpha);
      var vertexShaderSource = FileLoader.LoadTextFile("Shader.vert");
      var fragmentShaderSource = FileLoader.LoadTextFile("Shader.frag");
      shaderProgram = new ShaderProgram();
      shaderProgram.Create(gl, vertexShaderSource, fragmentShaderSource, null);
      shaderProgram.BindAttributeLocation(gl, 0, "in_Position");
      shaderProgram.BindAttributeLocation(gl, 1, "in_Color");
      shaderProgram.AssertValid(gl);

      overlayControl_Resized(null, null);

      overlayControl.Click += overlayControl_Click;
      overlayControl.MouseMove += OnMouseMove;
      overlayControl.MouseDown += OnMouseDown;
      overlayControl.MouseUp += OnMouseUp;
      overlayControl.MouseWheel += overlayControl_MouseWheel;
    }

    void list_ItemChecked(object sender, ItemCheckedEventArgs e)
    {
      var d = e.Item; 
      
      var words = d.Text.Split(' ');
      var matching = all_renderables.FirstOrDefault(x => x.category == d.Group.Name && x.description == words[0]);
      if (matching != null)
      {
        if (d.Checked)
        {
          load_renderables.Add(matching);
        }
        else
        {
          unload_renderables.Add(matching);
        }
      }
    }

    void overlayControl_MouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
    {

      if (Form.ActiveForm == null)
      {
        return;
      }
      var gl = overlayControl.OpenGL;

      var clientMouse = PointToClient(MousePosition);


      var mouseX = clientMouse.X - overlayControl.Left;
      var mouseY = overlayControl.Height- clientMouse.Y - overlayControl.Top;
      
      if (e.Delta != 0)
      {

        double curX = 0;
        double curY = 0;
        double curZ = 0;
        
        gl.UnProject(mouseX, 
                    mouseY, 
                    0, 
                    Array.ConvertAll<float, double>(modelMatrix.to_array(),Convert.ToDouble ),
                    Array.ConvertAll<float, double>(projectionMatrix.to_array(), Convert.ToDouble),
                    new int[]{ 0,0,(int)overlayControl.Width,(int)overlayControl.Height },
                    ref curX, ref curY, ref curZ);

        // Debug.WriteLine("derp " + e.Delta / 5000.0);
        scale *= (1+( e.Delta / 1000.0f));

        //Debug.WriteLine("scale: " + scale);
        var futureUserOffsetModel = glm.translate(mat4.identity(), new vec3(offsetX, offsetY, 0));
        var futureScaledModel = glm.scale(futureUserOffsetModel, new vec3(scale, scale, 1.0f));
        
        var futureX = new double[1];
        var futureY = new double[1];
        var futureZ = new double[1];

        gl.Project(curX,
                   curY,
                   0,
                   Array.ConvertAll<float, double>(futureScaledModel.to_array(), Convert.ToDouble),
                   Array.ConvertAll<float, double>(projectionMatrix.to_array(), Convert.ToDouble),
                    new int[] { 0, 0, (int)overlayControl.Width, (int)overlayControl.Height },
                   futureX,  futureY,  futureZ
                  );

         offsetX -= (float)(futureX[0] - mouseX);
         offsetY += (float)(futureY[0] - mouseY);
      }
    }


    bool _mousePressed;
    Point _mousePos;
    private void OnMouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
    {
      _mousePressed = true;
      _mousePos = new Point(e.X, e.Y);
    }

    private void OnMouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
    {
      if (_mousePressed)
      {
        int deltaX = e.X - _mousePos.X;
        int deltaY = e.Y - _mousePos.Y;
        _mousePos = new Point(e.X, e.Y);
        offsetX += deltaX;
        offsetY += deltaY;
      }
    }

    private void OnMouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
    {
      _mousePressed = false;
    }

    void overlayControl_Click(object sender, EventArgs e)
    {
    }

    void overlayControl_Resized(object sender, EventArgs e)
    {
      var gl = overlayControl.OpenGL;
      float top = 0f;
      float bottom = (float)overlayControl.Height;
      float left = 0f;
      float right = (float)overlayControl.Width;
      float far = 10f;
      float near = -10f;
      var col1 = new vec4(2 / (right - left), 0, 0, 0);
      var col2 = new vec4(0, 2 / (top - bottom), 0, 0);
      var col3 = new vec4(0, 0, -(2 / (far - near)), 0);
      var col4 = new vec4(-((right + left) / (right - left)), -((top + bottom) / (top - bottom)), (far + near) / (far - near), 1);
      projectionMatrix = new mat4(new vec4[] { col1, col2, col3, col4 });
    }

    private void overlayControl_Draw(object sender, RenderEventArgs args)
    {
      if (Form.ActiveForm != null)
      {
        if (Keyboard.IsKeyDown(Key.Escape))
        {
          Application.Exit();
        }
      }
   
      var gl = overlayControl.OpenGL;
      gl.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
      gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT | OpenGL.GL_STENCIL_BUFFER_BIT);

      if (shaderProgram == null)
      {
        return;
      }
      var curTime = DateTime.UtcNow;
      float delta = (curTime  - lastTime).Ticks / 50000.0f;
      lastTime = curTime;

      var userOffsetModel = glm.translate(mat4.identity(), new vec3(offsetX, offsetY, 0));
      var scaledModel = glm.scale(userOffsetModel, new vec3(scale, scale, 1.0f));

      modelMatrix = scaledModel;
      shaderProgram.Bind(gl);
      shaderProgram.SetUniformMatrix4(gl, "projectionMatrix", projectionMatrix.to_array());
      shaderProgram.SetUniformMatrix4(gl, "modelMatrix", modelMatrix.to_array());

      Renderable readyRenderable = null;
      if( this.ready_renderables.TryTake(out readyRenderable)){
        Debug.WriteLine("setting up : " + readyRenderable.description);
        readyRenderable.Setup(gl);
        this.active_renderables.Add(readyRenderable);
      }
     
      Renderable unloadRenderable = null;
      if (this.unload_renderables.TryTake(out unloadRenderable))
      {
        Debug.WriteLine("tearing down : " + unloadRenderable.description);
        unloadRenderable.Teardown(gl);
      }
      var sortedRenderables = this.active_renderables.OrderByDescending(x => x is RadarScan);
      foreach (var renderable in sortedRenderables)
      {
        renderable.Draw(gl);
      }
      shaderProgram.Unbind(gl);

      var now = DateTime.UtcNow;
      frameCount++;
      if ((now - startTime).TotalSeconds > 1)
      {
        // 1 second has passed. print out frames.
        //Debug.WriteLine(frameCount + " frames per second");
        startTime = now;
        frameCount = 0;
      }
    }
  }
}
