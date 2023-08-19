using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

using OpenCvSharp;
using OpenCvSharp.Dnn;
using OpenCvSharp.WpfExtensions;

using ZXing;

namespace OpenCVCamera
{
    internal class MainViewModel:ViewModelBase,IDisposable
    {

        ImageSource _frameImage;
        public ImageSource FrameImage
        {
            get
            {
                return _frameImage;
            }
            set
            {
                this.Set("FrameImage", ref _frameImage, value);
            }
        }

        private string _code;
        public string Code
        {
            get
            {
                return _code;
            }
            set
            {
                this.Set("Code", ref _code, value);
            }
        }
        private readonly VideoCapture capture;
        private readonly CascadeClassifier cascadeClassifier;

        private readonly BackgroundWorker bkgWorker;

        public MainViewModel()
        {

            capture = new VideoCapture();
            cascadeClassifier = new CascadeClassifier("haarcascade_frontalface_default.xml");

            bkgWorker = new BackgroundWorker { WorkerSupportsCancellation = true };
            bkgWorker.DoWork += Worker_DoWork;

            Load();  
        }

        private void MainWindow_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            capture.Open(0, VideoCaptureAPIs.ANY);
            if (!capture.IsOpened())
            {
                Close();
                return;
            }

            bkgWorker.RunWorkerAsync();
        }


        void Load()
        {
            capture.Open(0, VideoCaptureAPIs.ANY);
            if (!capture.IsOpened())
            {
                Close();
                return;
            }

            bkgWorker.RunWorkerAsync();
        }

        void Close()
        {
            bkgWorker.CancelAsync();

            capture.Dispose();
            cascadeClassifier.Dispose();
        }


        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            bkgWorker.CancelAsync();

            capture.Dispose();
            cascadeClassifier.Dispose();
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            var worker = (BackgroundWorker)sender;
            

