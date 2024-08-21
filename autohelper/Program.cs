using OpenCvSharp;
using System.Drawing.Imaging;
using OpenCvSharp.Extensions;
using Point = OpenCvSharp.Point;
using System.Diagnostics;
using System.DirectoryServices.ActiveDirectory;
using System.Text;
using Microsoft.CSharp;


// todo gpu acc, location game window, call return
namespace autohelper
{
    class ImgDesc
    {
        public int queue = 0;
        public int delay = 0;
        public string selectImage;
        public double[] clickPoint = new[] { 0.5, 0.5 };
        public double ide = 0.9;
        public double findide;
        public string name;
        public string dir;
        public bool loop;
        public bool abpoint = false;
        public bool wait = false;
        public bool isDefault;
        public static ImgDesc Parse(string dir, string ms)
        {
            ImgDesc desc = new ImgDesc();
            desc.name = ms;
            desc.dir = dir;
            Program.parseKey(ms, (m, k) =>
            {
                switch (m)
                {
                    case "select":
                        desc.selectImage = k.Replace('&', '@').Replace('_', ',');
                        break;
                    case "loop":
                        desc.loop = true;
                        break;
                    case "clickpoint_ab":
                    {
                        desc.abpoint = true;
                        var cps = k.Split('&');
                        double x = double.Parse(cps[0]);
                        double y = double.Parse(cps[1]);
                        desc.clickPoint = new[] { x, y };
                        break;
                        }

                    case "clickpoint":
                    {
                        var cps = k.Split('&');
                        double x = double.Parse(cps[0]);
                        double y = double.Parse(cps[1]);
                        desc.clickPoint = new[] { x, y };
                        break;
                        }
                    case "wait":
                    {
                        desc.wait = true;
                        break;
                    }
                    case "default":
                        desc.isDefault = true;
                        break;
                    case "delay":
                        desc.delay = int.Parse(k);
                        break;
                    default:
                        if (m.StartsWith('~'))
                            desc.queue = -1;
                        else
                            desc.queue = ToInt(m);
                        double.TryParse(k, out desc.ide);
                        break;
                }
            });
            return desc;
        }
        public static int ToInt(string str)
        {
            int num = 0;
            bool flag = false;
            if (string.IsNullOrEmpty(str))
                return 0;
            int index = 0;
            while (index < str.Length && !char.IsDigit(str[index]))
                ++index;
            if (index > 0 && str[index - 1] == '-')
                flag = true;
            for (; index < str.Length && char.IsDigit(str[index]); ++index)
                num = 10 * num + ((int)str[index] - 48);
            return flag ? -num : num;
        }
    }
    internal class Program
    {
        private static Dictionary<ImgDesc, Mat> clickimg = new Dictionary<ImgDesc, Mat>();
        private static StringBuilder records = new StringBuilder();
        private static Dictionary<ImgDesc, Mat> defaultimg = new Dictionary<ImgDesc, Mat>();
        private static Random rand;

        static void loadimg()
        {
            clickimg.Clear();
            defaultimg.Clear();
            rand = new Random(DateTime.Now.Second);

            Action<string, string> loadfile = (sd, file) =>
            {
                var sf = Path.GetFileNameWithoutExtension(file);
                var src = Cv2.ImRead(file);
                src.ConvertTo(src, MatType.CV_32FC3);

                var desc = ImgDesc.Parse(sd, sf);
                clickimg.Add(desc, src);
                if (desc.isDefault)
                    defaultimg.Add(desc, src);
            };

            Action<string> loaddir = (s) =>
            {
                var sd = Path.GetFileName(s);
                var files = Directory.GetFiles("./" + s, "*.png", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    loadfile(sd, file);
                }

                files = Directory.GetFiles("./" + s, "*.bmp", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    loadfile(sd, file);
                }
            };

            if (!string.IsNullOrEmpty(imgdir))
            {
                loaddir(imgdir);
            }
            else
            {
                var dir = Directory.GetDirectories("./", "*", SearchOption.TopDirectoryOnly);
                foreach (var s in dir)
                {
                    loaddir(s);
                }
            }
        }

