namespace InfoMap
{
  partial class Map
  {
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
      {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
      this.overlayControl = new SharpGL.OpenGLControl();
      this.panel1 = new System.Windows.Forms.Panel();
      ((System.ComponentModel.ISupportInitialize)(this.overlayControl)).BeginInit();
      this.SuspendLayout();
      // 
      // overlayControl
      // 
      this.overlayControl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.overlayControl.AutoSize = true;
      this.overlayControl.BackColor = System.Drawing.Color.Black;
      this.overlayControl.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
      this.overlayControl.DrawFPS = false;
      this.overlayControl.FrameRate = 60;
      this.overlayControl.Location = new System.Drawing.Point(248, -2);
      this.overlayControl.Name = "overlayControl";
      this.overlayControl.OpenGLVersion = SharpGL.Version.OpenGLVersion.OpenGL4_2;
      this.overlayControl.Padding = new System.Windows.Forms.Padding(100, 0, 0, 0);
      this.overlayControl.RenderContextType = SharpGL.RenderContextType.NativeWindow;
      this.overlayControl.RenderTrigger = SharpGL.RenderTrigger.TimerBased;
      this.overlayControl.Size = new System.Drawing.Size(759, 614);
      this.overlayControl.TabIndex = 0;
      // 
      // panel1
      // 
      this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
      this.panel1.AutoScroll = true;
      this.panel1.BackColor = System.Drawing.Color.Transparent;
      this.panel1.Location = new System.Drawing.Point(0, -2);
      this.panel1.Name = "panel1";
      this.panel1.Size = new System.Drawing.Size(248, 614);
      this.panel1.TabIndex = 1;
      // 
      // Map
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(1008, 606);
      this.Controls.Add(this.panel1);
      this.Controls.Add(this.overlayControl);
      this.Name = "Map";
      this.Text = "Map";
      this.Load += new System.EventHandler(this.MapLoad);
      ((System.ComponentModel.ISupportInitialize)(this.overlayControl)).EndInit();
      this.ResumeLayout(false);
      this.PerformLayout();

    }

    #endregion

    private SharpGL.OpenGLControl overlayControl;
    private System.Windows.Forms.Panel panel1;
  }
}

