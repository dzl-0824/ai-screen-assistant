using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WF=System.Windows.Forms;
using DPoint=System.Drawing.PointF;
using DColor=System.Drawing.Color;
using WPoint=System.Windows.Point;
using WImage=System.Windows.Controls.Image;
using WButton=System.Windows.Controls.Button;
using WTextBox=System.Windows.Controls.TextBox;
static class ShotStore {
 public const string DefaultDirectory=@"D:\ScreenText\Screenshots";
 public static string DirectoryPath(string folder){if(string.IsNullOrWhiteSpace(folder)||!Path.IsPathRooted(folder))throw new Exception("请选择完整的截图保存目录");return Path.GetFullPath(folder.Trim());}
 public static string SaveNew(Bitmap image,string folder){folder=DirectoryPath(folder);Directory.CreateDirectory(folder);string path=Path.Combine(folder,"screenshot-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,8)+".png");Write(image,path);return path;}
 public static void Write(Bitmap image,string path){string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{image.Save(temp,ImageFormat.Png);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
 public static List<string> History(string folder){folder=DirectoryPath(folder);if(!Directory.Exists(folder))return new List<string>();return Directory.GetFiles(folder,"screenshot-*.png").OrderBy(x=>Path.GetFileName(x),StringComparer.Ordinal).ToList();}
 public static Bitmap Read(string path){using(var stream=File.OpenRead(path))using(var image=new Bitmap(stream))return new Bitmap(image);}
 [System.Runtime.InteropServices.DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr handle);
 public static BitmapSource Source(Bitmap image){IntPtr handle=image.GetHbitmap();try{var source=System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(handle,IntPtr.Zero,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());source.Freeze();return source;}finally{DeleteObject(handle);}}

}
class ShotMark {
 public string Tool,Text="";public DPoint A,B;public List<DPoint> Points=new List<DPoint>();public DColor Color=DColor.FromArgb(230,52,65);public float Width=3,TextSize;
 public static RectangleF Box(DPoint a,DPoint b){return RectangleF.FromLTRB(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Max(a.X,b.X),Math.Max(a.Y,b.Y));}
 public void Draw(Graphics g){var box=Box(A,B);using(var pen=new System.Drawing.Pen(Color,Width)){pen.StartCap=pen.EndCap=LineCap.Round;switch(Tool){case "rect":if(box.Width>0&&box.Height>0)g.DrawRectangle(pen,box.X,box.Y,box.Width,box.Height);break;case "ellipse":if(box.Width>0&&box.Height>0)g.DrawEllipse(pen,box);break;case "arrow":using(var arrow=new AdjustableArrowCap(4,5,true)){pen.CustomEndCap=arrow;g.DrawLine(pen,A,B);}break;case "pen":if(Points.Count>1)g.DrawLines(pen,Points.ToArray());break;case "text":using(var font=new Font("Microsoft YaHei UI",TextSize>0?TextSize:Math.Max(16,Width*6),System.Drawing.FontStyle.Bold,GraphicsUnit.Pixel))using(var brush=new SolidBrush(Color))g.DrawString(Text,font,brush,A);break;case "mask":g.FillRectangle(System.Drawing.Brushes.Black,box);break;}}}
}
class ShotDocument:IDisposable {
 public Bitmap Original;public List<ShotMark> Marks=new List<ShotMark>();Stack<ShotMark> undone=new Stack<ShotMark>();public bool Dirty;
 public ShotDocument(Bitmap image){Original=new Bitmap(image);}
 public void Add(ShotMark mark){Marks.Add(mark);undone.Clear();Dirty=true;}
 public void Undo(){if(Marks.Count==0)return;var last=Marks[Marks.Count-1];Marks.RemoveAt(Marks.Count-1);undone.Push(last);Dirty=true;}
 public void Redo(){if(undone.Count==0)return;Marks.Add(undone.Pop());Dirty=true;}
 public Bitmap Render(ShotMark preview=null){var result=new Bitmap(Original);using(var g=Graphics.FromImage(result)){g.SmoothingMode=SmoothingMode.AntiAlias;foreach(var mark in Marks)mark.Draw(g);if(preview!=null)preview.Draw(g);}return result;}
 public void Dispose(){Original.Dispose();}
}
static class ShotUi {
 public static WButton Button(string text,Action action){var b=new WButton{Content=text};b.Click+=(s,e)=>action();return b;}
 public static string AskText(WF.Form owner){string value=null;using(var dialog=new WF.Form{Text="添加文字",FormBorderStyle=WF.FormBorderStyle.FixedDialog,StartPosition=WF.FormStartPosition.CenterParent,ClientSize=new System.Drawing.Size(360,125),MinimizeBox=false,MaximizeBox=false,Font=new Font("Microsoft YaHei UI",10)}){var input=new WF.TextBox{Left=16,Top=16,Width=328};var okay=new WF.Button{Text="添加",Left=244,Top=70,Width=100,DialogResult=WF.DialogResult.OK};dialog.Controls.Add(input);dialog.Controls.Add(okay);dialog.AcceptButton=okay;if(dialog.ShowDialog(owner)==WF.DialogResult.OK)value=input.Text;}return value;}
 public static void Error(Exception e){CopyToast.ShowMessage("操作未完成");}
}
class ShotEditor:WF.Form {
 public System.Windows.Controls.UserControl Root;ShotDocument document;string folder,path;List<string> history;WImage image;TextBlock note,counter;WrapPanel tools;ScrollViewer scroll;StackPanel surface;ShotMark drawing;string tool="move";DColor ink=DColor.FromArgb(230,52,65);float stroke=3;double zoom=1;Dictionary<string,WButton> toolButtons=new Dictionary<string,WButton>();public Action<WF.Form> Track;
 public ShotEditor(string folder,string path){this.folder=folder;this.path=path;Root=ModernChrome.Load("ShotEditor");Text="截图与标注";Icon=Theme.Icon();FormBorderStyle=WF.FormBorderStyle.None;ClientSize=new System.Drawing.Size(1000,680);MinimumSize=new System.Drawing.Size(800,520);StartPosition=WF.FormStartPosition.CenterScreen;BackColor=DColor.White;AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=WF.AutoScaleMode.Dpi;ModernChrome.WireFrame(Root,this);Controls.Add(ModernChrome.Host(Root));image=(WImage)Root.FindName("ShotImage");note=(TextBlock)Root.FindName("Note");counter=(TextBlock)Root.FindName("Counter");tools=(WrapPanel)Root.FindName("Tools");surface=(StackPanel)Root.FindName("Surface");scroll=(ScrollViewer)Root.FindName("ImageScroll");AddTool("move","浏览");AddTool("rect","矩形");AddTool("ellipse","圆形");AddTool("arrow","箭头");AddTool("pen","画笔");AddTool("text","文字");AddTool("mask","打码");tools.Children.Add(ShotUi.Button("撤销",()=>{document.Undo();Redraw();}));tools.Children.Add(ShotUi.Button("重做",()=>{document.Redo();Redraw();}));tools.Children.Add(ShotUi.Button("颜色",()=>{using(var picker=new WF.ColorDialog{Color=ink})if(picker.ShowDialog(this)==WF.DialogResult.OK)ink=picker.Color;}));var width=new ComboBox{Width=64,Margin=new Thickness(6,0,6,0),VerticalAlignment=VerticalAlignment.Center};foreach(int n in new[]{2,3,5,8})width.Items.Add(n);width.SelectedItem=3;width.SelectionChanged+=(s,e)=>stroke=Convert.ToSingle(width.SelectedItem);tools.Children.Add(width);Bind("Previous",()=>Navigate(-1));Bind("Next",()=>Navigate(1));Bind("CopyShot",()=>Copy(false));Bind("PinShot",Pin);Bind("SaveShot",()=>{if(SaveCurrent())Note("已保存到本地");});Bind("Fit",Fit);Bind("ZoomIn",()=>Zoom(1.2));Bind("ZoomOut",()=>Zoom(1/1.2));Bind("Folder",()=>{try{System.Diagnostics.Process.Start(folder);}catch(Exception e){Note(e.Message);}});image.MouseLeftButtonDown+=Down;image.MouseMove+=OnImageMove;image.MouseLeftButtonUp+=Up;image.LostMouseCapture+=(s,e)=>{if(drawing!=null){drawing=null;Redraw();}};Root.PreviewKeyDown+=Key;image.PreviewMouseWheel+=(s,e)=>{if((Keyboard.Modifiers&System.Windows.Input.ModifierKeys.Control)!=0){Zoom(e.Delta>0?1.15:1/1.15);e.Handled=true;}};Root.Loaded+=(s,e)=>Root.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,new Action(Fit));Shown+=(s,e)=>{ModernChrome.Round(this);BeginInvoke(new Action(Fit));};FormClosing+=(s,e)=>{if(!SaveCurrent())e.Cancel=true;};FormClosed+=(s,e)=>{if(document!=null)document.Dispose();};LoadPath(path);SetTool("move");}
 void Bind(string name,Action action){((WButton)Root.FindName(name)).Click+=(s,e)=>action();}
 void AddTool(string id,string title){var button=ShotUi.Button(title,()=>SetTool(id));toolButtons[id]=button;tools.Children.Add(button);}
 void SetTool(string value){tool=value;image.Cursor=value=="move"?Cursors.Arrow:Cursors.Cross;foreach(var pair in toolButtons){pair.Value.Background=new SolidColorBrush(pair.Key==value?System.Windows.Media.Color.FromRgb(232,237,249):System.Windows.Media.Colors.White);pair.Value.Foreground=new SolidColorBrush(pair.Key==value?System.Windows.Media.Color.FromRgb(0,47,167):System.Windows.Media.Color.FromRgb(25,33,43));}Note(value=="mask"?"拖动添加不透明黑色遮挡；复制和保存时合并到图片":"拖动标注 · Ctrl+Z 撤销 · Ctrl+C 复制 · Ctrl+P 贴图 · ← → 历史");}
 DPoint Position(MouseEventArgs e){var p=e.GetPosition(image);return new DPoint((float)Math.Max(0,Math.Min(document.Original.Width-1,p.X)),(float)Math.Max(0,Math.Min(document.Original.Height-1,p.Y)));}
 void Down(object sender,MouseButtonEventArgs e){if(tool=="move"){if(e.ClickCount==2)Copy(true);return;}var point=Position(e);if(tool=="text"){string text=ShotUi.AskText(this);if(!string.IsNullOrWhiteSpace(text)){document.Add(new ShotMark{Tool=tool,A=point,B=point,Text=text,Color=ink,Width=stroke});Redraw();}return;}drawing=new ShotMark{Tool=tool,A=point,B=point,Color=ink,Width=stroke};drawing.Points.Add(point);image.CaptureMouse();e.Handled=true;}
 void OnImageMove(object sender,MouseEventArgs e){if(drawing==null||e.LeftButton!=MouseButtonState.Pressed)return;drawing.B=Position(e);if(drawing.Tool=="pen")drawing.Points.Add(drawing.B);Redraw();}
 void Up(object sender,MouseButtonEventArgs e){if(drawing==null)return;drawing.B=Position(e);if(drawing.Tool=="pen")drawing.Points.Add(drawing.B);var mark=drawing;drawing=null;image.ReleaseMouseCapture();if(ShotMark.Box(mark.A,mark.B).Width>1||ShotMark.Box(mark.A,mark.B).Height>1||mark.Tool=="pen")document.Add(mark);Redraw();e.Handled=true;}
 void Key(object sender,KeyEventArgs e){if((Keyboard.Modifiers&System.Windows.Input.ModifierKeys.Control)!=0){if(e.Key==System.Windows.Input.Key.C){Copy(false);e.Handled=true;}else if(e.Key==System.Windows.Input.Key.S){SaveCurrent();e.Handled=true;}else if(e.Key==System.Windows.Input.Key.Z){document.Undo();Redraw();e.Handled=true;}else if(e.Key==System.Windows.Input.Key.Y){document.Redo();Redraw();e.Handled=true;}else if(e.Key==System.Windows.Input.Key.P){Pin();e.Handled=true;}return;}if(e.Key==System.Windows.Input.Key.Enter){Copy(true);e.Handled=true;}else if(e.Key==System.Windows.Input.Key.Left){Navigate(-1);e.Handled=true;}else if(e.Key==System.Windows.Input.Key.Right){Navigate(1);e.Handled=true;}else if(e.Key==System.Windows.Input.Key.Escape){Close();e.Handled=true;}}
 public bool SaveCurrent(){if(document==null)return true;try{if(document.Dirty){using(var result=document.Render())ShotStore.Write(result,path);document.Dirty=false;}return true;}catch(Exception e){Note("保存失败："+e.Message);return false;}}
 void LoadPath(string value){using(var loaded=ShotStore.Read(value)){var next=new ShotDocument(loaded);if(document!=null)document.Dispose();document=next;}path=value;history=ShotStore.History(folder);Redraw();counter.Text=(history.IndexOf(path)+1)+" / "+history.Count+" · "+document.Original.Width+" × "+document.Original.Height;((TextBlock)Root.FindName("FileName")).Text=Path.GetFileName(path);}
 public void Navigate(int direction){if(!SaveCurrent())return;try{history=ShotStore.History(folder);int current=history.IndexOf(path),next=current+direction;if(next<0||next>=history.Count){Note(direction<0?"已经是第一张":"已经是最新一张");return;}LoadPath(history[next]);Fit();Note("已切换历史截图");}catch(Exception e){Note("读取截图失败："+e.Message);}}
 public void Redraw(){using(var bitmap=document.Render(drawing))image.Source=ShotStore.Source(bitmap);image.Width=document.Original.Width;image.Height=document.Original.Height;surface.LayoutTransform=new ScaleTransform(zoom,zoom);}
 void Fit(){if(document==null)return;zoom=Math.Min(1,Math.Min(Math.Max(100,scroll.ActualWidth-40)/document.Original.Width,Math.Max(100,scroll.ActualHeight-40)/document.Original.Height));Redraw();}
 void Zoom(double ratio){zoom=Math.Max(.05,Math.Min(8,zoom*ratio));Redraw();Note("缩放 "+(zoom*100).ToString("0")+"%");}
 void Copy(bool close){if(!SaveCurrent())return;try{using(var bitmap=document.Render())WF.Clipboard.SetImage(bitmap);CopyToast.ShowCopied();Note("已复制 · 截图已自动保存");if(close)Close();}catch(Exception e){Note("复制失败："+e.Message);}}
 void Pin(){if(!SaveCurrent())return;try{var pin=new ShotPin(folder,path);pin.Track=Track;if(Track!=null)Track(pin);pin.Show();Close();}catch(Exception e){Note("贴图失败："+e.Message);}}
 void Note(string value){note.Text=value;}
}
class ShotPin:WF.Form,WF.IMessageFilter {
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr GetAncestor(IntPtr handle,uint flags);
 bool escapeFilter;
 public System.Windows.Controls.UserControl Root;WImage image;string folder,path;double zoom=1;Bitmap bitmap;public Action<WF.Form> Track;
 public ShotPin(string folder,string path){this.folder=folder;this.path=path;Root=ModernChrome.Load("ShotPin");Text="悬浮贴图";Icon=Theme.Icon();FormBorderStyle=WF.FormBorderStyle.None;TopMost=true;ShowInTaskbar=false;BackColor=DColor.White;StartPosition=WF.FormStartPosition.CenterScreen;AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=WF.AutoScaleMode.Dpi;Controls.Add(ModernChrome.Host(Root));image=(WImage)Root.FindName("PinImage");var menu=new System.Windows.Controls.ContextMenu();var copyItem=new System.Windows.Controls.MenuItem{Header="复制图片",InputGestureText="Ctrl+C"};copyItem.Click+=(s,e)=>Copy();menu.Items.Add(copyItem);var editItem=new System.Windows.Controls.MenuItem{Header="继续标注"};editItem.Click+=(s,e)=>Edit();menu.Items.Add(editItem);menu.Items.Add(new System.Windows.Controls.Separator());var closeItem=new System.Windows.Controls.MenuItem{Header="关闭贴图",InputGestureText="Esc"};closeItem.Click+=(s,e)=>Close();menu.Items.Add(closeItem);Root.ContextMenu=menu;image.MouseLeftButtonDown+=(s,e)=>{if(e.ClickCount==2){Edit();return;}ModernChrome.Drag(this);};Root.PreviewMouseWheel+=(s,e)=>{if((Keyboard.Modifiers&System.Windows.Input.ModifierKeys.Control)!=0)Opacity=Math.Max(.2,Math.Min(1,Opacity+(e.Delta>0?.05:-.05)));else{zoom=Math.Max(.1,Math.Min(5,zoom*(e.Delta>0?1.1:1/1.1)));ResizeImage();}e.Handled=true;};Root.PreviewKeyDown+=(s,e)=>{if(e.Key==System.Windows.Input.Key.Escape){Close();e.Handled=true;}else if(e.Key==System.Windows.Input.Key.Enter){Copy();e.Handled=true;}else if(e.Key==System.Windows.Input.Key.Left){Navigate(-1);e.Handled=true;}else if(e.Key==System.Windows.Input.Key.Right){Navigate(1);e.Handled=true;}else if(e.Key==System.Windows.Input.Key.C&&(Keyboard.Modifiers&System.Windows.Input.ModifierKeys.Control)!=0){Copy();e.Handled=true;}};Shown+=(s,e)=>{if(!escapeFilter){WF.Application.AddMessageFilter(this);escapeFilter=true;}Root.Focus();};Activated+=(s,e)=>{if(!IsDisposed)BeginInvoke(new Action(()=>{if(!IsDisposed)Root.Focus();}));};FormClosed+=(s,e)=>{if(bitmap!=null)bitmap.Dispose();};LoadPath(path);zoom=Math.Min(1,Math.Min(640.0/bitmap.Width,480.0/bitmap.Height));ResizeImage();}
 public bool PreFilterMessage(ref WF.Message message){if(IsDisposed||(message.Msg!=0x100&&message.Msg!=0x104)||message.WParam.ToInt32()!=(int)WF.Keys.Escape)return false;IntPtr active=GetForegroundWindow();if(active!=Handle&&GetAncestor(active,2)!=Handle&&(Root.ContextMenu==null||!Root.ContextMenu.IsOpen))return false;if(Root.ContextMenu!=null)Root.ContextMenu.IsOpen=false;Close();return true;}
 protected override bool ProcessCmdKey(ref WF.Message message,WF.Keys key){if(key==WF.Keys.Escape){if(Root.ContextMenu!=null)Root.ContextMenu.IsOpen=false;Close();return true;}return base.ProcessCmdKey(ref message,key);}
 protected override void Dispose(bool disposing){if(escapeFilter){WF.Application.RemoveMessageFilter(this);escapeFilter=false;}base.Dispose(disposing);}
 void LoadPath(string value){var next=ShotStore.Read(value);if(bitmap!=null)bitmap.Dispose();bitmap=next;path=value;image.Source=ShotStore.Source(bitmap);ResizeImage();}
 void ResizeImage(){double scale;using(var g=CreateGraphics())scale=g.DpiX/96;var area=WF.Screen.FromControl(this).WorkingArea;double ratio=Math.Min(zoom*scale,Math.Min((double)area.Width/bitmap.Width,(double)area.Height/bitmap.Height));ClientSize=new System.Drawing.Size(Math.Max(1,(int)Math.Round(bitmap.Width*ratio)),Math.Max(1,(int)Math.Round(bitmap.Height*ratio)));}

 void Navigate(int direction){try{var all=ShotStore.History(folder);int next=all.IndexOf(path)+direction;if(next>=0&&next<all.Count)LoadPath(all[next]);}catch{CopyToast.ShowMessage("读取截图失败");}}
 void Copy(){try{WF.Clipboard.SetImage(bitmap);CopyToast.ShowCopied();}catch{CopyToast.ShowMessage("复制失败");}}
 void Edit(){try{var editor=new ShotEditor(folder,path);editor.Track=Track;if(Track!=null)Track(editor);editor.Show();Close();}catch{CopyToast.ShowMessage("读取截图失败");}}
}