        static void clickrecord(int delay)
        {
            var lines = File.ReadAllLines("./record.txt");
            int i = 0;
            while (i < lines.Length)
            {
                Mat src = makeScreenshot();
                
                Cv2.CvtColor(src, src, ColorConversionCodes.BGRA2BGR, 3);
                src.ConvertTo(src, MatType.CV_32FC3);

                Console.WriteLine(lines[i]);

                ImgDesc lastfd = null;
                Rect rect = new Rect();
                Action click = () =>
                {
                    i++;
                    int px = rect.X + (int)(rect.Width * lastfd.clickPoint[0]);
                    int py = rect.Y + (int)(rect.Height * lastfd.clickPoint[1]);
                    Console.WriteLine($"click {lastfd.name} {px} {py}");
                    SetCursorPosition(px, py);
                    Cv2.WaitKey(30);
                    MouseEvent(MouseOperations.MouseEventFlags.LeftDown);
                    Cv2.WaitKey(30);
                    MouseEvent(MouseOperations.MouseEventFlags.LeftUp);
                    Cv2.WaitKey(30);
                };

                var em = clickimg.GetEnumerator();
                while (em.MoveNext())
                {
                    if (em.Current.Key.name == lines[i])
                    {
                        double val = em.Current.Key.ide;
                        var r = search(out rect, src, em.Current.Value);
                        Console.WriteLine($"{em.Current.Key.name}: {r} {r >= val}");
                        if (r > em.Current.Key.ide)
                        {
                            lastfd = em.Current.Key;
                        }
                        break;
                    }
                }
                if (lastfd != null)
                    
                {
                    if (string.IsNullOrEmpty(lastfd.selectImage))
                        click();
                    else
                    {
                        Console.WriteLine($"select :{lastfd.selectImage}");
                        em = clickimg.GetEnumerator();
                        while (em.MoveNext())
                        {
                            if (em.Current.Key.name == lastfd.selectImage && em.Current.Key.dir == lastfd.dir)
                            {
                                double val = em.Current.Key.ide;
                                var r = search(out rect, src, em.Current.Value);
                                Console.WriteLine($"{em.Current.Key.name}: {r} {r >= val}");
                                if (r > em.Current.Key.ide)
                                {
                                    click();
                                }
                                break;
                            }
                        }
                    }
                }

                GC.Collect();
                Cv2.WaitKey(delay);
            }


        }
        static void clickimage(int delay)
        {
            ImgDesc lastfd = null;
            while (true)
            {
                __continue:
                if(autoload)
                    loadimg();

                Mat src = makeScreenshot();
                //src.CvtColor(ColorConversionCodes.RGBA2RGB, 3);
                Cv2.CvtColor(src, src, ColorConversionCodes.BGRA2BGR, 3);
                src.ConvertTo(src, MatType.CV_32FC3);


                Rect rect = new Rect();
                Action click = () =>
                {
                    if (record)
                    {
                        records.AppendLine(lastfd.name);
                        using (StreamWriter writer = File.AppendText("./record.txt"))
                        {
                            writer.WriteLine(lastfd.name);
                        }
                    }
                    int px = rect.X + (int)(rect.Width * lastfd.clickPoint[0]);
                    int py = rect.Y + (int)(rect.Height * lastfd.clickPoint[1]);
                    if (lastfd.abpoint)
                    {
                        px = src.Width / 2;
                        py = src.Height / 2;
                        Console.WriteLine($"ab point {px} {py}");
                    }
                    Console.WriteLine($"click {lastfd.name} {px} {py}");
                    if (lastfd.wait)
                        return;
                    SetCursorPosition(px, py);
                    Cv2.WaitKey(30);
                    MouseEvent(MouseOperations.MouseEventFlags.LeftDown);
                    Cv2.WaitKey(30);
                    MouseEvent(MouseOperations.MouseEventFlags.LeftUp);
                    Cv2.WaitKey(30);
                };
                if (lastfd != null && lastfd.loop)
                {
                    Console.WriteLine($"last click :{lastfd.name}");
                    var em = clickimg.GetEnumerator();
                    while (em.MoveNext())
                    {
                        if (em.Current.Key.name == lastfd.name && em.Current.Key.dir == lastfd.dir)
                        {
                            double val = em.Current.Key.ide;
                            var r = search(out rect, src, em.Current.Value);
                            Console.WriteLine($"{em.Current.Key.name}: {r} {r >= val}");
                            if (r > em.Current.Key.ide)
                            {
                                click();
                                GC.Collect();
                                goto __continue;
                            }
                            break;
                        }
                    }
                }
                ImgDesc fd = search(ref rect, src);

                if (fd != null)
                {

                    if (string.IsNullOrEmpty(fd.selectImage))
                    {
                        lastfd = fd;
                        click();
                    }
                    else
                    {
                        Console.WriteLine($"select :{fd.selectImage}");
                        var em = clickimg.GetEnumerator();
                        while (em.MoveNext())
                        {
                            if (em.Current.Key.name == fd.selectImage && em.Current.Key.dir == fd.dir)
                            {
                                double val = em.Current.Key.ide;
                                var r = search(out rect, src, em.Current.Value);
                                Console.WriteLine($"{em.Current.Key.name}: {r} {r >= val}");
                                if (r > em.Current.Key.ide)
                                {
                                    lastfd = fd;
                                    click();
                                }
                                break;
                            }
                        }
                    }
                }
                else if (defaultimg.Count > 0)
                {
                    var list = defaultimg.ToList();
                    int idx = rand.Next(0, list.Count);

                    var desc = list[idx];
                    double val = desc.Key.ide;
                    var r = search(out rect, src, desc.Value);
                    Console.WriteLine($"default {desc.Key.name}: {r} {r >= val}");
                    if (r > desc.Key.ide)
                    {
                        lastfd = desc.Key;
                        click();
                    }
                }

                GC.Collect();
                if(lastfd != null && lastfd.delay > 0)
                    Cv2.WaitKey(lastfd.delay);
                else
                    Cv2.WaitKey(delay);
            }
        }

