using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace OpenCVCamera
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        private readonly VideoCapture capture;
        private readonly CascadeClassifier cascadeClassifier;

        private readonly BackgroundWorker bkgWorker;

        public MainWindow()
        {
            InitializeComponent();


            this.image.DataContext = new MainViewModel();

            //capture = new VideoCapture();
            //cascadeClassifier = new CascadeClassifier("haarcascade_frontalface_default.xml");

            //bkgWorker = new BackgroundWorker { WorkerSupportsCancellation = true };
            //bkgWorker.DoWork += Worker_DoWork;

            //Loaded += MainWindow_Loaded;
            //Closing += MainWindow_Closing;
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

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            bkgWorker.CancelAsync();

            capture.Dispose();
            cascadeClassifier.Dispose();
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            var worker = (BackgroundWorker)sender;
            while (!worker.CancellationPending)
            {
                using (var frameMat = capture.RetrieveMat())
                {
                    var rects = cascadeClassifier.DetectMultiScale(frameMat, 1.1, 5, HaarDetectionTypes.ScaleImage, new OpenCvSharp.Size(30, 30));

                    foreach (var rect in rects)
                    {
                        Cv2.Rectangle(frameMat, rect, Scalar.Green);
                    }

                    // Must create and use WriteableBitmap in the same thread(UI Thread).
                    Dispatcher.Invoke(() =>
                    {
                        FrameImage.Source = frameMat.ToWriteableBitmap();

                        // create a barcode reader instance
                        ZXing.OpenCV.BarcodeReader reader = new ZXing.OpenCV.BarcodeReader();
                        // load a bitmap
                        //var barcodeBitmap = FrameImage.Source;
                        // detect and decode the barcode inside the bitmap
                        var result = reader.Decode(frameMat);
                        // do something with the result
                        if (result != null)
                        {
                            System.Diagnostics.Debug.WriteLine(result.BarcodeFormat.ToString());
                            System.Diagnostics.Debug.WriteLine(result.Text);
                            Thread.Sleep(1000);
                        }
                    });
                }

                Thread.Sleep(30);
            }
        }
    }
}
