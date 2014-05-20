using Gavaghan.Geodesy;
using InfoMap.Helpers;
using InfoMap.Models;
using Newtonsoft.Json;
using SharpGL;
using SharpGL.VertexBuffers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace InfoMap.DataSources
{
  public class RadarCell
  {
    public float reflectivity;
    public float azimuth;
    public float azimuth_after;
    public int azimuthNumber;
    public float dist;
    public float r;
    public float g;
    public float b;
  }
  public class Chunk
  {
    public int offset;
    public int length;
    public bool isCompressed;
    public MemoryStream uncompressed;
  }

  public class RadarScan : Renderable
  {
    public string station;
    public string stationName;
    public double latitude;
    public double longitude;
    public List<RadarCell> cells;
    public string siteName;

    public RadarScan(string filename, string category)
    {
      this.siteName = filename;
      this.description = this.siteName;
      this.category = category;
    }

    public override async Task<bool> Load()
    {
      var client1 = new HttpClient();
      var request1 = new HttpRequestMessage(HttpMethod.Get, "http://mesonet-nexrad.agron.iastate.edu/level2/raw/" + this.siteName + "/dir.list");
      string scanList = await (await client1.SendAsync(request1)).Content.ReadAsStringAsync();
      var scans = scanList.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

      var chosen = scans[scans.Length - 2].Split(' ')[1];

      var filelink = "http://mesonet-nexrad.agron.iastate.edu/level2/raw/" + this.siteName +"/" + chosen;

      var scaleColorsText = FileLoader.LoadTextFile("config/scale_BR.json");
      var scaleColors = JsonConvert.DeserializeObject<List<ColorScaleItem>>(scaleColorsText);

      cells = new List<RadarCell>();
      var uncompressedData = new MemoryStream();

      var start = DateTime.UtcNow;
      Debug.WriteLine("start");
      var client = new HttpClient();
      var request = new HttpRequestMessage(HttpMethod.Get, filelink);
      Stream contentStream = await (await client.SendAsync(request)).Content.ReadAsStreamAsync();
      var memoryReader = new MemoryStream((int)contentStream.Length);
      await contentStream.CopyToAsync(memoryReader);

      // Copy file into memory
      //var fileReader = File.OpenRead(this.filename);
      //var memoryReader = new MemoryStream((int)fileReader.Length);
      //fileReader.CopyTo(memoryReader);
      //fileReader.Close();
      memoryReader.Seek(0, SeekOrigin.Begin);


      var chunks = new List<Chunk>();
      var binaryReader = new BinaryReader(memoryReader);
      var bigEndianReader = new BigEndianReader(binaryReader);
      bigEndianReader.Skip(24);
      bool last = false;

      // Read each chunk, Get offset and size for each
      while (!last)
      {
        var chunkSize = bigEndianReader.ReadInt32();
        if (chunkSize < 0)
        {
          chunkSize *= -1;
          last = true;
        }
        if (chunkSize == 0)
        {
          continue;
        }
        var compressedSignifier = bigEndianReader.ReadChars(2);
        long pointer = bigEndianReader.BaseStream.Seek(-2, SeekOrigin.Current);
        bigEndianReader.Skip(chunkSize);
        chunks.Add(new Chunk
        {
          offset = (int)pointer,
          length = chunkSize,
          isCompressed = compressedSignifier == "BZ"
        });
      }

      bigEndianReader.Close();
      binaryReader.Close();
      var byteBuffer = memoryReader.GetBuffer();
      memoryReader.Close();

      var opts = new ParallelOptions
      {
        MaxDegreeOfParallelism = 6
      };

      // Decompress each chunk from file.
      Parallel.ForEach(chunks, opts, chunk =>
      {
        if (chunk.isCompressed)
        {
           chunk.uncompressed = new MemoryStream();
           var chunkStream = new MemoryStream(byteBuffer, chunk.offset, chunk.length);
           var chunkStart = DateTime.UtcNow; 
           ICSharpCode.SharpZipLib.BZip2.BZip2.Decompress(chunkStream, chunk.uncompressed, false);
           chunk.uncompressed.Seek(0, SeekOrigin.Begin);
        }
      });

      // Sum the sizes for each chunk
      var size = (int)chunks.Sum(x =>
      {
        if (x.isCompressed)
        {
          return x.uncompressed.Length;
        }
        else
        {
          return x.length;
        }
      });

      // 
      var uncompressedStream = new MemoryStream(size);
      foreach(var chunk in chunks){
        if (chunk.isCompressed)
        {
          chunk.uncompressed.CopyTo(uncompressedStream);
        }
        else
        {
          uncompressedStream.Write(byteBuffer, chunk.offset, chunk.length);
        }
      }


      Debug.WriteLine("took " + (DateTime.UtcNow - start).TotalMilliseconds + " ms file is reassembled");

      uncompressedStream.Seek(0, SeekOrigin.Begin);
      var uncompressedBinaryReader = new BinaryReader(uncompressedStream);
      var uncompressedReader = new BigEndianReader(uncompressedBinaryReader);
      var uncompressedLength = uncompressedStream.Length;

      var counts = new Dictionary<int, int>();
      var elevations = new Dictionary<int, int>();
      var azimuths = new Dictionary<int, float>();

      int record = 0;
      int message_offset31 = 0;

      int countRefs = 0;

      while (true)
      {
        long offset = record * 2432  + message_offset31;
        record++;

        if (offset >= uncompressedLength)
        {
          break;
        }
        uncompressedReader.BaseStream.Seek(offset, SeekOrigin.Begin);
        uncompressedReader.Skip(12);
        var messageSize = uncompressedReader.ReadUInt16();
        var idChannel = uncompressedReader.ReadBytes(1);
        var messageType = uncompressedReader.ReadBytes(1);

        uncompressedReader.Skip(8);
        var numSegments = uncompressedReader.ReadUInt16();
        var curSegment = uncompressedReader.ReadUInt16();
       
        if (!counts.ContainsKey(messageType[0]))
        {
          counts.Add(messageType[0], 1);
        }
        else
        {
          counts[messageType[0]]++;
        }
        
        if (messageType[0] == 31)
        {
          message_offset31 = message_offset31 + (messageSize * 2 + 12 - 2432);

          var identier = uncompressedReader.ReadChars(4);
          uncompressedReader.Skip(6);
          var azimuthNumber = uncompressedReader.ReadUInt16();
          var azimuthAngle = uncompressedReader.ReadFloat();
          var compressedFlag = uncompressedReader.ReadBytes(1)[0];
          var sp = uncompressedReader.ReadBytes(1)[0];
          var rlength = uncompressedReader.ReadInt16();
          var ars = uncompressedReader.ReadBytes(1)[0];

          if (!azimuths.ContainsKey(azimuthNumber))
          {
            azimuths.Add(azimuthNumber, azimuthAngle);
          }

          if (ars != 1)
          {
            //not HI res.
            continue;
          }

          var rs = uncompressedReader.ReadBytes(1)[0];
          var elevation_num = uncompressedReader.ReadBytes(1)[0]; // RDA elevation number
          var cut = uncompressedReader.ReadBytes(1); // sector number within cut
          var elevation = uncompressedReader.ReadFloat();

          if (elevation_num != 1)
          {
            continue;
          }

          if (!elevations.ContainsKey(elevation_num))
          {
            elevations.Add(elevation_num, 1);
          }
          else
          {
            elevations[elevation_num]++;
          }

          var rsbs = uncompressedReader.ReadBytes(1)[0];
          var aim = uncompressedReader.ReadBytes(1)[0];
          var dcount = uncompressedReader.ReadInt16();

          uint[] data_pointers = new uint[9];
          for (int i = 0; i < 9; i++)
          {
            data_pointers[i] = uncompressedReader.ReadUInt32();
          }
          for (int i = 0; i < 9; i++)
          {
            uncompressedReader.BaseStream.Seek(offset + 28+ data_pointers[i], SeekOrigin.Begin);
            uncompressedReader.Skip(1);
            var dataBlockName = uncompressedReader.ReadChars(3);

            if (dataBlockName == "REF")
            {
              uncompressedReader.Skip(4);
             
              var numGates = uncompressedReader.ReadUInt16();
              var firstGateDistance = uncompressedReader.ReadUInt16();
              var gateDistanceInterval = uncompressedReader.ReadUInt16();
              uncompressedReader.Skip(6);
              var dataMomentScale = uncompressedReader.ReadFloat();
              var dataMomentOffset = uncompressedReader.ReadFloat();

              for (int j = 0; j < numGates; j++)
              {
                  var gateBytes = uncompressedReader.ReadBytes(1)[0];

                  var dist = firstGateDistance + gateDistanceInterval * j;

                  var reflectivity = gateBytes * 0.5f - 33;

                  if (reflectivity < 1)
                  {
                    continue;
                  }
                  var cell = new RadarCell
                  {
                    reflectivity = reflectivity,
                    azimuth = azimuthAngle,
                    dist = dist,
                    azimuthNumber = azimuthNumber
                  };
                  this.cells.Add(cell);
              }
              countRefs++;
            } else if (this.latitude == 0 && dataBlockName == "VOL")
            {
              Debug.WriteLine("Found VOL");
              uncompressedReader.Skip(4);
              this.latitude = uncompressedReader.ReadFloat();
              this.longitude =  uncompressedReader.ReadFloat();
            }
          }
        } 
      }
      Debug.WriteLine("took " + (DateTime.UtcNow - start).TotalMilliseconds + " ms radar ready to render");

      this.vertexArray = new float[this.cells.Count * 3 * 6];
      this.colorArray = new float[this.cells.Count * 4 * 6];
      var earth = Ellipsoid.WGS84;
      var stationGeo = new GlobalCoordinates(new Angle(this.latitude), new Angle(this.longitude));
      var calc = new GeodeticCalculator();

      Parallel.For(0, this.cells.Count, opts, i =>
      {
        var cell = this.cells[i];

        if (cell.azimuthNumber == azimuths.Count())
        {
          cell.azimuth_after = azimuths[1];
        }
        else
        {
          cell.azimuth_after = azimuths[cell.azimuthNumber+1];
        }

        int bucket = -1;
        for(int j = 0; j < scaleColors.Count; j++){
           if( cell.reflectivity > scaleColors[j].dbZ )
           {
             bucket = j;
             break;
           }
        }

        switch (bucket)
        {
          case 0:
          case -1:
            //off scale.
            break;
          case 1:
          default:

            int[] lowColor = null;
            int[] highColor = null;

            if (bucket == 1)
            {
              lowColor = new int[] { scaleColors[0].range[0], scaleColors[0].range[1], scaleColors[0].range[2] };
              highColor = new int[] { scaleColors[1].range[0], scaleColors[1].range[1], scaleColors[1].range[2] };
            }
            else
            {
              lowColor = new int[] { scaleColors[bucket].range[3], scaleColors[bucket].range[4], scaleColors[bucket].range[5] };
              highColor = new int[] { scaleColors[bucket].range[0], scaleColors[bucket].range[1], scaleColors[bucket].range[2] };
            }
            var range = scaleColors[bucket - 1].dbZ - scaleColors[bucket].dbZ;
            var percentForLowColor = (cell.reflectivity - scaleColors[bucket].dbZ) / range;
            var percentForHighColor = 1 - percentForLowColor;

            cell.r = (percentForHighColor * highColor[0] + percentForLowColor * lowColor[0]) / 255.0f;
            cell.g = (percentForHighColor * highColor[1] + percentForLowColor * lowColor[1]) / 255.0f;
            cell.b = (percentForHighColor * highColor[2] + percentForLowColor * lowColor[2]) / 255.0f;
            break;
        }

        var r = cell.dist;
        int baseVertex = 3 * 6 * i;

        var top_left = calc.CalculateEndingGlobalCoordinates(earth, stationGeo, new Angle(cell.azimuth_after), r+250.0f);
        var top_right = calc.CalculateEndingGlobalCoordinates(earth, stationGeo, new Angle(cell.azimuth), r+250.0f);
        var bottom_right = calc.CalculateEndingGlobalCoordinates(earth, stationGeo, new Angle(cell.azimuth), r);
        var bottom_left = calc.CalculateEndingGlobalCoordinates(earth, stationGeo, new Angle(cell.azimuth_after), r);

        this.vertexArray[baseVertex + 0] = ConvertLongitude(top_left.Longitude.Degrees);//top left x
        this.vertexArray[baseVertex + 1] = ConvertLatitude(top_left.Latitude.Degrees);///top left y
        this.vertexArray[baseVertex + 3] = ConvertLongitude(top_right.Longitude.Degrees); //top right x
        this.vertexArray[baseVertex + 4] = ConvertLatitude(top_right.Latitude.Degrees); //top right y
        this.vertexArray[baseVertex + 6] = ConvertLongitude(bottom_right.Longitude.Degrees); //bottom right x
        this.vertexArray[baseVertex + 7] = ConvertLatitude(bottom_right.Latitude.Degrees); //bottom right y
        this.vertexArray[baseVertex + 9] = ConvertLongitude(bottom_right.Longitude.Degrees);//bottom right x
        this.vertexArray[baseVertex + 10] =ConvertLatitude(bottom_right.Latitude.Degrees); //bottom right y
        this.vertexArray[baseVertex + 12] = ConvertLongitude(bottom_left.Longitude.Degrees); //bottom left x
        this.vertexArray[baseVertex + 13] = ConvertLatitude(bottom_left.Latitude.Degrees);//bottom left y
        this.vertexArray[baseVertex + 15] = ConvertLongitude(top_left.Longitude.Degrees); //top left x
        this.vertexArray[baseVertex + 16] = ConvertLatitude(top_left.Latitude.Degrees); //top left y

        int baseColor = 4 * 6 * i;
        const float alpha = 0.6f;
        this.colorArray[baseColor + 0] = cell.r;
        this.colorArray[baseColor + 1] = cell.g;
        this.colorArray[baseColor + 2] = cell.b;
        this.colorArray[baseColor + 3] = alpha;
        this.colorArray[baseColor + 4] = cell.r;
        this.colorArray[baseColor + 5] = cell.g;
        this.colorArray[baseColor + 6] = cell.b;
        this.colorArray[baseColor + 7] = alpha;
        this.colorArray[baseColor + 8] = cell.r;
        this.colorArray[baseColor + 9] = cell.g;
        this.colorArray[baseColor + 10] = cell.b;
        this.colorArray[baseColor + 11] = alpha;
        this.colorArray[baseColor + 12] = cell.r;
        this.colorArray[baseColor + 13] = cell.g;
        this.colorArray[baseColor + 14] = cell.b;
        this.colorArray[baseColor + 15] = alpha;
        this.colorArray[baseColor + 16] = cell.r;
        this.colorArray[baseColor + 17] = cell.g;
        this.colorArray[baseColor + 18] = cell.b;
        this.colorArray[baseColor + 19] = alpha;
        this.colorArray[baseColor + 20] = cell.r;
        this.colorArray[baseColor + 21] = cell.g;
        this.colorArray[baseColor + 22] = cell.b;
        this.colorArray[baseColor + 23] = alpha;

      });

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
      var mercN = Math.Log(Math.Tan((Math.PI / 4) + (latRad / 2)));
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
      gl.DrawArrays(OpenGL.GL_TRIANGLES, 0, this.cells.Count * 6); //cells.Count * 4);
      this.vertexBufferArray.Unbind(gl);
    }

  }
}
