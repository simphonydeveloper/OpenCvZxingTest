using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OpenCvSharp;

namespace OpenCVCamera
{
    internal class OpencvHelper
    { /// <summary>
      /// 灰度图
      /// </summary>
      /// <param name="srcImage">未处理的mat容器</param>
      /// <param name="grayImage">灰度图mat容器</param>
        public static void CvGrayImage(Mat srcImage, Mat grayImage)
        {
            if (srcImage.Channels() == 3)
            {
                Cv2.CvtColor(srcImage, grayImage, ColorConversionCodes.BGR2GRAY);
            }
            else
            {
                grayImage = srcImage.Clone();
            }
            //Imshow("灰度图", grayImage);
        }
        /// <summary>
        /// 图像的梯度幅值
        /// </summary>
        /// <param name="grayImage"></param>
        public static void CvConvertScaleAbs(Mat grayImage, Mat gradientImage)
        {
            //建立图像的梯度幅值
            Mat gradientXImage = new Mat();
            Mat gradientYImage = new Mat();
            Cv2.Sobel(grayImage, gradientXImage, MatType.CV_32F, xorder: 1, yorder: 0, ksize: -1);
            Cv2.Sobel(grayImage, gradientYImage, MatType.CV_32F, xorder: 0, yorder: 1, ksize: -1);
            //Cv2.Scharr(grayImage, gradientXImage, MatType.CV_32F, 1, 0);//CV_16S  CV_32F
            //Cv2.Scharr(grayImage, gradientYImage, MatType.CV_32F, 0, 1);
            //因为我们需要的条形码在需要X方向水平,所以更多的关注X方向的梯度幅值,而省略掉Y方向的梯度幅值
            Cv2.Subtract(gradientXImage, gradientYImage, gradientImage);
            //归一化为八位图像
            Cv2.ConvertScaleAbs(gradientImage, gradientImage);
            //看看得到的梯度图像是什么样子
            //Imshow("图像的梯度幅值", gradientImage);
        }
        /// <summary>
        /// 二值化图像
        /// </summary>
        public static void BlurImage(Mat gradientImage, Mat blurImage, Mat thresholdImage)
        {
            //对图片进行相应的模糊化,使一些噪点消除
            //new OpenCvSharp.Size(12, 12);   (9,9)
            Cv2.Blur(gradientImage, blurImage, new OpenCvSharp.Size(6, 6));
            //Cv2.GaussianBlur(gradientImage, blurImage, new OpenCvSharp.Size(7, 7), 0);//Size必须是奇数
            //模糊化以后进行阈值化,得到到对应的黑白二值化图像,二值化的阈值可以根据实际情况调整
            Cv2.Threshold(blurImage, thresholdImage, 210, 255, ThresholdTypes.Binary);
            //看看二值化图像
            //Imshow("二值化图像", thresholdImage);
        }
        /// <summary>
        /// 闭运算
        /// </summary>
        public static void MorphImage(Mat thresholdImage, Mat morphImage)
        {
            //二值化以后的图像,条形码之间的黑白没有连接起来,就要进行形态学运算,消除缝隙,相当于小型的黑洞,选择闭运算
            //因为是长条之间的缝隙,所以需要选择宽度大于长度
            Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(21, 7));
            Cv2.MorphologyEx(thresholdImage, morphImage, MorphTypes.Close, kernel);
            //看看形态学操作以后的图像
            //Imshow("闭运算", morphImage);
        }
        /// <summary>
        /// 膨胀腐蚀
        /// </summary>
        public static void DilationErosionImage(Mat morphImage)
        {
            //现在要让条形码区域连接在一起,所以选择膨胀腐蚀,而且为了保持图形大小基本不变,应该使用相同次数的膨胀腐蚀
            //先腐蚀,让其他区域的亮的地方变少最好是消除,然后膨胀回来,消除干扰,迭代次数根据实际情况选择
            OpenCvSharp.Size size = new OpenCvSharp.Size(3, 3);
            OpenCvSharp.Point point = new OpenCvSharp.Point(-1, -1);
            Cv2.Erode(morphImage, morphImage, Cv2.GetStructuringElement(MorphShapes.Rect, size), point, 4);
            Cv2.Dilate(morphImage, morphImage, Cv2.GetStructuringElement(MorphShapes.Rect, size), point, 4);
            //看看形态学操作以后的图像
            //Imshow("膨胀腐蚀", morphImage);
        }

        /// <summary>
        /// 显示处理后的图片
        /// </summary>
        /// <param name="name">处理过程名称</param>
        /// <param name="srcImage">图片盒子</param>
        public static void Imshow(string name, Mat srcImage)
        {
            using (var window = new Window(name, image: srcImage))
            {
                Cv2.WaitKey(0);
            }
            //Cv2.ImShow(name, srcImage);
            //Cv2.WaitKey(0);
        }
        /// <summary>
        /// 旋转图片
        /// </summary>
        public static void RotateImage(Mat src, Mat dst, double angle, double scale)
        {
            var imageCenter = new Point2f(src.Cols / 2f, src.Rows / 2f);
            var rotationMat = Cv2.GetRotationMatrix2D(imageCenter, angle, scale);
            Cv2.WarpAffine(src, dst, rotationMat, src.Size());
        }
    }
}
