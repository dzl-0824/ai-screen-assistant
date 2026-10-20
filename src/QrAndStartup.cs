using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Win32;
static class StartupRegistration {
 public static string ValueName="ScreenText";
 const string Path="Software\\Microsoft\\Windows\\CurrentVersion\\Run";
 public static bool IsEnabled(){using(var key=Registry.CurrentUser.OpenSubKey(Path)){return key!=null&&string.Equals(Convert.ToString(key.GetValue(ValueName)),Command(),StringComparison.OrdinalIgnoreCase);}}
 static string Command(){return "\""+System.Windows.Forms.Application.ExecutablePath+"\"";}
 public static void SetEnabled(bool enabled){using(var key=Registry.CurrentUser.CreateSubKey(Path)){if(enabled)key.SetValue(ValueName,Command(),RegistryValueKind.String);else key.DeleteValue(ValueName,false);}}
}
static class EmbeddedDependencies {
 static int installed;
 public static void Install(){if(Interlocked.Exchange(ref installed,1)!=0)return;AppDomain.CurrentDomain.AssemblyResolve+=(sender,args)=>{string name=new AssemblyName(args.Name).Name;string resource=name=="zxing"?"QrLibrary":name=="NAudio"?"RecordingAudioLibrary":null;if(resource==null)return null;using(var stream=typeof(EmbeddedDependencies).Assembly.GetManifestResourceStream(resource)){if(stream==null)return null;using(var memory=new MemoryStream()){stream.CopyTo(memory);return Assembly.Load(memory.ToArray());}}};}
}
static class QrDecoder {
 public static string DecodeImage(string image){EmbeddedDependencies.Install();int comma=image.IndexOf(',');string data=image.StartsWith("data:",StringComparison.Ordinal)?image.Substring(comma+1):image;using(var stream=new MemoryStream(Convert.FromBase64String(data)))using(var bitmap=new Bitmap(stream))return Decode(bitmap);}
 public static string Decode(Bitmap image){EmbeddedDependencies.Install();return DecodeCore(image);}
 [MethodImpl(MethodImplOptions.NoInlining)]static string DecodeCore(Bitmap image){var reader=new ZXing.BarcodeReader{AutoRotate=true};reader.Options.TryHarder=true;reader.Options.TryInverted=true;reader.Options.PossibleFormats=new[]{ZXing.BarcodeFormat.QR_CODE};var results=reader.DecodeMultiple(image);if(results==null||results.Length==0){var single=reader.Decode(image);if(single!=null)results=new[]{single};}var values=new List<string>();var seen=new HashSet<string>(StringComparer.Ordinal);if(results!=null)foreach(var result in results)if(!string.IsNullOrEmpty(result.Text)&&seen.Add(result.Text))values.Add(result.Text);if(values.Count==0)throw new Exception("未识别到二维码，请框选完整二维码并包含四周留白。");if(values.Count==1)return values[0];var text=new System.Text.StringBuilder();for(int i=0;i<values.Count;i++){if(i>0)text.Append("\r\n\r\n");text.Append("二维码 "+(i+1)+"\r\n"+values[i]);}return text.ToString();}
 public static bool WebUrl(string text,out Uri uri){return Uri.TryCreate(text.Trim(),UriKind.Absolute,out uri)&&(uri.Scheme=="http"||uri.Scheme=="https")&&text.IndexOf('\n')<0&&text.IndexOf('\r')<0;}
}

static class SmartIntent {
 public static int Parse(string text){switch(text.Trim().ToLowerInvariant()){case "translate":return 1;case "table":return 4;case "explain":return 2;case "summary":return 3;case "ocr":return 0;default:throw new Exception("未能判断处理方式，请使用 Alt+1～6 指定功能。");}}
}