            int line = 0;
            while (!worker.CancellationPending)
            {
                using (Mat frameMat = capture.RetrieveMat())
                {
                    var type = frameMat.Type();

                  

                    //图像转换为灰度图像
                    Mat grayImage = new Mat();
                    OpencvHelper.CvGrayImage(frameMat, grayImage);

                    //OpencvHelper.RotateImage(grayImage, grayImage, 50, 1);
                    //OpencvHelper.Imshow("旋转", grayImage);

                    //建立图像的梯度幅值
                    Mat gradientImage = new Mat();
                    OpencvHelper.CvConvertScaleAbs(grayImage, gradientImage);

                    //对图片进行相应的模糊化,使一些噪点消除
                    Mat blurImage = new Mat();
                    Mat thresholdImage = new Mat();
                    OpencvHelper.BlurImage(gradientImage, blurImage, thresholdImage);

                    //二值化以后的图像,条形码之间的黑白没有连接起来,就要进行形态学运算,消除缝隙,相当于小型的黑洞,选择闭运算
                    //因为是长条之间的缝隙,所以需要选择宽度大于长度
                    Mat morphImage = new Mat();
                    OpencvHelper.MorphImage(thresholdImage, morphImage);

                    //现在要让条形码区域连接在一起,所以选择膨胀腐蚀,而且为了保持图形大小基本不变,应该使用相同次数的膨胀腐蚀
                    //先腐蚀,让其他区域的亮的地方变少最好是消除,然后膨胀回来,消除干扰,迭代次数根据实际情况选择
                    OpencvHelper.DilationErosionImage(morphImage);


                    Mat[] contours = new Mat[10000];
                    List<double> OutArray = new List<double>();
                    //接下来对目标轮廓进行查找,目标是为了计算图像面积
                    Cv2.FindContours(morphImage, out contours, OutputArray.Create(OutArray), RetrievalModes.External, ContourApproximationModes.ApproxSimple);
                    //看看轮廓图像
                    //Cv2.DrawContours(srcImage, contours, -1, Scalar.Yellow);
                    //OpencvHelper.Imshow("目标轮廓", srcImage);

                    //计算轮廓的面积并且存放
                    for (int i = 0; i < OutArray.Count; i++)
                    {
                        OutArray[i] = contours[i].ContourArea(false);
                    }

                    List<string> codes = new List<string>();
                    int num = 0;
                    while (num < 10&& OutArray.Count>0) //找出10个面积最大的矩形
                    {
                        //找出面积最大的轮廓
                        double minValue, maxValue;
                        OpenCvSharp.Point minLoc, maxLoc;
                        Cv2.MinMaxLoc(InputArray.Create(OutArray), out minValue, out maxValue, out minLoc, out maxLoc);
                        //计算面积最大的轮廓的最小的外包矩形
                        RotatedRect minRect = Cv2.MinAreaRect(contours[maxLoc.Y]);
                        //找到了矩形的角度,但是这是一个旋转矩形,所以还要重新获得一个外包最小矩形
                        OpenCvSharp.Rect myRect = Cv2.BoundingRect(contours[maxLoc.Y]);
                        //将扫描的图像裁剪下来,并保存为相应的结果,保留一些X方向的边界,所以对rect进行一定的扩张
                        myRect.X = myRect.X - (myRect.Width / 20);
                        myRect.Width = (int)(myRect.Width * 1.1);

                        //TermCriteria termc = new TermCriteria(CriteriaType.MaxIter, 1, 1);
                        //Cv2.CamShift(srcImage, myRect, termc);

                        //一次最大面积的
                        var a = contours.ToList();
                        a.Remove(contours[maxLoc.Y]);
                        contours = a.ToArray();
                        OutArray.Remove(OutArray[maxLoc.Y]);

                        string code = DiscernBarCode(frameMat, myRect);
                        if (!string.IsNullOrEmpty(code))
                        {
                            //Cv2.Rectangle(srcImage, myRect, new Scalar(0, 255, 255), 3, LineTypes.AntiAlias);
                            codes.Add(code);
                            this.Code = code;
                            System.Diagnostics.Debug.WriteLine(code);
                        }
                        Cv2.Rectangle(frameMat, myRect, new Scalar(0, 255, 255), 3, LineTypes.AntiAlias);
                        num++;
                        if (contours.Count() <= 0)
                            break;
                    }
                    //frameMat.CvtColor();

                    //var rects = cascadeClassifier.DetectMultiScale(frameMat, 1.1, 5, HaarDetectionTypes.ScaleImage, new OpenCvSharp.Size(30, 30));
                    
                    //cascadeClassifier.GetOriginalWindowSize();
                    //foreach (var rect in rects)
                    //{
                    //    //System.Diagnostics.Debug.WriteLine($"{rect.Width} {rect.Height} {rect.X} {rect.Y}");
                    //    Cv2.Rectangle(frameMat, rect, Scalar.Green);
                    //}

                    //if (line > 500)
                    //    line = 0;

                    //Cv2.Line(frameMat, new OpenCvSharp.Point(0, line), new OpenCvSharp.Point(700, line), Scalar.Green, 2);
                    //line += 8;


                    // Must create and use WriteableBitmap in the same thread(UI Thread).
                    Application.Current.Dispatcher.Invoke(callback: () =>
                    {
                        FrameImage = frameMat.ToWriteableBitmap();

                        // create a barcode reader instance
                        ZXing.OpenCV.BarcodeReader reader = new ZXing.OpenCV.BarcodeReader();
                        // load a bitmap
                        //var barcodeBitmap = FrameImage.Source;
                        // detect and decode the barcode inside the bitmap

                        var result = reader.Decode(frameMat);
                      
                        //string protoPath = AppContext.BaseDirectory + "sr.prototxt";
                        //string modelPath = AppContext.BaseDirectory + "sr.caffemodel";
                        
                        //var srnet = CvDnn.ReadNetFromCaffe(protoPath, modelPath);
                        //Mat blob = CvDnn.BlobFromImage(frameMat, 1.0 / 255, frameMat.Size(), new Scalar(0.0f), false, false);
                        //srnet.SetInput(blob);
                        //var prob = srnet.Forward();

                        //var result = reader.Decode(prob);
                        // do something with the result
                        if (result != null)
                        {

                            //var rects = cascadeClassifier.DetectMultiScale(frameMat, 1.1, 5, HaarDetectionTypes.ScaleImage, new OpenCvSharp.Size(30, 30));

                            //foreach (ResultPoint rect in result.ResultPoints)
                            //{
                            //    System.Diagnostics.Debug.WriteLine($"{rect.X} {rect.Y}");
                              
                            //}

                            //Cv2.Rectangle(frameMat, new OpenCvSharp.Point(result.ResultPoints[0].X, result.ResultPoints[0].Y), new OpenCvSharp.Point(result.ResultPoints[1].X, result.ResultPoints[1].Y), Scalar.Green);

                            //System.Diagnostics.Debug.WriteLine(result.BarcodeFormat.ToString());
                            //System.Diagnostics.Debug.WriteLine(result.Text);
                            //Thread.Sleep(500);
                        }
                    });
                }

                Thread.Sleep(30);
            }
        }
        /// <summary>
        /// 解析条形码图片
        /// </summary>
        private string DiscernBarCode(Mat srcImage, OpenCvSharp.Rect myRect)
        {
            try
            {
                ZXing.OpenCV.BarcodeReader reader = new ZXing.OpenCV.BarcodeReader();
                var result = reader.Decode(srcImage);
                //Mat resultImage = new Mat(srcImage, myRect);
                //System.Drawing.Image img = CreateImage(resultImage);
                //Bitmap pImg = MakeGrayscale3((Bitmap)img);
                //BarcodeReader reader = new BarcodeReader();
                //reader.Options.CharacterSet = "UTF-8";
                //Result result = reader.Decode(new Bitmap(pImg));
                //Console.Write(result);
                if (result != null)
                    return result.ToString();
                else
                    return "";
            }
            catch (Exception ex)
            {
                //Console.Write(ex);
                return "";
            }
        }

        private System.Drawing.Image CreateImage(Mat resultImage)
        {
            byte[] bytes = resultImage.ToBytes();
            MemoryStream ms = new MemoryStream(bytes);
            return Bitmap.FromStream(ms, true);
        }

        private void HandelCode(Mat srcImage, OpenCvSharp.Rect myRect, Mat[] contours)
        {
            Mat resultImage = new Mat(srcImage, myRect);
            System.Drawing.Image img = CreateImage(resultImage);
           
            DiscernBarcode(img);
            //看看轮廓图像
            Cv2.DrawContours(srcImage, contours, -1, Scalar.Red);
            //把这个矩形在源图像中画出来
            Cv2.Rectangle(srcImage, myRect, new Scalar(0, 255, 255), 3, LineTypes.AntiAlias);
            //Image img2 = CreateImage(srcImage);
            //picFindContours.Image = img2;
        }
        /// <summary>
        /// 解析条形码图片
        /// </summary>
        private void DiscernBarcode(System.Drawing.Image primaryImage)
        {
            //Bitmap pImg = MakeGrayscale3((Bitmap)primaryImage);
            BarcodeReader reader = new BarcodeReader();
            reader.Options.CharacterSet = "UTF-8";
            Result result = reader.Decode(new Bitmap(primaryImage));//Image.FromFile(path)
            Console.Write(result);
            if (result != null)
                System.Diagnostics.Debug.WriteLine(result.ToString());
            else
                Console.WriteLine();
            //txtBarCode.Text = "";

            //watch.Start();
            //watch.Stop();
            //TimeSpan timeSpan = watch.Elapsed;
            //MessageBox.Show("扫描执行时间：" + timeSpan.TotalMilliseconds.ToString());


            //using (ZBar.ImageScanner scanner = new ZBar.ImageScanner())
            //{
            //    scanner.SetConfiguration(ZBar.SymbolType.None, ZBar.Config.Enable, 0);
            //    scanner.SetConfiguration(ZBar.SymbolType.CODE39, ZBar.Config.Enable, 1);
            //    scanner.SetConfiguration(ZBar.SymbolType.CODE128, ZBar.Config.Enable, 1);

            //    List<ZBar.Symbol> symbols = new List<ZBar.Symbol>();
            //    symbols = scanner.Scan((Image)pImg);
            //    if (symbols != null && symbols.Count > 0)
            //    {
            //        //string result = string.Empty;
            //        //symbols.ForEach(s => result += "条码内容:" + s.Data + " 条码质量:" + s.Type + Environment.NewLine);
            //        txtBarCode.Text = symbols.FirstOrDefault().Data;
            //    }
            //    else
            //    {
            //        txtBarCode.Text = "";
            //    }
            //}
        }
        /// <summary>
        /// 处理图片灰度
        /// </summary>
        /// <param name="original"></param>
        /// <returns></returns>
        public static Bitmap MakeGrayscale3(Bitmap original)
        {
            //create a blank bitmap the same size as original
            Bitmap newBitmap = new Bitmap(original.Width, original.Height);
            //get a graphics object from the new image
            Graphics g = Graphics.FromImage(newBitmap);
            //create the grayscale ColorMatrix
            System.Drawing.Imaging.ColorMatrix colorMatrix = new System.Drawing.Imaging.ColorMatrix(
               new float[][]
              {
                 new float[] {.3f, .3f, .3f, 0, 0},
                 new float[] {.59f, .59f, .59f, 0, 0},
                 new float[] {.11f, .11f, .11f, 0, 0},
                 new float[] {0, 0, 0, 1, 0},
                 new float[] {0, 0, 0, 0, 1}
              });
            //create some image attributes
            ImageAttributes attributes = new ImageAttributes();
            //set the color matrix attribute
            attributes.SetColorMatrix(colorMatrix);
            //draw the original image on the new image
            //using the grayscale color matrix
            g.DrawImage(original, new Rectangle(0, 0, original.Width, original.Height),
               0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);
            //dispose the Graphics object
            g.Dispose();
            return newBitmap;
        }
        public void Dispose()
        {
            System.Diagnostics.Debug.WriteLine("dispose");
            this.Close();
        }
    }
}
