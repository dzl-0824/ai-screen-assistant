using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Net.Mail;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Windows.Forms;
class MailSettings {
 public string Host="smtp.qq.com",From="",Secret="",Contacts="",Subject="屏幕截图";public int Port=465;public bool StartTls=false;
 public string Password(){try{return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(Secret),null,DataProtectionScope.CurrentUser));}catch{return "";}}
 public void SetPassword(string value){Secret=Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value),null,DataProtectionScope.CurrentUser));}
 public MailSettings Copy(){return (MailSettings)MemberwiseClone();}
 public string[] Recipients(){var values=new List<string>();foreach(var line in Contacts.Replace("\r","").Split('\n')){if(string.IsNullOrWhiteSpace(line))continue;var address=new MailAddress(line.Trim());if(address.Address.Contains("\r")||address.Address.Contains("\n"))throw new Exception("联系人邮箱格式不正确");if(!values.Contains(address.Address))values.Add(address.Address);}return values.ToArray();}
 public bool Configured(){return !string.IsNullOrWhiteSpace(From)||!string.IsNullOrWhiteSpace(Contacts)||!string.IsNullOrWhiteSpace(Password());}
 public void Validate(){if(string.IsNullOrWhiteSpace(Host)||Host.IndexOfAny(new[]{'\r','\n',' ','/'})>=0||Port<1||Port>65535)throw new Exception("请填写有效的 SMTP 地址和端口");var sender=new MailAddress(From);if(sender.Address!=From.Trim())throw new Exception("发件邮箱仅填写邮箱地址");if(Recipients().Length==0)throw new Exception("请至少配置一个收件人，每行一个邮箱");if(string.IsNullOrWhiteSpace(Password()))throw new Exception("请填写 SMTP 授权码");if(Subject.IndexOfAny(new[]{'\r','\n'})>=0)throw new Exception("邮件主题不能换行");}
}
static class ScreenshotMail {
 public static byte[] CaptureFullScreen(){var area=SystemInformation.VirtualScreen;using(var image=new Bitmap(area.Width,area.Height)){using(var g=Graphics.FromImage(image))g.CopyFromScreen(area.Location,Point.Empty,area.Size);using(var stream=new MemoryStream()){image.Save(stream,ImageFormat.Png);return stream.ToArray();}}}
 public static string Mime(MailSettings c,byte[] image){string boundary="ScreenText_"+Guid.NewGuid().ToString("N");string title=string.IsNullOrWhiteSpace(c.Subject)?"屏幕截图":c.Subject;var text=new StringBuilder();text.Append("From: <"+new MailAddress(c.From).Address+">\r\nTo: "+string.Join(", ",c.Recipients().Select(x=>"<"+x+">").ToArray())+"\r\nSubject: =?UTF-8?B?"+Convert.ToBase64String(Encoding.UTF8.GetBytes(title))+"?=\r\nDate: "+DateTime.UtcNow.ToString("r",System.Globalization.CultureInfo.InvariantCulture)+"\r\nMessage-ID: <"+Guid.NewGuid().ToString("N")+"@screentext.local>\r\nMIME-Version: 1.0\r\nContent-Type: multipart/mixed; boundary=\""+boundary+"\"\r\n\r\n");text.Append("--"+boundary+"\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Transfer-Encoding: base64\r\n\r\n"+Convert.ToBase64String(Encoding.UTF8.GetBytes("全屏截图 · "+DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")))+"\r\n");text.Append("--"+boundary+"\r\nContent-Type: image/png\r\nContent-Transfer-Encoding: base64\r\nContent-Disposition: attachment; filename=\"screenshot-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".png\"\r\n\r\n");string b64=Convert.ToBase64String(image);for(int i=0;i<b64.Length;i+=76)text.Append(b64.Substring(i,Math.Min(76,b64.Length-i))+"\r\n");text.Append("--"+boundary+"--\r\n");return text.ToString();}
 public static int Reply(Stream stream){var line=new StringBuilder();int code=0;for(int lines=0;lines<100;lines++){line.Clear();while(true){int b=stream.ReadByte();if(b<0)throw new IOException("邮件服务器提前断开连接");if(b==10)break;if(b!=13)line.Append((char)b);if(line.Length>8192)throw new IOException("SMTP 响应过长");}string value=line.ToString();if(value.Length<3||!int.TryParse(value.Substring(0,3),out code))throw new IOException("SMTP 响应无效");if(value.Length<4||value[3]!='-')return code;}throw new IOException("SMTP 响应过长");}
 public static void Command(Stream stream,string command,int expected){byte[] bytes=Encoding.ASCII.GetBytes(command+"\r\n");stream.Write(bytes,0,bytes.Length);stream.Flush();Expect(stream,expected);}
 static void Expect(Stream stream,int expected){int got=Reply(stream);if(got!=expected){if(got==535||got==534)throw new Exception("SMTP 认证失败，请检查发件邮箱、授权码和 SMTP 服务开关");if(got>=500&&expected==250)throw new Exception("邮件服务器拒绝请求，请检查收件人和发信权限（"+got+"）");throw new Exception("邮件服务器返回 "+got+"，预期 "+expected);}}
 public static void Deliver(Stream stream,MailSettings c,byte[] image){Command(stream,"AUTH LOGIN",334);Command(stream,Convert.ToBase64String(Encoding.UTF8.GetBytes(c.From)),334);Command(stream,Convert.ToBase64String(Encoding.UTF8.GetBytes(c.Password())),235);Command(stream,"MAIL FROM:<"+c.From+">",250);foreach(var recipient in c.Recipients())Command(stream,"RCPT TO:<"+recipient+">",250);Command(stream,"DATA",354);Command(stream,Mime(c,image)+".",250);}
 public static void Send(MailSettings c,byte[] image){c.Validate();using(var tcp=new TcpClient()){var connect=tcp.ConnectAsync(c.Host,c.Port);if(!connect.Wait(15000))throw new TimeoutException("SMTP 连接超时");connect.GetAwaiter().GetResult();tcp.ReceiveTimeout=tcp.SendTimeout=30000;Stream stream=tcp.GetStream();SslStream secure=null;try{if(c.StartTls){Expect(stream,220);Command(stream,"EHLO screentext.local",250);Command(stream,"STARTTLS",220);}secure=new SslStream(stream,false);secure.ReadTimeout=secure.WriteTimeout=30000;secure.AuthenticateAsClient(c.Host,null,SslProtocols.Tls12,true);stream=secure;if(!c.StartTls)Expect(stream,220);Command(stream,"EHLO screentext.local",250);Deliver(stream,c,image);try{byte[] quit=Encoding.ASCII.GetBytes("QUIT\r\n");stream.Write(quit,0,quit.Length);}catch{}}finally{if(secure!=null)secure.Dispose();}}}
}