        static ImgDesc search(ref Rect rect, Mat src)
        {
            List<KeyValuePair<ImgDesc, Rect>> finds = new List<KeyValuePair<ImgDesc, Rect>>();
            var em = clickimg.GetEnumerator();
            while (em.MoveNext())
            {
                if(em.Current.Key.queue < 0)
                    continue;
                double val = em.Current.Key.ide;
                Rect now;
                var r = search(out now, src, em.Current.Value);
                if (r >= val)
                {
                    em.Current.Key.findide = r;
                    finds.Add(new KeyValuePair<ImgDesc, Rect>(em.Current.Key, now));
                }
                Console.WriteLine($"{em.Current.Key.name}: {r} {r >= val}");
            }
            finds.Sort((a, b) =>
            {
                var c = b.Key.queue.CompareTo(a.Key.queue);
                if (c != 0) return c;
                c = b.Key.findide.CompareTo(a.Key.findide);
                if (c != 0) return c;
                return b.Value.GetHashCode().CompareTo(a.Value.GetHashCode());
            });
            if (finds.Count > 0)
            {
                rect = finds[0].Value;
                return finds[0].Key;
            }
            return null;
        }

        static double search(out Rect rect, Mat src, Mat tosearch)
        {
            Mat out1 = new Mat();
            Cv2.MatchTemplate(src, tosearch, out1, TemplateMatchModes.CCoeffNormed);

            
            double r1, r2;
            Point minLoc, maxLoc;
            out1.MinMaxLoc(out r1, out r2, out minLoc, out maxLoc);


            rect.X = maxLoc.X;
            rect.Y = maxLoc.Y;
            rect.Width = tosearch.Width;
            rect.Height = tosearch.Height;

            return r2;
        }

        public static void parseKey(string arg, Action<string, string> call)
        {
            var als = arg.Split(',');
            foreach (var al in als)
            {
                var one = al.Split('@');
                if (one.Length > 1)
                    call(one[0], one[1]);
                else
                    call(one[0], "");
            }
        }

        private static bool autoload = false;
        private static bool record = false;
        private static bool play = false;

        private static string imgdir = "";

        static void Main(string[] args)
        {
            bool tes = false;
            int delay = 500;
            bool screenshot = false;
            Action<string, string> useKey = (m, k) =>
            {
                switch (m)
                {
                    case "autoload":
                        autoload = true;
                        break;
                    case "record":
                        record = true;
                        break;
                    case "play":
                        play = true;
                        break;
                    case "test":
                        tes = true;
                        break;
                    case "screenshot":
                        screenshot = true;
                        break;
                    case "adb":
                        initAdb(false);
                        if (!string.IsNullOrEmpty(k))
                        {
                            if (!string.IsNullOrEmpty(k))
                            {
                                if (k.Contains(":"))
                                    adbport = $"-s {k} ";
                                else
                                    adbport = $"-s 127.0.0.1:{k} ";
                            }
                        }
                        break;
                    case "imgdir":
                        imgdir = k;
                        break;
                    default:
                        if (!string.IsNullOrEmpty(k))
                            int.TryParse(k, out delay);
                        Console.WriteLine($"delay {delay}");
                        break;
                }
            };

            parseKey(Process.GetCurrentProcess().ProcessName, (m, k) =>
            {
                useKey(m, k);
            });
            if (args.Length > 0)
            {
                parseKey(args[0], (m, k) =>
                {
                    useKey(m, k);
                });
            }
            loadimg();

            if (tes)
            {
                test();
                return;
            }

            if (screenshot)
            {
                if (adb)
                {
                    makeScreenshotAdb();
                }
                else
                {
                    var img = makeScreenshotbmp();
                    img.Save("./screenshot.bmp");
                }
                return;
            }

            if (play)
                clickrecord(delay);
            else
                clickimage(delay);
        }

