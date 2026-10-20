using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
class CopyToast:Form {
 [StructLayout(LayoutKind.Sequential)]struct NativePoint{public int X,Y;public NativePoint(int x,int y){X=x;Y=y;}}
 [StructLayout(LayoutKind.Sequential)]struct NativeSize{public int Width,Height;public NativeSize(int w,int h){Width=w;Height=h;}}
 [StructLayout(LayoutKind.Sequential,Pack=1)]struct Blend{public byte Op,Flags,Alpha,Format;}
 [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr window);
 [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr window,IntPtr dc);
 [DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleDC(IntPtr dc);
 [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
 [DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr item);
 [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr item);
 [DllImport("user32.dll",SetLastError=true)]static extern bool UpdateLayeredWindow(IntPtr window,IntPtr destDc,ref NativePoint dest,ref NativeSize size,IntPtr sourceDc,ref NativePoint source,uint color,ref Blend blend,uint flags);
 public bool LastPresentSucceeded{get;private set;}static CopyToast current;string message="已复制";Timer life;float scale;IntPtr pixels;System.Diagnostics.Stopwatch elapsed=new System.Diagnostics.Stopwatch();
 public static void ShowCopied(){ShowMessage("已复制");}
 public static void ShowMessage(string text){if(current!=null&&!current.IsDisposed)current.Close();current=new CopyToast(text);current.Show();}
 public CopyToast():this("已复制"){}
 CopyToast(string text){message=(text??"").Replace('\r',' ').Replace('\n',' ');FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;AutoScaleMode=AutoScaleMode.None;using(var g=CreateGraphics())scale=g.DpiX/96f;using(var font=new Font("Microsoft YaHei UI",14*scale,FontStyle.Regular,GraphicsUnit.Pixel))using(var g=CreateGraphics()){float width=g.MeasureString(message,font).Width;ClientSize=new Size((int)(Math.Max(132,Math.Min(340,width/scale+78))*scale),(int)(62*scale));}var area=Screen.FromPoint(Cursor.Position).WorkingArea;Location=new Point(area.Left+(area.Width-Width)/2,area.Bottom-Height-(int)(24*scale));using(var image=RenderCard())pixels=image.GetHbitmap(Color.FromArgb(0));life=new Timer{Interval=16};life.Tick+=(s,e)=>{double ms=elapsed.Elapsed.TotalMilliseconds;if(ms>=1650){Close();return;}double fade=ms<140?ms/140:ms>1430?(1650-ms)/220:1;Present((byte)Math.Max(0,Math.Min(255,255*fade)));};Shown+=(s,e)=>{elapsed.Restart();Present(0);life.Start();};}
 protected override bool ShowWithoutActivation{get{return true;}}
 protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x80000|0x08000000|0x80|0x20;return p;}}
 static GraphicsPath Pill(RectangleF r){var path=new GraphicsPath();float d=r.Height;path.AddArc(r.Left,r.Top,d,d,90,180);path.AddArc(r.Right-d,r.Top,d,d,270,180);path.CloseFigure();return path;}
 public Bitmap RenderCard(){var bitmap=new Bitmap(Width,Height,PixelFormat.Format32bppPArgb);using(var g=Graphics.FromImage(bitmap)){g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;var body=new RectangleF(9*scale,7*scale,Width-18*scale,42*scale);for(int n=7;n>=1;n--){var shadow=body;shadow.Offset(0,3*scale);shadow.Inflate(n*.65f*scale,n*.55f*scale);using(var path=Pill(shadow))using(var brush=new SolidBrush(Color.FromArgb(3+(7-n)/2,25,40,65)))g.FillPath(brush,path);}using(var path=Pill(body)){using(var fill=new SolidBrush(Color.FromArgb(253,254,255)))g.FillPath(fill,path);using(var line=new Pen(Color.FromArgb(229,234,241),scale*.8f))g.DrawPath(line,path);}bool error=message.Contains("失败")||message.Contains("未完成");var icon=new RectangleF(body.Left+13*scale,body.Top+11*scale,20*scale,20*scale);using(var fill=new SolidBrush(error?Color.FromArgb(255,242,228):Color.FromArgb(232,239,255)))g.FillEllipse(fill,icon);using(var pen=new Pen(error?Color.FromArgb(190,112,29):Color.FromArgb(0,47,167),1.6f*scale)){pen.StartCap=pen.EndCap=LineCap.Round;if(error){g.DrawLine(pen,icon.Left+10*scale,icon.Top+5*scale,icon.Left+10*scale,icon.Top+11*scale);g.DrawLine(pen,icon.Left+10*scale,icon.Top+14.5f*scale,icon.Left+10*scale,icon.Top+15*scale);}else g.DrawLines(pen,new[]{new PointF(icon.Left+5*scale,icon.Top+10*scale),new PointF(icon.Left+8.5f*scale,icon.Top+13.5f*scale),new PointF(icon.Left+15*scale,icon.Top+7*scale)});}using(var font=new Font("Microsoft YaHei UI",14*scale,FontStyle.Regular,GraphicsUnit.Pixel))using(var ink=new SolidBrush(Color.FromArgb(35,46,62)))using(var format=new StringFormat{Alignment=StringAlignment.Near,LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap})g.DrawString(message,font,ink,new RectangleF(icon.Right+9*scale,body.Top,body.Right-icon.Right-22*scale,body.Height),format);}return bitmap;}
 void Present(byte alpha){if(IsDisposed||pixels==IntPtr.Zero)return;IntPtr desktop=GetDC(IntPtr.Zero),source=CreateCompatibleDC(desktop),previous=SelectObject(source,pixels);try{var dest=new NativePoint(Left,Top);var origin=new NativePoint(0,0);var size=new NativeSize(Width,Height);var blend=new Blend{Op=0,Flags=0,Alpha=alpha,Format=1};LastPresentSucceeded=UpdateLayeredWindow(Handle,desktop,ref dest,ref size,source,ref origin,0,ref blend,2);}finally{SelectObject(source,previous);DeleteDC(source);ReleaseDC(IntPtr.Zero,desktop);}}
 protected override void Dispose(bool disposing){if(disposing&&life!=null){life.Dispose();life=null;}if(pixels!=IntPtr.Zero){DeleteObject(pixels);pixels=IntPtr.Zero;}base.Dispose(disposing);}
}
