using OpenCvSharp;
using System.Drawing.Imaging;
using OpenCvSharp.Extensions;
using Point = OpenCvSharp.Point;
using System.Diagnostics;
using System.DirectoryServices.ActiveDirectory;
using System.Text;
using Microsoft.CSharp;
using OpenCvSharp.XFeatures2D;


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

        public class ImgData
        {
            public Mat src;
            public Mat desc = new Mat();
            public KeyPoint[] KeyPoints;
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
    internal partial class Program
    {
        private static Dictionary<ImgDesc, ImgDesc.ImgData> clickimg = new Dictionary<ImgDesc, ImgDesc.ImgData>();
        private static StringBuilder records = new StringBuilder();
        private static Dictionary<ImgDesc, ImgDesc.ImgData> defaultimg = new Dictionary<ImgDesc, ImgDesc.ImgData>();
        private static Random rand;
        public static SURF sift;
        static void loadimg()
        {
            clickimg.Clear();
            defaultimg.Clear();
            rand = new Random(DateTime.Now.Second);

            Action<string, string> loadfile = (sd, file) =>
            {
                var sf = Path.GetFileNameWithoutExtension(file);
                var src = Cv2.ImRead(file);
                ImgDesc.ImgData data = new ImgDesc.ImgData();
                data.src = src;
                Mat gray = new Mat();
                Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

                sift.DetectAndCompute(gray, null, out data.KeyPoints, data.desc);
                src.ConvertTo(src, MatType.CV_32FC3);

                var desc = ImgDesc.Parse(sd, sf);
                clickimg.Add(desc, data);
                if (desc.isDefault)
                    defaultimg.Add(desc, data);
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
                ImgDesc.ImgData data = new ImgDesc.ImgData();
                data.src = src;
                Mat gray = new Mat();
                Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
                src.ConvertTo(src, MatType.CV_32FC3);

                sift.DetectAndCompute(gray, null, out data.KeyPoints, data.desc);
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
                        var r = search(out rect, data, em.Current.Value);
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
                                var r = search(out rect, data, em.Current.Value);
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

                ImgDesc.ImgData data = new ImgDesc.ImgData();
                data.src = src;

                Mat gray = new Mat();
                Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
                sift.DetectAndCompute(gray, null, out data.KeyPoints, data.desc);
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
                            var r = search(out rect, data, em.Current.Value);
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
                ImgDesc fd = search(ref rect, data);

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
                                var r = search(out rect, data, em.Current.Value);
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
                    var r = search(out rect, data, desc.Value);
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

        static ImgDesc search(ref Rect rect, ImgDesc.ImgData src)
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

        private static bool useSurf = true;
        static double search(out Rect rect, ImgDesc.ImgData src, ImgDesc.ImgData tosearch)
        {
            if (!useSurf)
            {
                Mat out1 = new Mat();
                Cv2.MatchTemplate(src.src, tosearch.src, out1, TemplateMatchModes.CCoeffNormed);


                double r1, r2;
                Point minLoc, maxLoc;
                out1.MinMaxLoc(out r1, out r2, out minLoc, out maxLoc);

                rect.X = maxLoc.X;
                rect.Y = maxLoc.Y;
                rect.Width = tosearch.src.Width;
                rect.Height = tosearch.src.Height;

                return r2;
            }

            var w = tosearch.src.Width;
            var h = tosearch.src.Height;
            Point[] drawingPoints;
            List<DMatch> goods;
            double val = GetMatchRect((KeyPoint[])tosearch.KeyPoints.Clone(), (KeyPoint[])src.KeyPoints.Clone(),
                tosearch.desc.Clone(), src.desc.Clone(), 0.5f, w, h, out drawingPoints, out goods);

            if (drawingPoints != null)
            {
                int minx = Math.Min(drawingPoints[0].X, drawingPoints[1].X);
                int minx1 = Math.Min(drawingPoints[2].X, drawingPoints[3].X);

                int miny = Math.Min(drawingPoints[0].Y, drawingPoints[1].Y);
                int miny1 = Math.Min(drawingPoints[2].Y, drawingPoints[3].Y);

                int maxx = Math.Max(drawingPoints[0].X, drawingPoints[1].X);
                int maxx1 = Math.Max(drawingPoints[2].X, drawingPoints[3].X);

                int maxy = Math.Max(drawingPoints[0].Y, drawingPoints[1].Y);
                int maxy1 = Math.Max(drawingPoints[2].Y, drawingPoints[3].Y);


                rect.X = Math.Min(minx, minx1);
                rect.Y = Math.Min(miny, miny1);
                rect.Width = Math.Max(maxx, maxx1) - rect.X;
                rect.Height = Math.Max(maxy, maxy1) - rect.Y;
            }
            else
            {
                rect = default(Rect);
            }
            return 1 - val;
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

        static double GetMatchRect(KeyPoint[] keypoints1, KeyPoint[] keypoints2,
            Mat descriptors1, Mat descriptors2, float ide, int w, int h, out Point[] drawingPoints, out List<DMatch> goods)
        {
            if (keypoints1.Length == 0 || keypoints2.Length == 0)
            {
                goods = null;
                drawingPoints = null;
                return 999;
            }
            // Match descriptor vectors
            using var flannMatcher = new BFMatcher();
            Console.WriteLine($"{descriptors1.Type()} {keypoints1.Length} {descriptors2.Type()} {keypoints2.Length}");
            DMatch[] flannMatches = flannMatcher.Match(descriptors1, descriptors2);
            List<DMatch> list = flannMatches.ToList();
            list.Sort((a, b) =>
            {
                return a.Distance.CompareTo(b.Distance);
            });
            float mindis = list[0].Distance;
            goods = new List<DMatch>();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Distance < MathF.Max(mindis * 2, ide) && goods.Count < 10)
                {
                    goods.Add(list[i]);
                }
                //Console.WriteLine($"{list[i]}");
            }

            if (goods.Count > 4)
            {
                List<Point2f> p1 = new List<Point2f>();
                List<Point2f> p2 = new List<Point2f>();
                for (int i = 0; i < goods.Count; i++)
                {
                    p1.Add(keypoints1[goods[i].QueryIdx].Pt);
                    p2.Add(keypoints2[goods[i].TrainIdx].Pt);
                }
                var mat = Cv2.FindHomography(InputArray.Create(p1), InputArray.Create(p2), HomographyMethods.USAC_DEFAULT);
                List<Point2f> p0 = new List<Point2f>();

                p0.Add(new Point2f(0, 0));
                p0.Add(new Point2f(0, h - 1));
                p0.Add(new Point2f(w - 1, h - 1));
                p0.Add(new Point2f(w - 1, 0));

                if (!mat.Empty())
                {
                    var p01 = Cv2.PerspectiveTransform(p0, mat);
                    drawingPoints = p01.Select(p => (Point)p).ToArray();
                    var orgPoints = p0.Select(p => (Point)p).ToArray();

                    var ms = Cv2.MatchShapes(orgPoints, drawingPoints, ShapeMatchModes.I1);
                    return ms;
                }
            }

            drawingPoints = null;
            return 999;
        }

        private static void MatchBySift(Mat src1, Mat src2)
        {
            using var gray1 = new Mat();
            using var gray2 = new Mat();
            Cv2.CvtColor(src1, gray1, ColorConversionCodes.BGR2GRAY);
            Cv2.CvtColor(src2, gray2, ColorConversionCodes.BGR2GRAY);

            // Detect the keypoints and generate their descriptors using SIFT
            using var sift = SURF.Create(300);
            using Mat<float> descriptors1 = new Mat<float>();
            using Mat<float> descriptors2 = new Mat<float>();
            sift.DetectAndCompute(gray1, null, out KeyPoint[] keypoints1, descriptors1);
            sift.DetectAndCompute(gray2, null, out KeyPoint[] keypoints2, descriptors2);

            var w = gray1.Width;
            var h = gray1.Height;
            Point[] drawingPoints;
            List<DMatch> goods;
            double val = GetMatchRect(keypoints1, keypoints2, descriptors1, descriptors2, 0.4f, w, h, out drawingPoints, out goods);
            Console.WriteLine($"MatchShapes {val}");

            if(drawingPoints != null)
                Cv2.Polylines(gray2, new[] { drawingPoints }, true, Scalar.Aqua, 1);

            // Draw matches
            using var flannView = new Mat();
            Cv2.DrawMatches(gray1, keypoints1, gray2, keypoints2, goods.ToArray(), flannView);

            new Window("SIFT matching (by FlannBasedMatcher)", flannView);
            Cv2.WaitKey();
        }

        private static string imgdir;

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
                    case "useSurf":
                        useSurf = bool.Parse(k);
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
            sift = SURF.Create(300);
            loadimg();

            if (tes)
            {
                test();

                Mat src1 = makeScreenshot();
                Mat src2 = new Mat("./test.png");
                MatchBySift(src2, src1);
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

    }
}
