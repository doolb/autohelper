using OpenCvSharp;
using System.Drawing;
using System.Drawing.Imaging;
using OpenCvSharp.Extensions;
using Point = OpenCvSharp.Point;
using static System.Net.Mime.MediaTypeNames;
using static System.Windows.Forms.AxHost;
using System.Diagnostics;
using System.Text;
using OpenCvSharp.Features2D;

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
                    case "clickpoint":
                        var cps = k.Split('&');
                        double x = double.Parse(cps[0]);
                        double y = double.Parse(cps[1]);
                        desc.clickPoint = new[] { x, y };
                        break;
                    case "default":
                        desc.isDefault = true;
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

        static void loadimg()
        {
            clickimg.Clear();
            var dir = Directory.GetDirectories("./","*", SearchOption.TopDirectoryOnly);
            foreach (var s in dir)
            {
                var sd = Path.GetFileName(s);
                var files = Directory.GetFiles("./" + s, "*.png", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var sf = Path.GetFileNameWithoutExtension(file);
                    var src = Cv2.ImRead(file);
                    src.ConvertTo(src, MatType.CV_32FC3);

                    var desc = ImgDesc.Parse(sd, sf);
                    clickimg.Add(desc, src);
                }
            }
        }

        static void clickrecord(int delay)
        {
            var lines = File.ReadAllLines("./record.txt");
            int i = 0;
            while (i < lines.Length)
            {
                Mat src = BitmapConverter.ToMat(makeScreenshot());
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
                    MouseOperations.SetCursorPosition(px, py);
                    Cv2.WaitKey(30);
                    MouseOperations.MouseEvent(MouseOperations.MouseEventFlags.LeftDown);
                    Cv2.WaitKey(30);
                    MouseOperations.MouseEvent(MouseOperations.MouseEventFlags.LeftUp);
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

                Mat src = BitmapConverter.ToMat(makeScreenshot());
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
                    Console.WriteLine($"click {lastfd.name} {px} {py}");
                    MouseOperations.SetCursorPosition(px, py);
                    Cv2.WaitKey(30);
                    MouseOperations.MouseEvent(MouseOperations.MouseEventFlags.LeftDown);
                    Cv2.WaitKey(30);
                    MouseOperations.MouseEvent(MouseOperations.MouseEventFlags.LeftUp);
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
                else
                {
                    var em = clickimg.GetEnumerator();
                    while (em.MoveNext())
                    {
                        if (em.Current.Key.isDefault)
                        {
                            double val = em.Current.Key.ide;
                            var r = search(out rect, src, em.Current.Value);
                            Console.WriteLine($"default {em.Current.Key.name}: {r} {r >= val}");
                            if (r > em.Current.Key.ide)
                            {
                                lastfd = em.Current.Key;
                                click();
                            }
                            break;
                        }
                    }
                }

                GC.Collect();
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
        private static bool adb = false;

        static void Main(string[] args)
        {
            bool tes = false;
            int delay = 500;
            parseKey(Process.GetCurrentProcess().ProcessName, (m, k) =>
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
                    case "adb":
                        adb = true;
                        break;
                    default:
                        if (!string.IsNullOrEmpty(k))
                            int.TryParse(k, out delay);
                        Console.WriteLine($"delay {delay}");
                        break;
                }
            });
            loadimg();

            if (tes)
            {
                test();
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

        public static Bitmap makeScreenshot()
        {
            Bitmap screenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height, PixelFormat.Format32bppArgb);

            Graphics gfxScreenshot = Graphics.FromImage(screenshot);

            gfxScreenshot.CopyFromScreen(Screen.PrimaryScreen.Bounds.X, Screen.PrimaryScreen.Bounds.Y, 0, 0, Screen.PrimaryScreen.Bounds.Size, CopyPixelOperation.SourceCopy);

            gfxScreenshot.Dispose();

            return screenshot;
        }
    }
}