        private static void test()
        {
            Mat cv1 = Cv2.ImRead("./1.png");
            cv1.ConvertTo(cv1, MatType.CV_32FC1);
            Mat cv2 = Cv2.ImRead("./2.png");
            cv2.ConvertTo(cv2, MatType.CV_32FC1);

            Mat out1 = new Mat();
            Cv2.MatchTemplate(cv2, cv1, out1, TemplateMatchModes.CCoeffNormed);

            double r1, r2;
            Point minLoc, maxLoc;
            out1.MinMaxLoc(out r1, out r2, out minLoc, out maxLoc);

            int x = maxLoc.X;
            int y = maxLoc.Y;
            int w = maxLoc.X + cv1.Width;
            int h = maxLoc.Y + cv1.Height;

            cv2 = Cv2.ImRead("./2.png");
            Cv2.Rectangle(cv2, new Point(x, y), new Point(w, h), new Scalar(255, 0, 0), 3);
            Cv2.ImShow("window", cv2);
            cv1 = Cv2.ImRead("./1.png");
            Cv2.ImShow("window11", cv1);

            Console.WriteLine(r2.ToString());
            Cv2.WaitKey(0);

        }

        public static Mat makeScreenshot()
        {
            if (adb)
                return makeScreenshotAdb();
            else
                return BitmapConverter.ToMat(makeScreenshotbmp());
        }

        public static Bitmap makeScreenshotbmp()
        {
            Bitmap screenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height, PixelFormat.Format32bppArgb);

            Graphics gfxScreenshot = Graphics.FromImage(screenshot);

            gfxScreenshot.CopyFromScreen(Screen.PrimaryScreen.Bounds.X, Screen.PrimaryScreen.Bounds.Y, 0, 0, Screen.PrimaryScreen.Bounds.Size, CopyPixelOperation.SourceCopy);

            gfxScreenshot.Dispose();

            return screenshot;
        }

        public static bool adb = false;
        private static string adbport = "";

        private static void initAdb(bool _screenshotadb)
        {
            adb = true;
        }

        public static Mat makeScreenshotAdb()
        {
            Process pr2 = new Process();
            pr2.StartInfo.FileName = @"c:\windows\system32\cmd.exe";
            pr2.StartInfo.Arguments = $"/c \"adb.exe {adbport}exec-out screencap -p > adb.png\"";
            pr2.Start();
            pr2.WaitForExit();

            return Cv2.ImRead("adb.png");

            var outputStream = new StreamWriter("adb.png");
            Process process = new Process();
            process.StartInfo.FileName = "adb.exe";
            process.StartInfo.Arguments = "exec-out screencap -p";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
            {
                if (!String.IsNullOrEmpty(e.Data))
                {
                    outputStream.WriteLine(e.Data);
                }
            });

            process.Start();

            process.BeginOutputReadLine();

            process.WaitForExit();
            process.Close();

            outputStream.Close();
            
            return Cv2.ImRead("adb.png");
        }

        private static int adb_x, adb_y;
        public static void SetCursorPosition(int x, int y)
        {
            if (adb)
            {
                adb_x = x;
                adb_y = y;
            }
            else
            {
                MouseOperations.SetCursorPosition(x, y);
            }
        }
        public static void MouseEvent(MouseOperations.MouseEventFlags value)
        {
            if (adb)
            {
                if (value == MouseOperations.MouseEventFlags.LeftUp)
                {
                    double scale = 1;
                    Process.Start(new ProcessStartInfo("adb.exe", $"{adbport}shell input tap {adb_x * scale} {adb_y * scale}"))?.WaitForExit(-1);
                }
            }
            else
            {
                MouseOperations.MouseEvent(value);
            }
        }
    }
}
